namespace KeyMouse.Runtime;

/// <summary>Nested step captures and Runner logs must not replace another job's console writers.</summary>
internal sealed class CommandOutput : IDisposable
{
    private static readonly AsyncLocal<CommandOutput?> Current = new();
    private readonly CommandOutput? _previous = Current.Value;
    private readonly TextWriter _out, _error;

    private CommandOutput(TextWriter output, TextWriter error) => (_out, _error) = (output, error);
    internal static CommandOutput Redirect(TextWriter output, TextWriter error) =>
        Current.Value = new CommandOutput(output, error);
    internal static TextWriter Out => Current.Value?._out ?? Console.Out;
    internal static TextWriter Error => Current.Value?._error ?? Console.Error;
    public void Dispose() => Current.Value = _previous;

    internal static (int ExitCode, string Output) Capture(Func<int> execute)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var capture = CommandOutput.Redirect(stdout, stderr);
        int code = execute();
        return (code, (stdout.ToString() + stderr.ToString()).Trim());
    }
}
