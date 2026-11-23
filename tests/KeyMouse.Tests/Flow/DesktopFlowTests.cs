namespace KeyMouse.Tests;

/// <summary>Exercise live flow control and OCR coordinates with a private desktop fixture, never Windows input.</summary>
internal static class DesktopFlowTests
{
    private sealed class Fixture : IFlowDesktop
    {
        internal bool Available = true;
        internal int Reads, Validations;
        internal bool RestoreSeen;
        public WindowResolution Resolve(WindowSelector selector, bool allowRestore)
        {
            RestoreSeen |= allowRestore;
            var result = new WindowResolution();
            if (Available && selector.ProcessName == "fixture") result.Usable.Add(new WindowInfo { Handle = (IntPtr)13 });
            return result;
        }
        public bool IsFront(string process) => process == "fixture";
        public (int X, int Y)? WindowOrigin(WindowInfo window) => (100, 200);
        public void Validate(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect) => Validations++;
        public (string Text, double? Confidence, LineInfo[] Lines) Read(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect)
        {
            Reads++;
            return ("OK", 99, [new LineInfo("0", "OK", [2, 4, 6, 8], [new WordInfo("OK", 99, [2, 4, 6, 8])])]);
        }
    }

    internal static void Run()
    {
        using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
        string path = Path.GetTempFileName();
        try
        {
            foreach (string space in new[] { "screen", "window", "client" })
            {
                var desktop = new Fixture();
                File.WriteAllText(path, $$"""{"format":"keymouse-flow","version":1,"allowRestore":true,"steps":[{"type":"click-text","text":"OK","target":{"process":"fixture"},"region":{"space":"{{space}}","x":10,"y":20,"width":32,"height":32},"confirm":1}]}""");
                FlowStep? click = null; bool restore = false;
                int exit = FlowRunner.Run([path], new(), (step, allow) => { click = step; restore = allow; return 0; }, desktop);
                Harness.Check($"{space} OCR click uses matched box centre, correct target and restore", exit == 0 && click?.At is { } at && restore && desktop.Reads == 1 &&
                    at.X == (space == "window" ? 115 : 15) && at.Y == (space == "window" ? 228 : 28) && at.Space == (space == "client" ? "client" : "screen") &&
                    (space == "screen" ? click.Target == null : click.Target?.Process == "fixture" && desktop.RestoreSeen == (space == "window")));
                desktop.Reads = desktop.Validations = 0; click = null;
                exit = FlowRunner.Run([path, "--dry-run"], new(), (_, _) => throw new Exception("dry OCR clicked"), desktop);
                Harness.Check($"{space} dry OCR validates without reading or clicking", exit == 0 && desktop.Reads == 0 && desktop.Validations == 1);
            }
            File.WriteAllText(path, FlowRun.Flow("""[{"type":"read-text","into":"answer","target":{"process":"fixture"},"region":{"x":0,"y":0,"width":30,"height":30}}]"""));
            var missing = new Fixture { Available = false };
            Harness.Check("lost target returns 4 before reading or dispatching", FlowRunner.Run([path], new(), (_, _) => throw new Exception("unexpected input"), missing) == 4 && missing.Reads == 0);
        }
        finally { File.Delete(path); }
    }
}
