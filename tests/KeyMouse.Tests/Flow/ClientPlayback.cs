using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using W = KeyMouse.Windowing.NativeWindow;

namespace KeyMouse.Tests;

/// <summary>Interactive opt-in: record synthetic observations (no hooks or global injection), then cover
/// and move the target and replay the actual saved JSON in a separate CLI. An identical child in the
/// covering form is a witness. Only our HWNDs ever receive a message.</summary>
internal static class ClientPlayback
{
    private sealed class DesktopUnavailable(string message) : Exception(message);
    private sealed class Surface : Panel
    {
        internal List<(int Msg, int Pos)> Received = [];
        protected override void WndProc(ref Message m)
        {
            if (m.Msg is 0x200 or 0x201 or 0x202 or 0x20A or 0x20E)
            { Received.Add((m.Msg, (int)m.LParam)); m.Result = 0; return; }
            base.WndProc(ref m);
        }
    }
    private sealed class Tool : Form
    {
        private const int NoActivate = 0x08000000, ExStyle = -20;
        [DllImport("user32")] private static extern int GetWindowLongW(nint hwnd, int index);
        [DllImport("user32")] private static extern int SetWindowLongW(nint hwnd, int index, int value);
        private bool settingUp = true;
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; if (settingUp) p.ExStyle |= NoActivate; return p; }
        }
        internal void AllowActivationDuringReplay()
        {
            settingUp = false;
            SetWindowLongW(Handle, ExStyle, GetWindowLongW(Handle, ExStyle) & ~NoActivate);
            if ((GetWindowLongW(Handle, ExStyle) & NoActivate) != 0) throw new InvalidOperationException("Fixture still has WS_EX_NOACTIVATE");
        }
        internal readonly Surface Pad = new() { Bounds = new Rectangle(10, 10, 110, 55) };
        internal readonly Button Button = new() { Bounds = new Rectangle(10, 70, 100, 28), Text = "Apply" };
        internal int Clicks, Activations;
        protected override bool ShowWithoutActivation => true;
        internal Tool(string name, Point at)
        {
            Text = name; StartPosition = FormStartPosition.Manual; Location = at; ClientSize = new Size(140, 110);
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            Controls.AddRange([Pad, Button]); Button.Click += (_, _) => Clicks++; Activated += (_, _) => Activations++;
        }
    }
    internal static int Run(string cli)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { Test(cli); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error == null) return 0;
        Console.Error.WriteLine(error.Message); return error is DesktopUnavailable ? 3 : 1;
    }
    private static void Test(string cli)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var area = Screen.PrimaryScreen!.WorkingArea;
        var at = new Point(area.Right - 240, area.Bottom - 200);
        var front = W.GetForegroundWindow(); var cursor = Cursor.Position;
        if (front == 0) throw new DesktopUnavailable("No foreground window on the interactive desktop; playback not run");
        using var target = new Tool("KM target " + Guid.NewGuid(), at);
        using var cover = new Tool("KM witness " + Guid.NewGuid(), at);
        string dir = Directory.CreateTempSubdirectory("km-client-").FullName;
        string path = Path.Combine(dir, "recording.json");
        void Require(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }
        try
        {
            target.BackColor = Color.SteelBlue; cover.BackColor = Color.DarkRed;
            target.Button.Text = "APPLY"; cover.Button.Text = "OTHER";
            using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel);
            target.Button.Font = cover.Button.Font = font;
            target.Show(); target.Refresh(); Application.DoEvents();
            if (W.GetForegroundWindow() != front) throw new DesktopUnavailable($"Foreground changed during setup ({front:X}->{W.GetForegroundWindow():X}); playback not run");
            var recorder = new Recorder.Session(path, 0, 2, true, 0) { ClientPoints = true };
            uint t = recorder.StartedAt;
            void Event(HookKind kind, int x, int y, int data = 0, int delay = 10)
            {
                var screen = target.PointToScreen(new Point(x, y));
                Require(WindowLocator.WindowAt(screen.X, screen.Y) == target.Handle, $"Fixture point not on private target: point={screen} bounds={target.Bounds}, area={area}, root={target.Handle:X}, hit={WindowLocator.WindowAt(screen.X, screen.Y):X}");
                recorder.OnEvent(new(kind, 0, screen.X, screen.Y, data, t += (uint)delay, 0));
            }
            Event(HookKind.MouseMove, 20, 20);
            Event(HookKind.Wheel, 20, 20, 120); Event(HookKind.HWheel, 20, 20, -120);
            Event(HookKind.ButtonDown, 20, 20, 1); Event(HookKind.ButtonUp, 20, 20, 1, 300);
            Event(HookKind.ButtonDown, 20, 20, 1); Event(HookKind.ButtonUp, 70, 40, 1, 40);
            Event(HookKind.ButtonDown, 30, 82, 1); Event(HookKind.ButtonUp, 30, 82, 1);
            target.Location = new(at.X - 80, at.Y - 30); cover.Location = target.Location; cover.Show(); Application.DoEvents();
            Require(WindowLocator.WindowAt(cover.Left + 30, cover.Top + 20) == cover.Handle, "Witness is not covering target");
            recorder.Finish(); var doc = FlowDocument.Load(path);
            Require(doc.Steps.All(s => s.Target?.Title == target.Text && (s.At ?? s.From)?.Space == "client"), "Wrong recorded target or space");
            Require(doc.Steps.Single(s => s.Type == "drag").To is { X:70, Y:40, Space:"client" }, "Drag lost event-time endpoint");
            using (var shot = new Bitmap(Path.Combine(dir, doc.Steps.Single(s => s.Type == "click").Shot!)))
                Require(shot.GetPixel(shot.Width - 3, shot.Height - 3).ToArgb() == target.BackColor.ToArgb(), "Recorded screenshot captured the covering window");
            string Play(bool dry, bool expectSuccess = true)
            {
                var si = new ProcessStartInfo(cli) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                si.ArgumentList.Add("run"); si.ArgumentList.Add(path); if (dry) si.ArgumentList.Add("--dry-run");
                using var p = Process.Start(si)!; var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
                var clock = Stopwatch.StartNew();
                while (!p.HasExited) { Application.DoEvents(); Thread.Sleep(5); if (clock.ElapsedMilliseconds > 20000) { p.Kill(); throw new TimeoutException(); } }
                Application.DoEvents(); Require((p.ExitCode == 0) == expectSuccess, $"Unexpected exit {p.ExitCode}: {stdout.Result}{stderr.Result}"); return stdout.Result;
            }
            target.AllowActivationDuringReplay(); cover.AllowActivationDuringReplay();
            Require(W.GetForegroundWindow() == front && front != target.Handle && front != cover.Handle, "No stable unrelated foreground before playback");
            var baseline = (target.Activations, cover.Activations);
            target.Pad.Received.Clear(); cover.Pad.Received.Clear(); Play(true);
            Require(target.Pad.Received.Count == 0 && target.Clicks == 0, "Dry run emitted mouse input");
            Play(false);
            var msgs = target.Pad.Received;
            Require(target.Clicks == 1 && cover.Clicks == 0 && cover.Pad.Received.Count == 0, "Click/target isolation failed");
            Require(msgs.Count(m => m.Msg == 0x201) == 2 && msgs.Count(m => m.Msg == 0x202) == 2 &&
                msgs.Contains((0x202, 60 | 30 << 16)) && msgs.Any(m => m.Msg == 0x20A) && msgs.Any(m => m.Msg == 0x20E) && msgs.Count(m => m.Msg == 0x200) >= 20,
                "Unexpected drag/hold/move/wheel sequence: " + string.Join(",", msgs));
            int count = msgs.Count;
            target.Text += " renamed";
            Play(false, expectSuccess: false);
            Require(msgs.Count == count && cover.Pad.Received.Count == 0 && target.Clicks == 1 && cover.Clicks == 0, "Stale selector delivered input");
            var selector = doc.Steps[0].Target!; selector.Title = target.Text;
            var button = target.Button.Bounds;
            new FlowDocument { Steps = [new FlowStep { Type = "click-text", Target = selector, Text = "APPLY", TimeoutMs = 2000,
                Region = new FlowRegion { X = button.X, Y = button.Y, Width = button.Width, Height = button.Height } }] }.Save(path);
            Play(false);
            Require(target.Clicks == 2 && cover.Clicks == 0 && cover.Pad.Received.Count == 0, "Covered OCR click did not reach the target");
            Require(W.GetForegroundWindow() == front && cursor == Cursor.Position && (target.Activations, cover.Activations) == baseline, $"Foreground unchanged={W.GetForegroundWindow()==front}, cursor unchanged={cursor==Cursor.Position}, activations {baseline} -> {(target.Activations, cover.Activations)}");
            Console.WriteLine($"PASS {doc.Steps.Count} recorded client steps, covered screenshot and OCR click; stale selector refused; foreground and cursor unchanged; target clicks={target.Clicks}, witness=0");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
