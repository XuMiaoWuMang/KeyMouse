using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyMouse;

/// <summary>
/// `record` - watches the keyboard and mouse and writes the session out as a replayable JSON flow.
///
/// Three decisions that shape everything else:
///
/// * **Coordinates are recorded window-relative** wherever they can be (client area), because a
///   screen coordinate stops meaning the same thing the moment a window moves - the same rule the
///   rest of the tool follows. Points that belong to no client area (a title bar, the desktop) are
///   recorded as screen coordinates and flagged in the note.
/// * **Targets are recorded as process + class**, not as a window handle: handles are gone by the
///   next run, and titles change with the document.
/// * **Mouse movement is recorded by default but thinned**: every pixel of a 3-second sweep is
///   noise, while a hover that opens a submenu is not. Points closer than the threshold to the last
///   kept one are dropped; the threshold is in the file so a human can see what was thrown away.
/// </summary>
internal static class Recorder
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkEscape = 0x1B;
    private const int ShotWidth = 320;
    private const int ShotHeight = 200;

    internal static int Run(string[] args, Program.GlobalOptions g)
    {
        var (positional, options) = Program.Parse(
            args, "out", "min-gap", "move-threshold", "duration", "shot-size");
        if (positional.Count > 0)
            throw new ArgumentException($"record 不接受位置参数 '{positional[0]}'");
        if (g.HasSelector)
            throw new ArgumentException("record 不接受窗口选择器：它记录的是你实际操作过的窗口");

        string output = options.TryGetValue("out", out string? rawOut) && rawOut.Length > 0 ? rawOut : "flow.json";
        int minGap = Math.Clamp(Program.IntOr(options, "min-gap", 300), 0, 60_000);
        int moveThreshold = Math.Clamp(Program.IntOr(options, "move-threshold", 8), 1, 500);
        int durationMs = Math.Clamp(Program.IntOr(options, "duration", 0), 0, 3_600_000);
        bool shots = !options.ContainsKey("no-shots");

        var session = new Session(output, minGap, moveThreshold, shots, durationMs);

        Console.WriteLine($"● 录制中——操作你要录的窗口（Ctrl+Alt+Q 停止，连按两次 ESC 也停" +
                          (durationMs > 0 ? $"，{durationMs / 1000.0:0.#} 秒后自动停" : "") + "）");
        Console.WriteLine("  鼠标轨迹会一起录（--no-shots 关截图，--move-threshold N 调抽稀）");
        NativeHooks.Record(session.OnEvent, session.ShouldStop);
        session.Finish();

        Console.WriteLine($"■ 已停止：{session.Steps.Count} 步 → {Path.GetFullPath(output)}");
        if (session.ShotCount > 0) Console.WriteLine($"  截图 {session.ShotCount} 张 → {session.ShotDirectory}");
        Console.WriteLine("  回放：KeyMouse run " + Path.GetFileName(output) + " [--dry-run]");
        return 0;
    }

    /// <summary>State for one recording session; the hook callback feeds it events in order.</summary>
    private sealed class Session
    {
        private readonly int _minGapMs;
        private readonly int _moveThreshold;
        private readonly bool _shots;
        private readonly int _durationMs;
        private readonly uint _startedAt;
        private readonly List<(HookEvent Event, string Type, string? Detail)> _raw = new();
        private readonly HashSet<int> _held = new();
        private readonly StringBuilder _typing = new();
        private uint _typingStartedAt;
        private uint _typingLastAt;
        private int _shotIndex;
        private uint _lastEventTime;
        private uint _lastEscapeAt;
        private uint _escapeCount;
        private bool _stopped;

        internal Session(string output, int minGapMs, int moveThreshold, bool shots, int durationMs)
        {
            OutputPath = output;
            _minGapMs = minGapMs;
            _moveThreshold = moveThreshold;
            _shots = shots;
            _durationMs = durationMs;
            _startedAt = Now();
            _lastEventTime = _startedAt;
            ShotDirectory = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".",
                Path.GetFileNameWithoutExtension(output) + ".shots");
        }

        internal string OutputPath { get; }
        internal string ShotDirectory { get; }
        internal int ShotCount { get; private set; }
        internal List<FlowStep> Steps { get; } = new();

        private static uint Now() => (uint)Environment.TickCount64;

        internal bool ShouldStop()
        {
            if (_stopped) return true;
            if (_durationMs > 0 && Now() - _startedAt >= _durationMs)
            {
                _stopped = true;
                return true;
            }
            return false;
        }

        /// <summary>Called inside the hook chain for every input event: keep it cheap.</summary>
        internal void OnEvent(HookEvent e)
        {
            // The stop combo must not be recorded as part of the flow.
            bool stopCombo = e.Kind == HookKind.KeyDown && e.VirtualKey == 0x51 /* Q */ &&
                             _held.Contains(VkControl) && _held.Contains(VkMenu);
            if (stopCombo)
            {
                _stopped = true;
                return;
            }

            switch (e.Kind)
            {
                case HookKind.KeyDown:
                    if (e.VirtualKey == VkEscape)
                    {
                        _escapeCount = Now() - _lastEscapeAt < 1000 ? _escapeCount + 1 : 1;
                        _lastEscapeAt = Now();
                        if (_escapeCount >= 2) { _stopped = true; return; }
                    }
                    OnKeyDown(e);
                    break;
                case HookKind.KeyUp:
                    if (IsModifier(e.VirtualKey)) _held.Remove(e.VirtualKey);
                    break;
                case HookKind.MouseMove:
                    FlushTyping();
                    _raw.Add((e, "move", null));
                    break;
                case HookKind.ButtonDown:
                    FlushTyping();
                    _raw.Add((e, "down", ButtonName(e.Data)));
                    break;
                case HookKind.ButtonUp:
                    _raw.Add((e, "up", ButtonName(e.Data)));
                    break;
                case HookKind.Wheel:
                    FlushTyping();
                    _raw.Add((e, "wheel", null));
                    break;
            }

            _lastEventTime = e.Time;
        }

        private void OnKeyDown(HookEvent e)
        {
            if (IsModifier(e.VirtualKey))
            {
                _held.Add(e.VirtualKey);
                return;
            }

            // Unicode injection (KeyMouse's own `key type`, and some IMEs) arrives as VK_PACKET with
            // the character in the scan-code field. Recording it as text keeps a session that mixed a
            // human and a KeyMouse command honest - and makes the recorder testable by driving it
            // with KeyMouse itself.
            if (e.VirtualKey == 0xE7)
            {
                char packet = (char)e.Data;
                if (!char.IsControl(packet))
                {
                    if (_typing.Length == 0) _typingStartedAt = e.Time;
                    _typingLastAt = e.Time;
                    _typing.Append(packet);
                }
                return;
            }

            char? character = Character(e.VirtualKey, e.Data);
            bool withCommandModifier = _held.Contains(VkControl) || _held.Contains(VkMenu) ||
                                       _held.Contains(VkLWin) || _held.Contains(VkRWin);
            if (character is { } c && !withCommandModifier)
            {
                if (_typing.Length == 0) _typingStartedAt = e.Time;
                _typingLastAt = e.Time;
                _typing.Append(c);
                return;
            }

            FlushTyping();
            _raw.Add((e, "key", KeyExpression(e.VirtualKey)));
        }

        private string KeyExpression(int vk)
        {
            var parts = new List<string>();
            if (_held.Contains(VkControl)) parts.Add("ctrl");
            if (_held.Contains(VkShift)) parts.Add("shift");
            if (_held.Contains(VkMenu)) parts.Add("alt");
            if (_held.Contains(VkLWin)) parts.Add("win");
            string? name = KeyMap.NameOf(vk) ?? $"vk:{vk:X2}";
            parts.Add(name);
            return string.Join("+", parts);
        }

        private static bool IsModifier(int vk) =>
            vk is VkShift or VkControl or VkMenu or VkLWin or VkRWin;

        private static string ButtonName(int button) => button switch
        {
            2 => "right",
            3 => "middle",
            4 => "x1",
            _ => "left",
        };

        /// <summary>
        /// The character this key produces under the current layout and modifier state. Recording
        /// characters instead of key codes is what makes a flow survive a different keyboard layout.
        /// </summary>
        private static char? Character(int vk, int scanCode)
        {
            var state = new byte[256];
            if (!NativeHooks.GetKeyboardState(state)) return null;
            var buffer = new char[4];
            int written = NativeHooks.ToUnicodeEx((uint)vk, (uint)scanCode, state, buffer, buffer.Length, 0,
                NativeHooks.GetKeyboardLayout(0));
            if (written != 1) return null;
            char c = buffer[0];
            return char.IsControl(c) ? null : c;
        }

        private void FlushTyping()
        {
            if (_typing.Length == 0) return;
            string text = _typing.ToString();
            _typing.Clear();

            // The typing pace the human actually used, so replaying a flow feels like the recording
            // instead of blasting every character at the default speed. Carried in Data of the
            // synthetic event: the tuple shape is (event, type, detail) and this is per-step numeric.
            int interval = _typing.Length <= 1 || _typingLastAt <= _typingStartedAt
                ? 15
                : Math.Max(1, (int)((_typingLastAt - _typingStartedAt) / (text.Length - 1)));
            _raw.Add((new HookEvent(HookKind.KeyDown, 0, 0, 0, interval, _typingStartedAt, IntPtr.Zero), "type", text));
        }

        /// <summary>Turns the raw event stream into steps: pairs button gestures, thins the trajectory,
        /// inserts the pauses the user actually left, and resolves the window each step belongs to.</summary>
        internal void Finish()
        {
            FlushTyping();

            var trajectory = new List<HookEvent>();
            var wheelBurst = new List<HookEvent>();
            (HookEvent Event, string Button)? pressed = null;
            int trajectoryAtPress = 0;
            var lastStepTime = _startedAt;

            void Emit(FlowStep step, HookEvent at)
            {
                if (_minGapMs > 0 && Steps.Count > 0)
                {
                    long gap = (long)at.Time - lastStepTime;
                    if (gap >= _minGapMs)
                    {
                        Steps.Add(new FlowStep { Type = "sleep", Ms = (int)Math.Min(gap, int.MaxValue) });
                    }
                }
                Steps.Add(step);
                lastStepTime = at.Time;
            }

            // Moves are emitted in place, thinned, right before whatever step follows them: the hover
            // that opens a submenu is part of the flow, three seconds of sweeping is not.
            void FlushTrajectory()
            {
                if (trajectory.Count == 0) return;
                var points = trajectory.Select(t => new Point(t.X, t.Y)).ToList();
                foreach (int index in ThinTrajectoryIndices(points, _moveThreshold))
                {
                    var e = trajectory[index];
                    Emit(Describe(e, "move", null, new Point(e.X, e.Y), null, null, null), e);
                }
                trajectory.Clear();
            }

            void FlushWheel()
            {
                if (wheelBurst.Count == 0) return;
                var first = wheelBurst[0];
                int delta = wheelBurst.Sum(w => w.Data);
                var step = Describe(first, "wheel", null, new Point(first.X, first.Y), null, null, delta);
                Emit(step, wheelBurst[^1]);
                wheelBurst.Clear();
            }

            foreach (var (e, kind, detail) in _raw)
            {
                // Moves are flushed before whatever step follows them - except on button release:
                // the path of a drag belongs to the drag step, and flushing there would emit it twice
                // (measured: a 260 px drag produced 21 stray move steps before this rule).
                if (kind is "key" or "type" or "wheel" or "down") FlushTrajectory();
                switch (kind)
                {
                    case "move":
                        trajectory.Add(e);
                        continue;

                    case "down":
                        FlushWheel();
                        pressed = (e, detail!);
                        trajectoryAtPress = trajectory.Count;
                        continue;

                    case "up":
                        FlushWheel();
                        if (pressed is not { } downEvent) continue;
                        pressed = null;
                        var from = new Point(downEvent.Event.X, downEvent.Event.Y);
                        var to = new Point(e.X, e.Y);
                        if (IsDrag(from, to, _moveThreshold))
                        {
                            // The path of the drag belongs to the drag step, not to extra move steps.
                            if (trajectory.Count > trajectoryAtPress)
                                trajectory.RemoveRange(trajectoryAtPress, trajectory.Count - trajectoryAtPress);
                            Emit(Describe(e, "drag", null, from, to, (int)(e.Time - downEvent.Event.Time), null), e);
                        }
                        else
                        {
                            Emit(Describe(downEvent.Event, "click", downEvent.Button, from, null, null, null), downEvent.Event);
                        }
                        continue;

                    case "wheel":
                        if (wheelBurst.Count > 0 && e.Time - wheelBurst[^1].Time > 200) FlushWheel();
                        wheelBurst.Add(e);
                        continue;

                    case "key":
                        FlushWheel();
                        Emit(Describe(e, "key", detail, null, null, null, null), e);
                        continue;

                    case "type":
                        FlushWheel();
                        Emit(Describe(e, "type", detail, null, null, null, null), e);
                        continue;
                }
            }

            FlushWheel();
            FlushTrajectory();

            var document = new FlowDocument
            {
                RecordedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                Screen = new FlowScreen { Width = VirtualWidth(), Height = VirtualHeight() },
                Options = new FlowOptions
                {
                    MinGapMs = _minGapMs,
                    MoveThresholdPx = _moveThreshold,
                    Shots = _shots,
                },
                Steps = Steps,
            };
            document.Save(OutputPath);
        }

        /// <summary>Builds one step: resolves the window, converts the point, and takes the shot.</summary>
        private FlowStep Describe(HookEvent e, string type, string? detail, Point? point, Point? to, int? duration, int? wheelDelta)
        {
            var step = new FlowStep { Type = type };
            IntPtr window = type is "click" or "drag" or "wheel" or "move"
                ? WindowLocator.WindowAt(point?.X ?? e.X, point?.Y ?? e.Y)
                : NativeWindow.GetForegroundWindow();

            if (window != IntPtr.Zero)
            {
                WindowInfo info = WindowInfo.Capture(window);
                step.Target = new FlowTarget
                {
                    Process = info.ProcessName,
                    Class = info.ClassName,
                    Title = info.Title.Length > 0 ? info.Title : null,
                };
            }

            Point? reference = point;
            if (type is "click" or "wheel" or "move" or "drag")
            {
                step.At = ToPoint(window, point ?? new Point(e.X, e.Y), out string? note);
                if (note is not null) step.Note = note;
            }
            else
            {
                // Keys and typing belong to the focused window; the pointer position is only used to
                // aim the screenshot, because that is where the human was looking.
                Point pointer = NativeHooks.GetCursorPosition();
                step.At = null;
                reference = pointer;
                if (window != IntPtr.Zero && !WindowLocator.IsInsideClientArea(window, pointer.X, pointer.Y))
                    step.Note = "键盘事件：截图取自本次操作时的鼠标位置";
            }

            if (type == "drag" && to is { } destination)
            {
                step.From = step.At;
                step.To = ToPoint(window, destination, out _);
                step.DurationMs = duration;
            }
            if (type == "wheel") step.Delta = wheelDelta;
            if (type == "type")
            {
                step.Text = detail;
                step.IntervalMs = e.Data > 0 ? e.Data : 15; // measured pace, see FlushTyping
            }
            if (type == "key")
            {
                step.Combo = detail?.Contains('+') == true ? detail : null;
                if (step.Combo is null) step.Text = detail;
            }

            if (_shots && reference is { } shotAt && type is "click" or "drag" or "wheel" or "type" or "key")
            {
                step.Shot = TakeShot(shotAt, type);
            }

            return step;
        }

        private FlowPoint ToPoint(IntPtr window, Point screen, out string? note)
        {
            note = null;
            if (window != IntPtr.Zero && NativeWindow.GetClientRect(window, out NativeWindow.RECT client))
            {
                var origin = new NativeWindow.POINT { X = 0, Y = 0 };
                if (NativeWindow.ClientToScreen(window, ref origin))
                {
                    int width = client.Right - client.Left;
                    int height = client.Bottom - client.Top;
                    if (screen.X >= origin.X && screen.Y >= origin.Y &&
                        screen.X < origin.X + width && screen.Y < origin.Y + height)
                    {
                        return new FlowPoint { Space = FlowSpace.Client, X = screen.X - origin.X, Y = screen.Y - origin.Y };
                    }
                }
            }
            note ??= "这一步在客户区之外（标题栏/桌面）：记的是屏幕坐标，窗口移动后需要重新指位置";
            return new FlowPoint { Space = FlowSpace.Screen, X = screen.X, Y = screen.Y };
        }

        private string? TakeShot(Point center, string type)
        {
            Directory.CreateDirectory(ShotDirectory);
            int x = Math.Max(0, center.X - ShotWidth / 2);
            int y = Math.Max(0, center.Y - ShotHeight / 2);
            var frame = NativeCapture.TryCaptureScreen(x, y, ShotWidth, ShotHeight);
            if (frame is null) return null;

            string name = $"{++_shotIndex:d4}-{type}.png";
            string png = Path.Combine(ShotDirectory, name);
            try
            {
                using var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppRgb);
                var data = bitmap.LockBits(new Rectangle(0, 0, frame.Width, frame.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try
                {
                    for (int row = 0; row < frame.Height; row++)
                    {
                        Marshal.Copy(frame.Bgra, row * frame.Width * 4, IntPtr.Add(data.Scan0, row * data.Stride),
                            frame.Width * 4);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
                bitmap.Save(png, ImageFormat.Png);
            }
            catch (Exception)
            {
                return null; // a missing screenshot must not lose the recording
            }

            ShotCount++;
            string file = Path.GetFileName(png);
            return Path.Combine(Path.GetFileName(ShotDirectory), file).Replace('\\', '/');
        }

        private static int VirtualWidth() =>
            NativeHooks.GetSystemMetrics(NativeHooks.SmXVirtualScreen) +
            NativeHooks.GetSystemMetrics(NativeHooks.SmCxVirtualScreen);

        private static int VirtualHeight() =>
            NativeHooks.GetSystemMetrics(NativeHooks.SmYVirtualScreen) +
            NativeHooks.GetSystemMetrics(NativeHooks.SmCyVirtualScreen);
    }

    /// <summary>
    /// Indices of the points worth keeping: the first one, then every point at least
    /// <paramref name="threshold"/> pixels away from the last kept one. Pure, so the thinning rule
    /// can be tested without a desktop.
    /// </summary>
    internal static List<int> ThinTrajectoryIndices(IReadOnlyList<Point> points, int threshold)
    {
        var kept = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (kept.Count == 0 || Distance(points[kept[^1]], points[i]) >= threshold) kept.Add(i);
        }
        return kept;
    }

    /// <summary>True when the gesture travelled far enough to be a drag rather than a click.</summary>
    internal static bool IsDrag(Point down, Point up, int threshold) => Distance(down, up) > threshold;

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
}
