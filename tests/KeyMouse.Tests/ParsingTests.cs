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

        var expanded = ScriptRunner.Expand(
            ScriptRunner.Parse(new[] { "key type \"${title}\" --process ${app}", "sleep 100" }), variables);
        Harness.Sequence("a value with spaces stays a single argument",
            new[] { "key", "type", "无标题 记事本", "--process", "notepad" }, expanded[0].Tokens);
        Harness.Equal("line numbers survive expansion", 2, expanded[1].LineNumber);

        // Regression: the pass used to be skipped entirely when no --set was given, so a raw
        // ${name} leaked into the command and failed later with a confusing message.
        Harness.Throws<CommandFailure>("a ${var} with no --set at all is still refused",
            () => ScriptRunner.Expand(
                ScriptRunner.Parse(new[] { "key type \"${title}\"" }), new Dictionary<string, string>()));

        var untouched = ScriptRunner.Expand(
            ScriptRunner.Parse(new[] { "key press enter" }), new Dictionary<string, string>());
        Harness.Sequence("tokens without ${} pass through untouched",
            new[] { "key", "press", "enter" }, untouched[0].Tokens);

        var repeated = ScriptRunner.Expand(
            ScriptRunner.Parse(new[] { "key type \"${app}-${app}\"" }), variables);
        Harness.Sequence("a token may use a variable twice",
            new[] { "key", "type", "notepad-notepad" }, repeated[0].Tokens);

        Harness.Throws<CommandFailure>("an undefined variable is refused",
            () => ScriptRunner.Expand(ScriptRunner.Parse(new[] { "key press ${nope}" }), variables));

        Harness.Throws<CommandFailure>("an unterminated ${ is refused",
            () => ScriptRunner.Expand(ScriptRunner.Parse(new[] { "key press ${nope" }), variables));
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

        var rest = Program.ExtractGlobalOptions(new[] { "mouse", "click", "left", "--title", "记事本", "-n", "2" }, out var g);
        Harness.Equal("--title takes the next argument", "记事本", g.Title);
        Harness.Check("selector is detected", g.HasSelector);
        Harness.Sequence("command arguments stay in place", new[] { "mouse", "click", "left", "-n", "2" }, rest);

        Program.ExtractGlobalOptions(new[] { "mouse", "click", "--title=X", "--pid=1234" }, out g);
        Harness.Equal("--flag=value form", "X", g.Title);
        Harness.Equal("numeric inline value", (uint?)1234, g.Pid);

        rest = Program.ExtractGlobalOptions(new[] { "mouse", "move", "-100", "-200" }, out g);
        Harness.Sequence("negative numbers stay positional", new[] { "mouse", "move", "-100", "-200" }, rest);
        Harness.Check("negative numbers do not create a selector", !g.HasSelector);

        rest = Program.ExtractGlobalOptions(new[] { "key", "type", "--", "--title", "-wx" }, out g);
        Harness.Sequence("-- ends option parsing", new[] { "key", "type", "--title", "-wx" }, rest);
        Harness.Check("nothing after -- is treated as an option", !g.HasSelector);

        rest = Program.ExtractGlobalOptions(new[] { "mouse", "click", "-wx", "10", "-wy", "20" }, out g);
        Harness.Equal("-wx", 10, g.Wx);
        Harness.Equal("-wy", 20, g.Wy);
        Harness.Sequence("relative flags are consumed", new[] { "mouse", "click" }, rest);

        rest = Program.ExtractGlobalOptions(
            new[] { "mouse", "drag", "-wx", "1", "-wy", "2", "--wx2", "3", "--wy2", "4" }, out g);
        Harness.Equal("drag start x", 1, g.Wx);
        Harness.Equal("drag start y", 2, g.Wy);
        Harness.Equal("drag end x", 3, g.Wx2);
        Harness.Equal("drag end y", 4, g.Wy2);
        Harness.Sequence("all four client flags are consumed", new[] { "mouse", "drag" }, rest);

        Program.ExtractGlobalOptions(new[] { "key", "press", "f24", "--focus-policy", "none", "--allow-restore", "--strict-point" }, out g);
        Harness.Equal("focus policy", "none", g.FocusPolicy);
        Harness.Check("--allow-restore", g.AllowRestore);
        Harness.Check("--strict-point", g.StrictPoint);

        Program.ExtractGlobalOptions(new[] { "key", "press", "f24" }, out g);
        Harness.Equal("focus policy defaults to gentle", "gentle", g.FocusPolicy);
        Harness.Equal("focus attempts default to 3", 3, g.FocusAttempts);

        Harness.Throws<ArgumentException>("a flag without its value is an error",
            () => Program.ExtractGlobalOptions(new[] { "key", "type", "--title" }, out _));
        Harness.Throws<ArgumentException>("a non-numeric value is an error",
            () => Program.ExtractGlobalOptions(new[] { "key", "type", "--pid", "abc" }, out _));
    }

    private static void CommandOptions()
    {
        Harness.Group("command option parsing");

        var (positional, options) = Program.Parse(new[] { "left", "-n", "2", "-i", "50" }, "n", "i");
        Harness.Sequence("positional values", new[] { "left" }, positional);
        Harness.Equal("-n", "2", options["n"]);
        Harness.Equal("-i", "50", options["i"]);

        (positional, options) = Program.Parse(new[] { "wheel", "-120" }, "delta");
        Harness.Sequence("negative positional survives", new[] { "wheel", "-120" }, positional);
        Harness.Check("negative number is not mistaken for a flag", !options.ContainsKey("120"));

        (positional, options) = Program.Parse(new[] { "--button=right" }, "button");
        Harness.Equal("--flag=value form", "right", options["button"]);

        (positional, options) = Program.Parse(new[] { "--all" });
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
