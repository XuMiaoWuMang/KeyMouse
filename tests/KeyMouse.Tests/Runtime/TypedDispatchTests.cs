namespace KeyMouse.Tests;

internal static class TypedDispatchTests
{
    internal static void Run()
    {
        using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
        var screen = new FlowPoint { Space = "screen", X = 42, Y = 37 };
        void Leaf(FlowStep step, int exit, long suppressed)
        {
            using var run = ExecutionMode.Begin(true);
            int code = FlowCommandExecutor.Execute(step, false);
            Harness.Check($"{step.Type} {step.Button ?? step.Combo ?? step.Text}: expected exit {exit}, suppressed {suppressed}",
                code == exit && ExecutionMode.SuppressedEvents == suppressed && ExecutionMode.InjectedEvents == 0,
                $"exit={code} suppressed={ExecutionMode.SuppressedEvents}");
        }
        Leaf(new() { Type = "keydown", Combo = "shift" }, 0, 1);
        Leaf(new() { Type = "keyup", Combo = "shift" }, 0, 1);
        Leaf(new() { Type = "key", Combo = "a+ctrl" }, 0, 4);
        Leaf(new() { Type = "type", Text = "--x", IntervalMs = 0 }, 0, 6);
        Leaf(new() { Type = "click", Button = "right", At = screen }, 0, 3);
        Leaf(new() { Type = "move", At = screen }, 0, 1);
        Leaf(new() { Type = "mousedown", Button = "x2" }, 0, 1);
        Leaf(new() { Type = "mouseup", Button = "x2" }, 0, 1);
        Leaf(new() { Type = "wheel", At = screen, Delta = -120, Axis = "horizontal" }, 0, 2);
        Leaf(new() { Type = "drag", From = screen, To = screen, DurationMs = 1 }, 0, 23);
        Leaf(new() { Type = "keyhold", Combo = "shift", Ms = 500 }, 0, 0);
        Leaf(new() { Type = "mousehold", Button = "right", At = screen, Ms = 50 }, 0, 0);
        Leaf(new() { Type = "hold", Button = "x1", Ms = 20 }, 0, 0);
        Leaf(new() { Type = "hold", Combo = "a", Ms = 20 }, 0, 0);
        // Validate first, including before moving the pointer for an invalid click.
        Leaf(new() { Type = "click", Button = "bad", At = screen }, 2, 0);
        Leaf(new() { Type = "type", Text = "" }, 2, 0);
        Leaf(new() { Type = "key", Combo = "+" }, 2, 0);
        Leaf(new() { Type = "move", At = new FlowPoint() }, 2, 0);
        Leaf(new() { Type = "move", At = new FlowPoint { Space = "unknown" } }, 2, 0);
        Leaf(new() { Type = "wheel", Axis = "diagonal" }, 2, 0);
        Leaf(new() { Type = "drag", From = screen, To = new FlowPoint() }, 2, 0);
        Leaf(new() { Type = "focus" }, 2, 0);

        void Cli(string[] args, int expected, long suppressed = 0)
        {
            using var run = ExecutionMode.Begin(true);
            var code = Commands.Execute(args);
            Harness.Check($"{string.Join(' ', args)}: dry-run exit {expected}, suppressed {suppressed}",
                code == expected && ExecutionMode.InjectedEvents == 0 && ExecutionMode.SuppressedEvents == suppressed);
        }
        Cli(["mouse", "moveby", "3", "-2"], 0, 1);
        Cli(["mouse", "dblclick", "-x", "2", "-y", "4"], 0, 5);
        Cli(["mouse", "drag", "1", "2", "3", "4", "--steps", "2", "--duration", "1"], 0, 5);
        Cli(["mouse", "wheel"], 2);
        Cli(["mouse", "click", "-x", "2"], 2);
        Cli(["mouse", "drag", "1", "2"], 2);
        Cli(["key", "press", "enter"], 0, 2);
        Cli(["key", "down"], 2);
        Cli(["key", "type", "--stdin"], 2); // nested/Runner dispatcher cannot consume stdin
        Cli(["schema", "drag"], 0);
        Cli(["schema", "no-such-step"], 2);
        using var help = new StringWriter();
        using var capture = CommandOutput.Redirect(help, TextWriter.Null);
        Harness.Check("CLI help advertises the dry-run and diagnostics entry points", Commands.Execute(["--help"]) == 0 &&
            help.ToString().Contains("--dry-run") && help.ToString().Contains("doctor"));
    }
}
