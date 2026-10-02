using System.Text;

namespace KeyMouse;

/// <summary>Switches the send / focus layers consult while a batch runs.</summary>
internal static class ExecutionMode
{
    public static bool DryRun { get; set; }
    public static int SkippedSends { get; private set; }

    public static void NoteSkippedSend() => SkippedSends++;
}

/// <summary>
/// Runs a batch of KeyMouse commands, one per line, in order. Every line goes through
/// exactly the same dispatcher as a standalone invocation, so selectors, the eligibility
/// gate and the focus verification apply per line - the runner adds sequencing, not a
/// second command language.
/// </summary>
internal static class ScriptRunner
{
    private static bool _running;

    public static int Run(string[] args, Program.GlobalOptions global, Func<string[], int> execute)
    {
        if (_running)
            return Fail(2, "run: nested scripts are refused - a script cannot start another script");

        if (global.HasSelector || global.Wx.HasValue || global.Wy.HasValue || global.Pick is not null)
            return Fail(2, "run: window options belong on the individual lines, not on 'run' itself");

        string? path = null;
        int delayMs = 0;
        bool keepGoing = false, dryRun = false, echo = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--delay":
                    if (++i >= args.Length || !int.TryParse(args[i], out delayMs) || delayMs < 0)
                        return Fail(2, "--delay needs a non-negative number of milliseconds");
                    break;
                case "--keep-going" or "-k":
                    keepGoing = true;
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--echo":
                    echo = true;
                    break;
                default:
                    if (arg.Length > 1 && arg[0] == '-') return Fail(2, $"run: unknown option '{arg}'");
                    if (path is not null) return Fail(2, "run: only one script can be given");
                    path = arg;
                    break;
            }
        }

        if (path is null)
            return Fail(2, "usage: KeyMouse run <file|-> [--delay MS] [--keep-going] [--dry-run] [--echo]");

        List<(int LineNumber, List<string> Tokens)> commands;
        try
        {
            commands = Parse(ReadScript(path));
        }
        catch (CommandFailure ex)
        {
            return Fail(ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(1, $"cannot read script '{path}': {ex.Message}");
        }

        if (commands.Count == 0)
            return Fail(2, $"script '{path}' contains no commands");

        string source = path == "-" ? "stdin" : Path.GetFullPath(path);
        var failures = new List<string>();
        int firstFailureCode = 0;

        ExecutionMode.DryRun = dryRun;
        _running = true;
        try
        {
            Console.WriteLine(dryRun
                ? $"DRY RUN: {commands.Count} command(s) from {source} - gate checks still run, nothing will be sent"
                : $"{commands.Count} command(s) from {source}");

            int total = commands.Count;
            for (int i = 0; i < total; i++)
            {
                var (lineNumber, tokens) = commands[i];
                string shown = string.Join(' ', tokens.Select(Quote));
                string tag;
                string? captured = null;

                if (tokens[0].Equals("sleep", StringComparison.OrdinalIgnoreCase))
                {
                    if (tokens.Count != 2 || !int.TryParse(tokens[1], out int ms) || ms < 0)
                        return Fail(2, $"line {lineNumber}: 'sleep' takes one non-negative number of milliseconds");
                    Thread.Sleep(ms);
                    tag = "wait";
                }
                else
                {
                    var stdout = new StringWriter();
                    var stderr = new StringWriter();
                    TextWriter oldOut = Console.Out;
                    TextWriter oldErr = Console.Error;
                    int code;
                    Console.SetOut(stdout);
                    Console.SetError(stderr);
                    try
                    {
                        code = execute(tokens.ToArray());
                    }
                    finally
                    {
                        Console.SetOut(oldOut);
                        Console.SetError(oldErr);
                    }

                    captured = (stdout.ToString() + stderr.ToString()).Trim();
                    tag = code == 0 ? (dryRun ? "DRY" : "ok") : "FAIL";
                    if (code != 0)
                    {
                        failures.Add($"line {lineNumber}: {shown}");
                        if (firstFailureCode == 0) firstFailureCode = code;
                    }
                }

                Console.WriteLine($"[{i + 1,3}/{total}] {tag,-4} {shown}");
                if (captured is { Length: > 0 } && (echo || tag == "FAIL"))
                    foreach (string detail in captured.Split('\n'))
                        Console.WriteLine("           " + detail.TrimEnd());

                if (tag == "FAIL" && !keepGoing)
                {
                    Console.WriteLine($"stop: first failure at line {lineNumber} (exit {firstFailureCode}); use --keep-going to run the rest");
                    break;
                }

                if (delayMs > 0 && i < total - 1) Thread.Sleep(delayMs);
            }
        }
        finally
        {
            _running = false;
            ExecutionMode.DryRun = false;
        }

        if (dryRun)
            Console.WriteLine($"\ndry run finished - {ExecutionMode.SkippedSends} input event(s) suppressed, nothing was sent");

        if (failures.Count == 0)
        {
            if (!dryRun) Console.WriteLine($"\nall {commands.Count} command(s) succeeded");
            return 0;
        }

        Console.WriteLine($"\n{failures.Count} command(s) failed:");
        foreach (string failure in failures) Console.WriteLine("  " + failure);
        return firstFailureCode == 0 ? 1 : firstFailureCode;
    }

    // ------------------------------------------------------------- parsing

    internal static List<(int LineNumber, List<string> Tokens)> Parse(string[] lines)
    {
        var commands = new List<(int, List<string>)>();
        for (int i = 0; i < lines.Length; i++)
        {
            List<string> tokens = Tokenize(lines[i]);
            if (tokens.Count == 0) continue;

            // tolerate lines copied straight out of the README
            if (tokens[0].Equals("KeyMouse", StringComparison.OrdinalIgnoreCase) ||
                tokens[0].Equals("KeyMouse.exe", StringComparison.OrdinalIgnoreCase))
            {
                tokens.RemoveAt(0);
                if (tokens.Count == 0) continue;
            }

            commands.Add((i + 1, tokens));
        }
        return commands;
    }

    /// <summary>
    /// Splits one line into argv. Supports "double quoted" tokens, \" and \\ escapes,
    /// '#' comments (whole line or trailing) and ignores blank lines.
    /// </summary>
    internal static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
            if (i >= line.Length || line[i] == '#') break;

            var token = new StringBuilder();
            bool inQuotes = false;
            while (i < line.Length)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '\\'))
                {
                    token.Append(line[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    i++;
                    continue;
                }
                if (!inQuotes && (char.IsWhiteSpace(c) || c == '#')) break;
                token.Append(c);
                i++;
            }
            if (inQuotes) throw new CommandFailure(2, $"unbalanced quote in line: {line.Trim()}");
            tokens.Add(token.ToString());
        }
        return tokens;
    }

    private static string[] ReadScript(string path)
    {
        byte[] bytes = path == "-" ? ReadStdin() : File.ReadAllBytes(path);

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new CommandFailure(2, "the script is not valid UTF-8 - save it as UTF-8 " +
                                        "(ANSI/GBK will not decode, and Chinese text needs UTF-8)");
        }

        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static byte[] ReadStdin()
    {
        using Stream stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string Quote(string token) =>
        token.Length == 0 || token.Any(ch => char.IsWhiteSpace(ch) || ch == '#') ? $"\"{token}\"" : token;

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine("error: " + message);
        return code;
    }
}
