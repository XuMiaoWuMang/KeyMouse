using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyMouse;

/// <summary>What the human selected, in screen pixels.</summary>
internal readonly record struct PickedSelection(int X, int Y, int Width, int Height, bool WholeClientArea);

/// <summary>
/// The full-screen overlay behind `region pick` and `probe --pick-region`.
///
/// Three decisions worth keeping:
///
/// * The screen is captured **once**, before the window appears, and that frozen frame is what the
///   user aims at. Live screen content under a translucent overlay is a moving target; a frozen
///   frame cannot move between aiming and reading.
/// * Hover resolves candidates from one enumeration taken when the overlay opens, and skips the
///   overlay's own handle. Enumerating ~400 top-level windows on every mouse move would cost about
///   as much as the OCR it is trying to help with.
/// * Clicking selects a whole client area, dragging selects a rectangle. Both end up as screen
///   pixels; turning those into a window-relative region is <see cref="RegionCommand"/>'s job, so
///   the rule lives in exactly one place.
/// </summary>
internal static class RegionPicker
{
    /// <summary>Shows the overlay. Returns null when the user cancels (ESC or right click).</summary>
    internal static PickedSelection? Pick()
    {
        // The coordinates this overlay produces have to be the physical pixels that SendInput and
        // probe speak, and for that the thread drawing it must be per-monitor-V2 aware. The
        // manifest says so for the process, but WinForms applies its own default to the UI thread:
        // measured, the desktop it saw was 2560x1440 (the scaled logical size) instead of 5120x1532.
        // So the context is set explicitly, re-asserted after WinForms initialises, and then
        // *verified* - because a virtualised desktop would silently produce coordinates that are
        // off by the scale factor.
        SetThreadDpiAwarenessContext(PerMonitorV2);
        Application.EnableVisualStyles();
        SetThreadDpiAwarenessContext(PerMonitorV2);

        int awareness = GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext());
        if (awareness != AwarenessPerMonitor)
            throw new CommandFailure(4, $"选区浮层所在的线程不是 Per-Monitor V2（awareness={awareness}）——" +
                                        $"宁可失败也不给出被系统缩放过的坐标");

        var virtualScreen = SystemInformation.VirtualScreen;
        var frame = NativeCapture.TryCaptureScreen(virtualScreen.X, virtualScreen.Y, virtualScreen.Width, virtualScreen.Height)
            ?? throw new CommandFailure(4, "屏幕抓不下来——没有可交互的桌面？（锁屏或远程会话里选不了区）");

        using var overlay = new Overlay(virtualScreen, frame);
        var picked = overlay.ShowDialog() == DialogResult.OK ? overlay.Result : null;
        if (picked is null && overlay.Failure is { } failure) throw new CommandFailure(4, failure);
        return picked;
    }

    private const int AwarenessPerMonitor = 2;
    private static readonly IntPtr PerMonitorV2 = new(-4); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const uint VkEscape = 0x1B;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr handle, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    private sealed class Overlay : Form
    {
        private readonly Rectangle _virtualScreen;
        private readonly Bitmap _snapshot;
        private readonly Bitmap _dimmed;
        private readonly List<WindowInfo> _windows;
        private readonly Font _labelFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;

        private Point _start;
        private Point _current;
        private bool _dragging;
        private bool _hotkeyRegistered;
        private WindowInfo? _hover;

        internal PickedSelection? Result { get; private set; }

        /// <summary>Why the overlay refused to hand back coordinates, when it did.</summary>
        internal string? Failure { get; private set; }

        internal Overlay(Rectangle virtualScreen, NativeCapture.Frame frame)
        {
            _virtualScreen = virtualScreen;
            _snapshot = ToBitmap(frame);
            _dimmed = Dimmed(_snapshot);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = virtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            Cursor = Cursors.Cross;
            Text = "KeyMouse 选区";

            // Reading Handle here creates the window, so it can be recognised - and skipped - below.
            IntPtr own = Handle;
            _windows = WindowLocator.EnumerateTopLevel()
                .Where(w => w.Handle != own && w.Visible && !w.Minimized && !w.Cloaked)
                .ToList();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // TopMost set before the handle existed was not enough - measured, the window ended up
            // *below* the window it was supposed to cover, so the drag went to that window instead.
            SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            Activate();

            // ESC as a global hotkey: a process started in the background is refused the foreground,
            // and a cancel key that only works when focus happens to land right is not a cancel key.
            _hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, 0, VkEscape);

            // The overlay is a 1:1 map of the frozen frame. If the window is not that size, the
            // pixels under the cursor are not the pixels in the snapshot, and any rectangle taken
            // from it would be a lie - so fail closed instead of reporting wrong coordinates.
            if (ClientSize.Width != _snapshot.Width || ClientSize.Height != _snapshot.Height)
            {
                Failure = $"选区浮层与屏幕不是 1:1（浮层 {ClientSize.Width}x{ClientSize.Height}，" +
                          $"屏幕 {_snapshot.Width}x{_snapshot.Height}）——没有产生任何坐标";
                Cancel();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_dimmed, 0, 0);

            Rectangle highlight = _dragging ? Normalize(_start, _current) : HoverRect();
            if (!highlight.IsEmpty)
            {
                // Undim the area under consideration, so what will be read is shown at full contrast.
                g.DrawImage(_snapshot, highlight, highlight, GraphicsUnit.Pixel);
                using var border = new Pen(_dragging ? Color.FromArgb(0, 120, 215) : Color.FromArgb(255, 176, 0), 2);
                g.DrawRectangle(border, highlight.X, highlight.Y, highlight.Width, highlight.Height);

                string label = _dragging
                    ? $"{highlight.Width} × {highlight.Height}"
                    : _hover is null ? "" : $"{(long)_hover.Handle:X8}  {_hover.Title}  ← 单击选整个客户区";
                if (label.Length > 0) DrawLabel(g, highlight, label);
            }

            DrawHint(g);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { Cancel(); return; }
            if (e.Button != MouseButtons.Left) return;

            _dragging = true;
            _start = e.Location;
            _current = e.Location;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                _current = e.Location;
                Invalidate();
                return;
            }

            var hover = WindowUnder(e.Location);
            if (!ReferenceEquals(hover, _hover))
            {
                _hover = hover;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragging) return;

            _dragging = false;
            Capture = false;

            Rectangle dragged = Normalize(_start, e.Location);
            if (dragged.Width >= 3 && dragged.Height >= 3)
            {
                Confirm(dragged, wholeClientArea: false);
                return;
            }

            // A click, not a drag: the whole client area of the window under the pointer.
            var window = WindowUnder(e.Location);
            if (window is null) { Invalidate(); return; }
            Confirm(ClientRectLocal(window), wholeClientArea: true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Cancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) { Cancel(); return; }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_hotkeyRegistered) UnregisterHotKey(Handle, HotkeyId);
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _snapshot.Dispose();
                _dimmed.Dispose();
                _labelFont.Dispose();
            }
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------ geometry

        private Rectangle HoverRect() => _hover is null ? Rectangle.Empty : ClientRectLocal(_hover);

        private Rectangle ClientRectLocal(WindowInfo window)
        {
            NativeWindow.GetWindowRect(window.Handle, out NativeWindow.RECT raw);
            var windowRect = new ScreenRect(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top);
            ScreenRect client = RegionCommand.ClientBounds(window.Handle, windowRect);
            return new Rectangle(
                client.X - _virtualScreen.X, client.Y - _virtualScreen.Y, client.Width, client.Height);
        }

        private WindowInfo? WindowUnder(Point local)
        {
            Point screen = PointToScreen(local);
            foreach (var window in _windows)
            {
                var rect = window.Rect;
                if (screen.X >= rect.Left && screen.X < rect.Right &&
                    screen.Y >= rect.Top && screen.Y < rect.Bottom)
                {
                    return window;
                }
            }
            return null;
        }

        private static Rectangle Normalize(Point a, Point b) => Rectangle.FromLTRB(
            Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

        // ------------------------------------------------------------------ finishing

        private void Confirm(Rectangle local, bool wholeClientArea)
        {
            Point topLeft = PointToScreen(new Point(local.X, local.Y));
            Result = new PickedSelection(topLeft.X, topLeft.Y, local.Width, local.Height, wholeClientArea);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel()
        {
            Result = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ------------------------------------------------------------------ drawing

        private void DrawLabel(Graphics g, Rectangle anchor, string text)
        {
            SizeF size = g.MeasureString(text, _labelFont);
            int width = (int)size.Width + 12;
            int height = (int)size.Height + 6;
            int x = Math.Min(Math.Max(anchor.X, 0), Math.Max(0, ClientSize.Width - width));
            int y = anchor.Y - height - 2;
            if (y < 0) y = Math.Min(anchor.Bottom + 2, ClientSize.Height - height);

            using var background = new SolidBrush(Color.FromArgb(230, 20, 20, 20));
            using var foreground = new SolidBrush(Color.White);
            g.FillRectangle(background, x, y, width, height);
            g.DrawString(text, _labelFont, foreground, x + 6, y + 3);
        }

        private void DrawHint(Graphics g)
        {
            const string hint = "拖动框选区域　·　单击 = 选中该窗口的整个客户区　·　ESC 取消";
            SizeF size = g.MeasureString(hint, _labelFont);
            int width = (int)size.Width + 20;
            int height = (int)size.Height + 10;
            int x = Math.Max(0, (ClientSize.Width - width) / 2);
            const int y = 12;

            using var background = new SolidBrush(Color.FromArgb(220, 20, 20, 20));
            using var foreground = new SolidBrush(Color.White);
            g.FillRectangle(background, x, y, width, height);
            g.DrawString(hint, _labelFont, foreground, x + 10, y + 5);
        }

        private static Bitmap Dimmed(Bitmap snapshot)
        {
            var dimmed = new Bitmap(snapshot.Width, snapshot.Height, PixelFormat.Format32bppRgb);
            using var g = Graphics.FromImage(dimmed);
            g.DrawImageUnscaled(snapshot, 0, 0);
            using var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(dim, 0, 0, dimmed.Width, dimmed.Height);
            return dimmed;
        }

        private static Bitmap ToBitmap(NativeCapture.Frame frame)
        {
            var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppRgb);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            try
            {
                for (int row = 0; row < frame.Height; row++)
                    Marshal.Copy(frame.Bgra, row * frame.Width * 4, IntPtr.Add(data.Scan0, row * data.Stride), frame.Width * 4);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}
