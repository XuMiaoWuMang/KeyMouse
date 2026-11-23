using System.Drawing;
using System.Windows.Forms;

namespace KeyMouse.Tests;

/// <summary>Opt-in Windows integration: ten flows each read their own off-screen HWND. No desktop capture,
/// focus changes, hooks, or input injection. A wrong HWND is detected from the fixture pixel color.</summary>
internal static class WindowIsolation
{
    // PrintWindow may report success with a blank DIB for an invisible HWND. Use a real painted
    // tool window entirely beyond the virtual desktop, without taskbar presence or activation.
    private sealed class Swatch : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; }
        }
    }
    internal static int Run()
    {
        string directory = Directory.CreateTempSubdirectory("km-window-e2e-").FullName;
        using var barrier = new Barrier(10);
        try
        {
            var runs = Enumerable.Range(1, 10).Select(i => Task.Factory.StartNew(() =>
            {
                var screen = SystemInformation.VirtualScreen;
                using var panel = new Swatch
                {
                    FormBorderStyle = FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(screen.Right + 128 + i * 64, screen.Bottom + 128),
                    ClientSize = new Size(32, 32),
                    BackColor = Color.FromArgb(i * 20, 15, 47)
                };
                panel.Show(); panel.Refresh();
                if (screen.IntersectsWith(panel.Bounds)) throw new InvalidOperationException("Test window was not off screen");
                var handle = panel.Handle;
                string path = Path.Combine(directory, $"{i}.json");
                File.WriteAllText(path, FlowRun.Flow("""[{"type":"keydown","combo":"a"}]"""));
                using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
                bool observed = false;
                return FlowRunner.Run([path, "--dry-run"], new(), (_, _) =>
                {
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10))) return 1;
                    const int offset = 4 * (16 * 32 + 16);
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        panel.Refresh();
                        var frame = NativeCapture.TryCapture(handle, 32, 32);
                        if (frame != null && (frame.Bgra[offset] | frame.Bgra[offset + 1] | frame.Bgra[offset + 2]) != 0)
                        {
                            observed = frame.Bgra[offset] == 47 && frame.Bgra[offset + 1] == 15 && frame.Bgra[offset + 2] == i * 20;
                            break; // never retry a wrong swatch
                        }
                        Thread.Sleep(16); // DWM may not have presented the first frame yet
                    }
                    return observed ? 0 : 1;
                }) == 0 && observed;
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            Task.WaitAll(runs);
            int n = runs.Count(t => t.Result); Console.WriteLine($"isolated flow/window pixels: {n}/10");
            IntPtr closed;
            using (var panel = new Panel()) closed = panel.Handle;
            try { _ = new WindowInfo { Handle = closed }.Rect; return 1; }
            catch (CommandFailure ex) when (ex.Code == 4) { Console.WriteLine("closed HWND: exit 4"); }
            return n == 10 ? 0 : 1;
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
