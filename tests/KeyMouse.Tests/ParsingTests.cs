namespace KeyMouse.Tests;

/// <summary>Everything that can be checked without a desktop: script tokenizer,
/// argv/global-option parsing, key names.</summary>
internal static class ParsingTests
{
    public static void Run()
    {
        Tokenizer();
        ScriptLines();
        GlobalOptions();
        CommandOptions();
        KeyNames();
        RunOptions();
        RetryAndVariables();
        WaitCommands();
        TargetInheritance();
        Loops();
        SingleProcessExecution();
    }

    private static void Loops()
    {
        Harness.Group("repeat/end analysis");

        Harness.Equal("a plain script needs no loops",
            3, Validate(new[] { "key press a", "sleep 10", "key press b" }, new()).TotalExecutions);

        Harness.Equal("a body is counted once per iteration",
            6, Validate(new[] { "repeat 3", "key press a", "key press b", "end" }, new()).TotalExecutions);

        // Nesting needs names: two unnamed loops would both bind ${i} and the inner one
        // would silently hide the outer, which is the kind of surprise worth refusing.
        Harness.Equal("named nested loops multiply",
            6, Validate(new[] { "repeat 2 as row", "repeat 3 as col", "key press a", "end", "end" }, new()).TotalExecutions);

        Harness.Equal("an inner loop sees the outer variable",
            6, Validate(new[] { "repeat 2 as row", "repeat 3 as col", "key press ${row}", "end", "end" }, new()).TotalExecutions);

        Harness.Equal("repeat 0 contributes nothing",
            1, Validate(new[] { "repeat 0", "key press a", "end", "key press b" }, new()).TotalExecutions);

        Harness.Equal("commands after a loop still count",
            4, Validate(new[] { "repeat 3", "key press a", "end", "key press b" }, new()).TotalExecutions);

        // Structure is checked up front, so a malformed loop cannot abort a half-run script.
        Harness.Throws<CommandFailure>("a repeat without end is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat 3", "key press a" })));
        Harness.Throws<CommandFailure>("an end without repeat is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "key press a", "end" })));
        Harness.Throws<CommandFailure>("a non-numeric repeat count is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat abc", "end" })));
        Harness.Throws<CommandFailure>("a negative repeat count is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat -1", "end" })));
        Harness.Throws<CommandFailure>("a missing repeat count is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat", "end" })));
        Harness.Throws<CommandFailure>("end takes no arguments",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat 2", "end 3" })));
        Harness.Throws<CommandFailure>("shadowing a loop variable is refused",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat 2", "repeat 2", "end", "end" })));
        Harness.Throws<CommandFailure>("an absurd loop is refused before it runs",
            () => ScriptRunner.AnalyzeLoops(ScriptRunner.Parse(new[] { "repeat 1000000", "repeat 1000000", "key press a", "end", "end" })));

        var plan = ScriptRunner.AnalyzeLoops(
            ScriptRunner.Parse(new[] { "repeat 3 as row", "key press ${row}", "end" }));
        Harness.Equal("a named loop exposes its name", "row", plan.Enclosing[1][0].Name);

        // The variable is only in scope inside the body - outside it, validation must say so.
        Harness.Throws<CommandFailure>("a loop variable used outside its loop is refused",
            () => Validate(new[] { "key press ${row}", "repeat 2 as row", "key press a", "end" }, new()));

        Harness.Check("a loop variable passes validation inside its body",
            !Throws(() => Validate(new[] { "repeat 2 as row", "key press ${row}", "end" }, new())));

        Harness.Check("an unclosed ${ is refused by loop-aware validation",
            Throws(() => Validate(new[] { "repeat 2", "key press ${oops", "end" }, new())));

        Harness.Check("a loop with an undefined variable is refused up front",
            Throws(() => Validate(new[] { "repeat 2", "key press ${nope}", "end" }, new())));
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (CommandFailure) { return true; }
    }

    /// <summary>
    /// v2 guarantee: a script runs every command in the process that started it. The runner
    /// hands each one to the delegate it was given, so counting the calls is the assertion -
    /// if anything ever spawned a process per line, that delegate would stop being called.
    /// </summary>
    private static void SingleProcessExecution()
    {
        Harness.Group("a script runs in one process");

        string path = Path.Combine(Path.GetTempPath(), "keymouse-unit-singleproc.txt");
        File.WriteAllLines(path,
            new[] { "repeat 3 as row", "key type ${word}-${row}", "end", "key press b", "key type ${word}" },
            new System.Text.UTF8Encoding(false));

        var seen = new List<string>();
        int exitCode = 0;

        var writer = new StringWriter();
        TextWriter oldOut = Console.Out;
        TextWriter oldErr = Console.Error;
        Console.SetOut(writer);
        Console.SetError(writer);
        try
        {
            exitCode = ScriptRunner.Run(
                new[] { path, "--set", "word=hi" },
                new Commands.GlobalOptions(),
                tokens => { seen.Add(string.Join(' ', tokens)); return 0; });
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
            File.Delete(path);
        }

        Harness.Equal("the script succeeded", 0, exitCode);
        Harness.Equal("every iteration ran through the in-process dispatcher", 5, seen.Count);
        Harness.Equal("the loop variable is substituted per iteration", "key type hi-0", seen[0]);
        Harness.Equal("the loop variable advances", "key type hi-2", seen[2]);
        Harness.Equal("the command after the loop ran in the same process", "key press b", seen[3]);

        // Regression: the execution path once skipped ${} substitution entirely when the line
        // was not inside a loop, so --set variables only worked in loop bodies.
        Harness.Equal("a --set variable is substituted outside a loop too", "key type hi", seen[4]);

        // The loop is expanded by the runner, not by re-entering the command line parser.
        Harness.Check("the dispatcher never saw a repeat/end line",
            !seen.Any(line => line.StartsWith("repeat") || line == "end"));
    }

    private static void TargetInheritance()
    {
        Harness.Group("script target inheritance");

        Harness.Check("a selector is recognised in a line",
            ScriptRunner.ExtractTargetTokens(new[] { "key", "type", "hi", "--process", "notepad" })
                is { Count: 2 } t1 && t1[0] == "--process" && t1[1] == "notepad");

        Harness.Check("--flag=value keeps its inline value",
            ScriptRunner.ExtractTargetTokens(new[] { "mouse", "click", "--title=My Window" })
                is { Count: 1 } t2 && t2[0] == "--title=My Window");

        Harness.Check("every selector of a line is kept",
            ScriptRunner.ExtractTargetTokens(new[] { "window", "focus", "--class", "Notepad", "--pick", "2" })
                is { Count: 4 } t3 && t3[2] == "--pick" && t3[3] == "2");

        Harness.Check("a line without a selector yields no target",
            ScriptRunner.ExtractTargetTokens(new[] { "key", "type", "hi" }) is null);

        Harness.Check("client-area flags are not a target",
            ScriptRunner.ExtractTargetTokens(new[] { "mouse", "click", "-wx", "10", "-wy", "20" }) is null);

        Harness.Check("mouse inherits", ScriptRunner.InheritsTarget(new[] { "mouse", "click", "left" }));
        Harness.Check("key inherits", ScriptRunner.InheritsTarget(new[] { "key", "type", "hi" }));
        Harness.Check("keyboard alias inherits", ScriptRunner.InheritsTarget(new[] { "keyboard", "press", "a" }));

        Harness.Check("window list does not inherit", !ScriptRunner.InheritsTarget(new[] { "window", "list" }));
        Harness.Check("waitfor does not inherit", !ScriptRunner.InheritsTarget(new[] { "waitfor", "--timeout", "100" }));
        Harness.Check("window focus does not inherit", !ScriptRunner.InheritsTarget(new[] { "window", "focus" }));
    }

    private static void RunOptions()
    {
        Harness.Group("run option parsing");

        var options = new ScriptOptions();
        Harness.Check("a plain script path is accepted", ScriptRunner.ParseOptions(new[] { "s.txt" }, options) is null);
        Harness.Equal("script path is kept", "s.txt", options.Path);
        Harness.Equal("retry defaults to 0", 0, options.Retry);
        Harness.Equal("retry delay defaults to 300", 300, options.RetryDelayMs);
        Harness.Check("dry run defaults to off", !options.DryRun);

        options = new ScriptOptions();
        ScriptRunner.ParseOptions(
            new[] { "-", "--retry", "2", "--retry-delay", "50", "--set", "app=notepad", "--set", "t=a=b", "--dry-run" },
            options);
        Harness.Equal("stdin marker is kept as the path", "-", options.Path);
        Harness.Equal("retry count", 2, options.Retry);
        Harness.Equal("retry delay", 50, options.RetryDelayMs);
        Harness.Equal("variable value", "notepad", options.Variables["app"]);
        Harness.Equal("a value may contain '='", "a=b", options.Variables["t"]);
        Harness.Check("dry run is recorded", options.DryRun);

        Harness.Check("--set without '=' is refused",
            ScriptRunner.ParseOptions(new[] { "s.txt", "--set", "oops" }, new ScriptOptions()) is not null);
        Harness.Check("unknown option is refused",
            ScriptRunner.ParseOptions(new[] { "s.txt", "--nope" }, new ScriptOptions()) is not null);
        Harness.Check("two scripts are refused",
            ScriptRunner.ParseOptions(new[] { "a.txt", "b.txt" }, new ScriptOptions()) is not null);
        Harness.Check("a non-numeric retry count is refused",
            ScriptRunner.ParseOptions(new[] { "s.txt", "--retry", "x" }, new ScriptOptions()) is not null);
        Harness.Check("a negative retry count is refused",
            ScriptRunner.ParseOptions(new[] { "s.txt", "--retry", "-1" }, new ScriptOptions()) is not null);
    }

    private static void RetryAndVariables()
    {
        Harness.Group("retry policy");
        Harness.Check("selector failure is retryable", ScriptRunner.IsRetryable(3));
        Harness.Check("unusable target is retryable", ScriptRunner.IsRetryable(4));
        Harness.Check("focus failure is retryable", ScriptRunner.IsRetryable(5));
        Harness.Check("success is never retried", !ScriptRunner.IsRetryable(0));
        Harness.Check("runtime failure is never retried (it may have sent something)",
            !ScriptRunner.IsRetryable(1));
        Harness.Check("usage error is never retried", !ScriptRunner.IsRetryable(2));

        Harness.Group("${variable} substitution");
        var variables = new Dictionary<string, string> { ["app"] = "notepad", ["title"] = "无标题 记事本" };

        Harness.Sequence("a value with spaces stays a single argument",
            new[] { "key", "type", "无标题 记事本", "--process", "notepad" },
            new List<string> { "key", "type", "${title}", "--process", "${app}" }
                .Select(token => ScriptRunner.Substitute(token, variables, 1)).ToList());

        Harness.Sequence("tokens without ${} pass through untouched",
            new[] { "key", "press", "enter" },
            new List<string> { "key", "press", "enter" }
                .Select(token => ScriptRunner.Substitute(token, variables, 1)).ToList());

        Harness.Equal("a token may use a variable twice", "notepad-notepad",
            ScriptRunner.Substitute("${app}-${app}", variables, 1));

        // Regression: the pass used to be skipped entirely when no --set was given, so a raw
        // ${name} leaked into the command and failed later with a confusing message.
        Harness.Throws<CommandFailure>("a ${var} with no --set at all is still refused",
            () => Validate(new[] { "key type \"${title}\"" }, new Dictionary<string, string>()));

        Harness.Throws<CommandFailure>("an undefined variable is refused",
            () => Validate(new[] { "key press ${nope}" }, variables));

        Harness.Throws<CommandFailure>("an unterminated ${ is refused",
            () => Validate(new[] { "key press ${nope" }, variables));

        Harness.Check("a defined variable passes validation",
            !Throws(() => Validate(new[] { "key type \"${app}\"" }, variables)));
    }

    private static ScriptRunner.LoopPlan Validate(string[] lines, Dictionary<string, string> variables)
    {
        var commands = ScriptRunner.Parse(lines);
        var plan = ScriptRunner.AnalyzeLoops(commands);
        ScriptRunner.ValidateVariables(commands, plan, variables);
        return plan;
    }

    private static void WaitCommands()
    {
        Harness.Group("wait pseudo-command parsing");

        var parsed = ScriptRunner.ParseWait(new List<string>
        {
            "waitfor", "--process", "notepad", "--timeout", "1500", "--interval", "50"
        });
        Harness.Check("selector is parsed", parsed.Selector is not null, parsed.Error ?? "");
        Harness.Equal("timeout", 1500, parsed.TimeoutMs);
        Harness.Equal("interval", 50, parsed.IntervalMs);

        var defaults = ScriptRunner.ParseWait(new List<string> { "waitgone", "--title", "x" });
        Harness.Equal("timeout defaults to 5000", 5000, defaults.TimeoutMs);
        Harness.Equal("interval defaults to 200", 200, defaults.IntervalMs);
        Harness.Equal("zero timeout means check once", 0,
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title", "x", "--timeout", "0" }).TimeoutMs);

        Harness.Check("no selector is an error",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--timeout", "100" }).Error is not null);
        Harness.Check("non-numeric timeout is an error",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title", "x", "--timeout", "abc" }).Error is not null);
        Harness.Check("negative timeout is an error",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title", "x", "--timeout", "-1" }).Error is not null);
        Harness.Check("zero interval is an error",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title", "x", "--interval", "0" }).Error is not null);
        Harness.Check("unknown argument is an error",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title", "x", "--bogus" }).Error is not null);
        Harness.Check("a dangling selector value is an error, not a crash",
            ScriptRunner.ParseWait(new List<string> { "waitfor", "--title" }).Error is not null);
    }

    private static void Tokenizer()
    {
        Harness.Group("script tokenizer");
        Harness.Sequence("plain argv", new[] { "key", "press", "enter" }, ScriptRunner.Tokenize("key press enter"));
        Harness.Sequence("quoted token keeps spaces", new[] { "key", "type", "hello world" },
            ScriptRunner.Tokenize("key type \"hello world\""));
        Harness.Sequence("whole-line comment", Array.Empty<string>(), ScriptRunner.Tokenize("# nothing here"));
        Harness.Sequence("blank line", Array.Empty<string>(), ScriptRunner.Tokenize("    "));
        Harness.Sequence("trailing comment", new[] { "key", "press", "enter" }, ScriptRunner.Tokenize("key press enter # why"));
        Harness.Sequence("hash inside quotes survives", new[] { "key", "type", "a#b" }, ScriptRunner.Tokenize("key type \"a#b\""));
        Harness.Sequence("escaped quotes", new[] { "key", "type", "say \"hi\"" }, ScriptRunner.Tokenize("key type \"say \\\"hi\\\"\""));
        Harness.Sequence("escaped backslash", new[] { "key", "type", "a\\b" }, ScriptRunner.Tokenize("key type \"a\\\\b\""));
        Harness.Sequence("extra whitespace collapses", new[] { "a", "b" }, ScriptRunner.Tokenize("   a   b  "));
        Harness.Sequence("empty quoted token", new[] { "key", "type", "" }, ScriptRunner.Tokenize("key type \"\""));
        Harness.Throws<CommandFailure>("unbalanced quote is refused", () => ScriptRunner.Tokenize("key type \"oops"));
    }

    private static void ScriptLines()
    {
        Harness.Group("script line parsing");
        var commands = ScriptRunner.Parse(new[]
        {
            "# header", "", "key press enter", "KeyMouse mouse move 1 2", "   ", "run nested.txt"
        });

        Harness.Equal("blank lines and comments are skipped", 3, commands.Count);
        Harness.Equal("original line numbers survive", 3, commands[0].LineNumber);
        Harness.Equal("later line number", 6, commands[2].LineNumber);
        Harness.Equal("a stray 'KeyMouse' prefix is tolerated", "mouse", commands[1].Tokens[0]);

        var onlyPrefix = ScriptRunner.Parse(new[] { "KeyMouse" });
        Harness.Equal("a line with nothing but the prefix is dropped", 0, onlyPrefix.Count);
    }

    private static void GlobalOptions()
    {
        Harness.Group("global option extraction");

        var rest = Commands.ExtractGlobalOptions(new[] { "mouse", "click", "left", "--title", "记事本", "-n", "2" }, out var g);
        Harness.Equal("--title takes the next argument", "记事本", g.Title);
        Harness.Check("selector is detected", g.HasSelector);
        Harness.Sequence("command arguments stay in place", new[] { "mouse", "click", "left", "-n", "2" }, rest);

        Commands.ExtractGlobalOptions(new[] { "mouse", "click", "--title=X", "--pid=1234" }, out g);
        Harness.Equal("--flag=value form", "X", g.Title);
        Harness.Equal("numeric inline value", (uint?)1234, g.Pid);

        rest = Commands.ExtractGlobalOptions(new[] { "mouse", "move", "-100", "-200" }, out g);
        Harness.Sequence("negative numbers stay positional", new[] { "mouse", "move", "-100", "-200" }, rest);
        Harness.Check("negative numbers do not create a selector", !g.HasSelector);

        rest = Commands.ExtractGlobalOptions(new[] { "key", "type", "--", "--title", "-wx" }, out g);
        Harness.Sequence("-- ends option parsing", new[] { "key", "type", "--title", "-wx" }, rest);
        Harness.Check("nothing after -- is treated as an option", !g.HasSelector);

        rest = Commands.ExtractGlobalOptions(new[] { "mouse", "click", "-wx", "10", "-wy", "20" }, out g);
        Harness.Equal("-wx", 10, g.Wx);
        Harness.Equal("-wy", 20, g.Wy);
        Harness.Sequence("relative flags are consumed", new[] { "mouse", "click" }, rest);

        rest = Commands.ExtractGlobalOptions(
            new[] { "mouse", "drag", "-wx", "1", "-wy", "2", "--wx2", "3", "--wy2", "4" }, out g);
        Harness.Equal("drag start x", 1, g.Wx);
        Harness.Equal("drag start y", 2, g.Wy);
        Harness.Equal("drag end x", 3, g.Wx2);
        Harness.Equal("drag end y", 4, g.Wy2);
        Harness.Sequence("all four client flags are consumed", new[] { "mouse", "drag" }, rest);

        Commands.ExtractGlobalOptions(new[] { "key", "press", "f24", "--focus-policy", "none", "--allow-restore", "--strict-point" }, out g);
        Harness.Equal("focus policy", "none", g.FocusPolicy);
        Harness.Check("--allow-restore", g.AllowRestore);
        Harness.Check("--strict-point", g.StrictPoint);

        Commands.ExtractGlobalOptions(new[] { "key", "press", "f24" }, out g);
        Harness.Equal("focus policy defaults to gentle", "gentle", g.FocusPolicy);
        Harness.Equal("focus attempts default to 3", 3, g.FocusAttempts);

        Harness.Throws<ArgumentException>("a flag without its value is an error",
            () => Commands.ExtractGlobalOptions(new[] { "key", "type", "--title" }, out _));
        Harness.Throws<ArgumentException>("a non-numeric value is an error",
            () => Commands.ExtractGlobalOptions(new[] { "key", "type", "--pid", "abc" }, out _));
    }

    private static void CommandOptions()
    {
        Harness.Group("command option parsing");

        var (positional, options) = Commands.Parse(new[] { "left", "-n", "2", "-i", "50" }, "n", "i");
        Harness.Sequence("positional values", new[] { "left" }, positional);
        Harness.Equal("-n", "2", options["n"]);
        Harness.Equal("-i", "50", options["i"]);

        (positional, options) = Commands.Parse(new[] { "wheel", "-120" }, "delta");
        Harness.Sequence("negative positional survives", new[] { "wheel", "-120" }, positional);
        Harness.Check("negative number is not mistaken for a flag", !options.ContainsKey("120"));

        (positional, options) = Commands.Parse(new[] { "--button=right" }, "button");
        Harness.Equal("--flag=value form", "right", options["button"]);

        (positional, options) = Commands.Parse(new[] { "--all" });
        Harness.Equal("bare flag becomes true", "true", options["all"]);
        Harness.Equal("bare flag is not positional", 0, positional.Count);
    }

    private static void KeyNames()
    {
        Harness.Group("key map");
        Harness.Equal("enter", (ushort)0x0D, KeyMap.Resolve("enter").Vk);
        Harness.Equal("f24", (ushort)0x87, KeyMap.Resolve("f24").Vk);
        Harness.Equal("ctrl", (ushort)0x11, KeyMap.Resolve("ctrl").Vk);
        Harness.Equal("names are case-insensitive", (ushort)0x0D, KeyMap.Resolve("ENTER").Vk);
        Harness.Equal("raw virtual key escape hatch", (ushort)0x5B, KeyMap.Resolve("vk:0x5B").Vk);
        Harness.Equal("numpad enter", (ushort)0x0D, KeyMap.Resolve("numenter").Vk);
        Harness.Check("arrow keys are extended", KeyMap.Resolve("left").Ext);
        Harness.Check("letters are not extended", !KeyMap.Resolve("a").Ext);
        Harness.Throws<ArgumentException>("unknown key is refused", () => KeyMap.Resolve("nosuchkey"));
    }
}
