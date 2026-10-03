namespace KeyMouse.Cli;

/// <summary>
/// The command-line entry point. One product, two entry points: this one, and the editor.
///
/// Everything that is not `serve`, `runner ...` or `flow edit` goes straight into the capability
/// layer, exactly as before - the CLI keeps being a plain blocking tool with exit codes, because that
/// is what scripts and the smoke suite depend on. `serve` starts the resident Runner; `runner ...`
/// drives it from a script instead of doing the work in this process; and the editor connects to the
/// same service over the same pipe. All three run the same dispatch.
/// </summary>
internal static class Program
{
    [STAThread]
    internal static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "serve":
                    return Runner.RunnerHost.Run(args[1..]);
                case "runner":
                    return RunnerCommand.Run(args[1..]);
                case "flow" when args.Length > 1 && args[1] == "edit":
                    // Launching the editor is a front-end concern (it starts another application).
                    return FlowEditorCommand.Run(args[1..]);
            }
        }
        return Commands.Execute(args);
    }
}
