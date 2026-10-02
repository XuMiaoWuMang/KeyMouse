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
