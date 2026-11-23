using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyMouse.Flow;
/// <summary>
/// `record` - watches the keyboard and mouse and writes the session out as a replayable JSON flow.
///
/// Three decisions that shape everything else:
///
/// * The CLI defaults to screen positions. With --client-points (used by the editor), bind each
///   eligible mouse event to its client coordinates before the window can move or be covered.
/// * **Only actual input becomes steps**: recording never prepends an automatic focus operation.
///   A flow that needs to activate a window can include an explicit focus step.
/// * **Mouse movement is recorded by default but thinned**: every pixel of a 3-second sweep is
///   noise, while a hover that opens a submenu is not. Points closer than the threshold to the last
///   kept one are dropped; the threshold is in the file so a human can see what was thrown away.
/// </summary>
internal static class Recorder
{
    internal enum Mode { Full, Key }

    /// <summary>
    /// `--ignore-process` 的取值检查：去掉首尾空白、去掉可能带的 `.exe`，空值直接报错。
    ///
    /// 抽成单独的方法是为了能测——[`Run`] 会挂全局钩子，单测里跑不起来。
    /// 引擎比的是 [`NativeWindow.ProcessNameOf`]，那边给的是**不带扩展名**的名字，
    /// 所以用户写 `KeyMouse.Editor.exe` 也认。
    /// </summary>
    internal static string ParseIgnoreProcess(string raw)
    {
        string name = raw.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        if (name.Length == 0)
            throw new ArgumentException("--ignore-process 需要一个进程名，例如 KeyMouse.Editor");

        return name;
    }

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkEscape = 0x1B;

    // 低级钩子报的是**左右键**（VK_LCONTROL 0xA2 …），不是通用的 VK_CONTROL。
    // 只认 0x11 的话 Ctrl 永远进不了 _held，于是 Ctrl+Alt+Q 按了没反应（实测反馈）。
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;

    internal static int Run(string[] args, CommandLine.GlobalOptions g)
    {
        var (positional, options) = CommandLine.Parse(
            args, "out", "mode", "min-gap", "move-threshold", "duration", "shot-size", "ignore-rect", "ignore-window", "ignore-process");
        if (positional.Count > 0)
            throw new ArgumentException($"record 不接受位置参数 '{positional[0]}'");
        if (g.HasSelector)
            throw new ArgumentException("record 不接受窗口选择器：它记录的是你实际操作过的窗口");

        string output = options.TryGetValue("out", out string? rawOut) && rawOut.Length > 0 ? rawOut : "flow.json";
        int minGap = Math.Clamp(CommandLine.IntOr(options, "min-gap", 300), 0, 60_000);
        int moveThreshold = Math.Clamp(CommandLine.IntOr(options, "move-threshold", 8), 1, 500);
        int durationMs = Math.Clamp(CommandLine.IntOr(options, "duration", 0), 0, 3_600_000);
        bool shots = !options.ContainsKey("no-shots");

        Mode mode = options.GetValueOrDefault("mode", "full").ToLowerInvariant() switch
        {
            "key" => Mode.Key,
            "full" => Mode.Full,
            _ => throw new ArgumentException("--mode 只接受 key（关键录制）或 full（完全录制）"),
        };
        var session = new Session(output, minGap, moveThreshold, shots, durationMs, mode) { ClientPoints = options.ContainsKey("client-points") };

        // --ignore-rect x,y,w,h[;x,y,w,h…]：落在这些矩形里的**鼠标事件一律不记**。
        // 用途：录制时窗口是隐藏的，用户靠一个悬浮的"停止"按钮结束——那一下点击属于界面，
        // 不属于用户要复刻的操作，不该进流程文件（否则回放会去点那个按钮）。
        // --ignore-window <hwnd>：这个窗口上的鼠标事件一律不记。比矩形可靠——窗口可以被拖走。
        if (options.TryGetValue("ignore-window", out string? rawWindow))
        {
            string text = rawWindow.Trim();
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!long.TryParse(hex ? text[2..] : text,
                    hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out long handle) || handle == 0)
            {
                throw new ArgumentException($"--ignore-window 需要一个窗口句柄（十进制或 0x 十六进制），收到 '{rawWindow}'");
            }
            session.IgnoreWindow = (IntPtr)handle;
        }

        // --ignore-process <名字>：这个进程的所有窗口上的鼠标事件一律不记。
        // 比句柄好用：外壳的录制浮条是录制中途才建出来的，录制开始时根本拿不到它的句柄；
        // 而"点停止"这一下就落在浮条上，绝不该被录成一步。
        if (options.TryGetValue("ignore-process", out string? ignoredProcess))
            session.IgnoreProcess = ParseIgnoreProcess(ignoredProcess);

        if (options.TryGetValue("ignore-rect", out string? rawRects))
        {
            foreach (string part in rawRects.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] n = part.Split(',');
                if (n.Length != 4 ||
                    !int.TryParse(n[0], out int rx) || !int.TryParse(n[1], out int ry) ||
                    !int.TryParse(n[2], out int rw) || !int.TryParse(n[3], out int rh))
                {
                    throw new ArgumentException($"--ignore-rect 需要 x,y,宽,高（可用 ; 分隔多个），收到 '{part}'");
                }
                session.IgnoreRects.Add((rx, ry, rw, rh));
            }
        }

        using StopSignal? externalStop = StopSignal.Install();
        session.ExternalStop = externalStop;

        NativeHooks.Record(session.OnEvent, session.ShouldStop,
            () =>
            {
                Execution.RecordingState("recording");
                CommandOutput.Out.WriteLine($"● 录制中——操作你要录的窗口（Ctrl+Alt+Q 停止，" +
                                  $"或 {Session.EscapeWindowMs / 1000.0:0.#} 秒内连按两次 ESC" +
                                  (durationMs > 0 ? $"，{durationMs / 1000.0:0.#} 秒后自动停" : "") + "）");
                CommandOutput.Out.WriteLine(mode == Mode.Key ? "  关键录制：跳过空移，每步执行前默认等待 1000 毫秒" : "  完全录制：记录每步的实际等待和鼠标轨迹（--move-threshold N 调抽稀）");
                CommandOutput.Out.WriteLine("  注意：管理员权限窗口上的操作录不到；要录它们请加 --elevate（或右键以管理员身份运行）");
            });
        session.Finish();

        CommandOutput.Out.WriteLine($"■ 已停止：{session.Steps.Count} 步 → {Path.GetFullPath(output)}");
        // 钩子到底看见了什么：这几行的存在是为了回答"我明明点了，流程里为什么没有"。
        string seen = string.Join("，", NativeHooks.Seen.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key} {pair.Value}"));
        CommandOutput.Out.WriteLine("  钩子收到：" + (seen.Length > 0 ? seen : "（一个事件都没有）"));

        // 权限比我们高的窗口，Windows 不会把发往它的输入交给我们的钩子——
        // 这不是"漏记"，是系统边界。说出来，别让用户对着一份少了点击的流程文件猜。
        if (NativeHooks.OutOfReach.Count > 0)
        {
            CommandOutput.Out.WriteLine("  ⚠ 这些窗口的权限比 KeyMouse 高，落在那里的点击录不到：");
            foreach (var pair in NativeHooks.OutOfReach)
            {
                CommandOutput.Out.WriteLine($"      pid {pair.Key}  {pair.Value}");
            }
            CommandOutput.Out.WriteLine("    要录这些窗口上的操作，请用管理员身份运行：");
            CommandOutput.Out.WriteLine("      KeyMouse --elevate record --out …（自动提权），或右键『以管理员身份运行』");
        }
        if (session.ShotCount > 0) CommandOutput.Out.WriteLine($"  截图 {session.ShotCount} 张 → {session.ShotDirectory}");
        CommandOutput.Out.WriteLine("  回放：KeyMouse run " + Path.GetFileName(output) + " [--dry-run]");
        return 0;
    }

    /// <summary>State for one recording session; the hook callback feeds it events in order.</summary>
    internal sealed class Session
    {
        private static readonly int MaxTypingIntervalMs = FlowSchema.TryGetNumberRule("type", "intervalMs", out var rule) ? rule.Max : int.MaxValue;
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
        /// <summary>
        /// 按下还没确认的那次 ESC。
        /// 为了不让"连按两次 ESC 停止"在流程里留下一个 esc，第一次 ESC 先扣着：
        /// EscapeWindowMs 内来了第二次就一起丢掉并停止；没来第二次就补记（它是一次真实的按键）。
        /// </summary>
        private HookEvent? _pendingEscape;
        private uint _lastEscapeAt;

        /// <summary>
        /// 连按两次 ESC 停止录制：**两次按下必须落在这么近**（毫秒）。
        /// 0.3 秒是"人手双击"的上限：放得太宽（早先是 1 秒），录制中正常按两次 ESC
        /// ——比如在编辑器里连按取消、或者游戏里退菜单——就会被当成停止。
        /// 同一个数也决定"单独一次 ESC 多久没等来第二次就补记进流程"。
        /// </summary>
        internal const uint EscapeWindowMs = 300;
        private bool _stopped;

        private readonly Mode _mode;

        internal Session(string output, int minGapMs, int moveThreshold, bool shots, int durationMs, Mode mode = Mode.Full)
        {
            _mode = mode;
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

        /// <summary>录制起点的 tick；测试用它构造相对时间轴上的事件。</summary>
        internal uint StartedAt => _startedAt;

        /// <summary>这些矩形里的鼠标事件不记（录制时自己的悬浮控件在这上面）。</summary>
        internal List<(int X, int Y, int W, int H)> IgnoreRects { get; } = [];

        /// <summary>这个窗口上的鼠标事件不记（编辑器的悬浮停止按钮）。拖动窗口后依然有效。</summary>
        internal IntPtr IgnoreWindow { get; set; }

        /// <summary>这个进程名（不带 .exe）的窗口上的鼠标事件不记。见 --ignore-process。</summary>
        internal string? IgnoreProcess { get; set; }

        /// <summary>
        /// 外部停止信号：外壳（或任何知道本进程 pid 的东西）把
        /// <c>Local\KeyMouse-record-stop-&lt;pid&gt;</c> 置上，这一轮就收尾。
        ///
        /// 为什么不用"注入 Ctrl+Alt+Q"：那条路要抢到前台窗口、要钩子看得见注入的键，
        /// 而录制浮条自己就是前台 + WebView2 窗口，实测时有时无。命名事件不依赖焦点。
        /// </summary>
        internal StopSignal? ExternalStop { get; set; }

        /// <summary>刚刚吞掉了一次按下：它的抬起也要吞，否则点击配对会错位。</summary>
        private bool _suppressRelease;

        internal string OutputPath { get; }
        internal string ShotDirectory { get; }
        internal int ShotCount { get; private set; }
        internal List<FlowStep> Steps { get; } = new();

        private static uint Now() => (uint)Environment.TickCount64;

        private bool _stoppingReported;

        internal bool ShouldStop()
        {
            if (_stopped)
            {
                // 只报一次：界面把按钮切成"正在停止…"，不必每秒重复。
                if (!_stoppingReported)
                {
                    _stoppingReported = true;
                    Execution.RecordingState("stopping");
                }
                return true;
            }

            // 单按一次 ESC 过了双击窗口还没等到第二次：它就是一次普通按键，补记进去。
            if (_pendingEscape is not null && Now() - _lastEscapeAt >= EscapeWindowMs) FlushPendingEscape();
            // A client attached to the Runner can stop a recording the same way it stops a flow.
            if (Execution.Cancelled)
            {
                _stopped = true;
                return true;
            }
            if (ExternalStop is not null && ExternalStop.IsSet)
            {
                _stopped = true;
                return true;
            }
            if (_durationMs > 0 && Now() - _startedAt >= _durationMs)
            {
                _stopped = true;
                return true;
            }
            return false;
        }

        /// <summary>Called inside the hook chain for every input event: keep it cheap.</summary>
        internal bool ClientPoints { get; init; }
        private (nint Handle, uint Pid, FlowTarget? Target) _lastOwner;

        private HookEvent CaptureClient(HookEvent e)
        {
            var root = WindowLocator.WindowAt(e.X, e.Y);
            var pt = new NativeWindow.POINT { X = e.X, Y = e.Y };
            if (root == 0 || !NativeWindow.ScreenToClient(root, ref pt) || !NativeWindow.GetClientRect(root, out var rect) ||
                pt.X < 0 || pt.Y < 0 || pt.X >= rect.Width || pt.Y >= rect.Height ||
                NativeWindow.GetWindowThreadProcessId(root, out var pid) == 0) return e;
            if (_lastOwner.Handle != root || _lastOwner.Pid != pid || e.Kind != HookKind.MouseMove)
            {
                var cls = NativeWindow.GetClass(root);
                // Desktop shell, menus and tooltips are transient screen surfaces, not replay targets.
                var process = NativeWindow.ProcessNameOf(pid);
                FlowTarget? target = process == "?" || cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or "#32768" or "tooltips_class32" or "" ? null
                    : new FlowTarget { Process = process, Class = cls, Title = NativeWindow.GetTitle(root) };
                _lastOwner = (root, pid, target);
            }
            return e with { Window = root, ClientPoint = new Point(pt.X, pt.Y), ClientTarget = _lastOwner.Target };
        }

        internal void OnEvent(HookEvent e)
        {
            // Drag endpoints come from button events. A discarded move needn't look up a window
            // or split a text run (and must not flush a pending long press).
            if (_mode == Mode.Key && e.Kind == HookKind.MouseMove) return;
            if (SkipIgnored(e)) return;
            if (ClientPoints && e.Kind is HookKind.ButtonDown or HookKind.ButtonUp or HookKind.MouseMove or HookKind.Wheel or HookKind.HWheel)
                e = CaptureClient(e);

            // 停止组合键本身不该进流程。修饰键状态除了 _held（可能因钩子的左右键差异漏记），
            // 再问一次系统：两条路任一成立就算按下（实测 Ctrl+Alt+Q 曾经因为只认 VK_CONTROL 失灵）。
            bool stopCombo = e.Kind == HookKind.KeyDown && e.VirtualKey == 0x51 /* Q */ &&
                             ((_held.Contains(VkControl) && _held.Contains(VkMenu)) ||
                              (IsDown(VkControl) && IsDown(VkMenu)));
            if (stopCombo)
            {
                _pendingEscape = null;
                _stopped = true;
                return;
            }

            // ESC 一律在这里处理完（按下与抬起都算）：
            // 抬起如果不拦下来，它会走下面"其他事件"的分支，把待定的那次立刻刷成一条记录——
            // 于是第二次 ESC 又变成"第一次"，连按两次永远停不下来（实测反馈）。
            if (e.VirtualKey == VkEscape)
            {
                if (e.Kind != HookKind.KeyDown) return;   // 抬起：不记录、不刷待定

                if (_pendingEscape is not null && Now() - _lastEscapeAt < EscapeWindowMs)
                {
                    _pendingEscape = null;   // 连按两次：一次都不记，直接停
                    _stopped = true;
                    return;
                }

                FlushPendingEscape();        // 上一次单按超过时间窗，补记
                _pendingEscape = e;
                _lastEscapeAt = Now();
                return;
            }

            FlushPendingEscape();

            switch (e.Kind)
            {
                case HookKind.KeyDown:
                    OnKeyDown(e);
                    break;
                case HookKind.KeyUp:
                    OnKeyUp(e);
                    _held.Remove(Normalize(e.VirtualKey));
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
                    _raw.Add((e, "wheel", "vertical"));
                    break;
                case HookKind.HWheel:
                    FlushTyping();
                    _raw.Add((e, "wheel", "horizontal"));
                    break;
            }

            _lastEventTime = e.Time;
        }

        private static int Normalize(int vk) => vk switch
        {
            VkLControl or VkRControl => VkControl,
            VkLMenu or VkRMenu => VkMenu,
            VkLShift or VkRShift => VkShift,
            _ => vk,
        };

        /// <summary>
        /// 落在忽略矩形里的鼠标事件直接丢掉；按下被丢时，它的抬起也一并丢，
        /// 免得后面的点击配对错位（配不上就会多出一次点击）。
        /// </summary>
        private bool SkipIgnored(HookEvent e)
        {
            if (IgnoreRects.Count == 0 && IgnoreWindow == IntPtr.Zero && IgnoreProcess is null) return false;

            bool mouse = e.Kind is HookKind.MouseMove or HookKind.ButtonDown or HookKind.ButtonUp
                or HookKind.Wheel or HookKind.HWheel;
            if (!mouse) return false;

            if (IgnoreProcess is not null)
            {
                // 同一进程的所有窗口都跳过：主窗口、录制浮条、以后可能还有别的。
                IntPtr under = NativeHooks.WindowAt(e.X, e.Y);
                if (under != IntPtr.Zero)
                {
                    NativeWindow.GetWindowThreadProcessId(under, out uint ownerPid);
                    if (ownerPid != 0 && NativeWindow.ProcessNameOf(ownerPid) == IgnoreProcess)
                    {
                        if (e.Kind == HookKind.ButtonDown) _suppressRelease = true;
                        return true;
                    }
                }
            }

            if (IgnoreWindow != IntPtr.Zero)
            {
                // 事件发生时问一句"这个点属于那个窗口吗"：窗口被拖到哪儿都认得出。
                if (NativeHooks.WindowAt(e.X, e.Y) == IgnoreWindow)
                {
                    if (e.Kind == HookKind.ButtonDown) _suppressRelease = true;
                    return true;
                }
            }

            if (e.Kind == HookKind.ButtonUp && _suppressRelease)
            {
                _suppressRelease = false;
                return true;
            }

            foreach ((int x, int y, int w, int h) in IgnoreRects)
            {
                if (e.X < x || e.Y < y || e.X >= x + w || e.Y >= y + h) continue;
                if (e.Kind == HookKind.ButtonDown) _suppressRelease = true;
                return true;
            }
            return false;
        }

        private static bool IsDown(int vk) => (NativeHooks.KeyState(vk) & 0x8000) != 0;

        /// <summary>把扣着的那次 ESC 记进流程（它确实被按下了，只是要等确认）。</summary>
        private void FlushPendingEscape()
        {
            if (_pendingEscape is not { } pending) return;
            _pendingEscape = null;
            OnKeyDown(pending);
        }

        /// <summary>按住超过这个时长就记成"按下 + 抬起"两步，而不是一次点击。</summary>
        private const int LongPressMs = 250;

        /// <summary>按着还没抬起的普通键：等抬起才知道是点击还是长按。</summary>
        private (HookEvent Event, string Combo)? _heldKey;

        /// <summary>
        /// 可打印键按着没抬起：等抬起再决定是"打字"还是"长按"。
        /// 判据是**系统连发**——按住 W 会产生连续 KeyDown，打字不会。
        /// </summary>
        private (HookEvent Event, char Char, int Vk, int Repeats)? _pendingChar;

        /// <summary>把挂起的可打印键当普通字符收进正在打的这段字里。</summary>
        private void CommitPendingChar()
        {
            if (_pendingChar is not { } p) return;
            _pendingChar = null;
            AppendCharacter(p.Char, p.Event.Time);
        }

        /// <summary>抬起时决定：短按记一次 key press，长按记 key down + key up（时长由时间线复现）。</summary>
        private void OnKeyUp(HookEvent e)
        {
            int released = Normalize(e.VirtualKey);
            if (_pendingChar is { } pending && pending.Vk == released)
            {
                int pressedFor = (int)(e.Time - pending.Event.Time);
                bool isHold = pending.Repeats > 0 || pressedFor >= LongPressMs;
                _pendingChar = null;
                if (isHold)
                {
                    // 按住不放：一步"长按"，时长记在 Ms 里。
                    FlushTyping();
                    _raw.Add((pending.Event with { Data = pressedFor }, "holdkey", KeyExpression(pending.Vk)));
                }
                else
                {
                    AppendCharacter(pending.Char, pending.Event.Time);
                }
                return;
            }

            if (_heldKey is not { } held) return;
            if (Normalize(held.Event.VirtualKey) != Normalize(e.VirtualKey)) return;

            _heldKey = null;
            int duration = (int)(e.Time - held.Event.Time);
            if (duration >= LongPressMs && _mode == Mode.Key)
            {
                // A shortcut is one action; keyhold only accepts a single key.
                if (held.Combo.Contains('+')) _raw.Add((held.Event, "key", held.Combo));
                else _raw.Add((held.Event with { Data = duration }, "holdkey", held.Combo));
            }
            else if (duration >= LongPressMs)
            {
                _raw.Add((held.Event, "keydown", held.Combo));
                _raw.Add((e, "keyup", held.Combo));
            }
            else
            {
                _raw.Add((held.Event, "key", held.Combo));
            }
        }

        private void OnKeyDown(HookEvent e)
        {
            int vk = Normalize(e.VirtualKey);
            if (IsModifier(vk))
            {
                _held.Add(vk);
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
                    AppendCharacter(packet, e.Time);
                }
                return;
            }

            char? character = Character(e.VirtualKey, e.Data);
            bool withCommandModifier = _held.Contains(VkControl) || _held.Contains(VkMenu) ||
                                       _held.Contains(VkLWin) || _held.Contains(VkRWin);
            if (character is { } c && !withCommandModifier)
            {
                // 先挂起：同样的键再响一次就是**系统连发**（按住不放），不是打字。
                if (_pendingChar is { } waiting && waiting.Vk == vk)
                {
                    _pendingChar = (waiting.Event, waiting.Char, waiting.Vk, waiting.Repeats + 1);
                    return;
                }
                CommitPendingChar();
                _pendingChar = (e, c, vk, 0);
                return;
            }

            FlushTyping();

            // 不立刻记：等抬起时才知道是短按（key press）还是长按（key down + key up）。
            // 系统按住会连发 KeyDown，这里同一键只认第一次。
            if (_heldKey is { } pending && Normalize(pending.Event.VirtualKey) == vk) return;
            _heldKey = (e, KeyExpression(e.VirtualKey));
        }

        private string KeyExpression(int vk)
        {
            var parts = new List<string>();
            if (_held.Contains(VkControl)) parts.Add("ctrl");
            if (_held.Contains(VkShift)) parts.Add("shift");
            if (_held.Contains(VkMenu)) parts.Add("alt");
            if (_held.Contains(VkLWin)) parts.Add("win");
            // 字母/数字键在 KeyMap 里没有名字：直接用字符（不带 Ctrl/Alt/Win 修饰时才有意义，
            // 例如按住 W 前进→ key down w）。
            bool plain = !_held.Contains(VkControl) && !_held.Contains(VkMenu) && !_held.Contains(VkLWin);
            string? name = KeyMap.NameOf(vk);
            if (name is null && plain)
            {
                if (vk is >= 0x41 and <= 0x5A) name = char.ToLowerInvariant((char)vk).ToString();
                else if (vk is >= 0x30 and <= 0x39) name = ((char)vk).ToString();
            }
            name ??= $"vk:{vk:X2}";
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

        private void AppendCharacter(char c, uint time)
        {
            // A longer gap cannot fit the type step’s intervalMs contract. Keep it on the next action.
            if (_mode == Mode.Full && _typing.Length > 0 && unchecked((int)(time - _typingLastAt)) > MaxTypingIntervalMs) FlushText();
            if (_typing.Length == 0) _typingStartedAt = time;
            _typingLastAt = time;
            _typing.Append(c);
        }

        private void FlushTyping()
        {
            CommitPendingChar();
            FlushText();
        }

        private void FlushText()
        {
            if (_typing.Length == 0) return;
            string text = _typing.ToString();
            _typing.Clear();

            // The typing pace the human actually used, so replaying a flow feels like the recording
            // instead of blasting every character at the default speed. Carried in Data of the
            // synthetic event: the tuple shape is (event, type, detail) and this is per-step numeric.
            int interval = text.Length <= 1
                ? 15
                : Math.Max(0, unchecked((int)(_typingLastAt - _typingStartedAt))) / (text.Length - 1);
            _raw.Add((new HookEvent(HookKind.KeyDown, 0, 0, 0, interval, _typingStartedAt, IntPtr.Zero), "type", text));
        }

        /// <summary>Turns raw events into screen-coordinate steps, retaining event timestamps,
        /// pairing button gestures and thinning the trajectory. Only recorded input becomes steps.</summary>
        internal void Finish()
        {
            FlushPendingEscape();
            // 录制结束时还按着的键：记成一次按下（没抬起就没法说时长）。
            if (_heldKey is { } stillDown)
            {
                _raw.Add((stillDown.Event, "key", stillDown.Combo));
                _heldKey = null;
            }
            FlushTyping();

            var trajectory = new List<HookEvent>();
            var wheelBurst = new List<HookEvent>();
            string? wheelAxis = null;   // 这一串滚动是竖向还是横向（换轴就另起一步）
            (HookEvent Event, string Button)? pressed = null;
            int trajectoryAtPress = 0;

            var timed = new List<(FlowStep Step, int Start)>();
            void Emit(FlowStep step, HookEvent at)
            {
                if (_mode == Mode.Full)
                    timed.Add((step, Math.Max(0, unchecked((int)(at.Time - _startedAt)))));
                else
                {
                    step.DelayMs = FlowSchema.DefaultStepDelayMs;
                    Steps.Add(step);
                }
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
                var step = Describe(first, "wheel", wheelAxis, new Point(first.X, first.Y), null, null, delta);
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
                        // 上一次的按下如果没等到抬起（钩子漏了一条 up），不能让它把这一次吞掉：
                        // 先把那一次按"点了"记下来，再交给新的这一对。
                        if (pressed is { } orphan)
                        {
                            Emit(Describe(orphan.Event, "click", orphan.Button,
                                new Point(orphan.Event.X, orphan.Event.Y), null, null, null), orphan.Event);
                        }
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
                            Emit(Describe(downEvent.Event, "drag", downEvent.Button, from, to, (int)(e.Time - downEvent.Event.Time), null, e), downEvent.Event);
                        }
                        else if ((int)(e.Time - downEvent.Event.Time) >= LongPressMs)
                        {
                            // 一次长按 = 一步。落点取按下那一刻的位置。
                            var at = new Point(downEvent.Event.X, downEvent.Event.Y);
                            FlowStep holdStep = Describe(downEvent.Event, "mousehold", downEvent.Button, at, null, null, null, e);
                            holdStep.Ms = (int)(e.Time - downEvent.Event.Time);
                            Emit(holdStep, downEvent.Event);
                        }
                        else
                        {
                            Emit(Describe(downEvent.Event, "click", downEvent.Button, from, null, null, null, e), downEvent.Event);
                        }
                        continue;

                    case "wheel":
                        // 同一次连续滚动才合并。换轴、或者方向反过来，都算新的一步——
                        // 否则"上滚两格再下滚两格"会被加成 delta 0，等于什么都没做。
                        if (wheelBurst.Count > 0)
                        {
                            bool sameAxis = wheelAxis == detail;
                            bool sameSign = Math.Sign(e.Data) == Math.Sign(wheelBurst[^1].Data);
                            bool differentPoint = _mode == Mode.Key && (e.X != wheelBurst[^1].X || e.Y != wheelBurst[^1].Y);
                            if (!sameAxis || !sameSign || differentPoint || e.Window != wheelBurst[^1].Window || (e.ClientTarget is null) != (wheelBurst[^1].ClientTarget is null) || e.Time - wheelBurst[^1].Time > 200) FlushWheel();
                        }
                        wheelAxis = detail;
                        wheelBurst.Add(e);
                        continue;

                    case "key":
                        FlushWheel();
                        Emit(Describe(e, "key", detail, null, null, null, null), e);
                        continue;

                    // 键盘长按：一步，时长写在 Ms 里。
                    case "holdkey":
                    {
                        FlushWheel();
                        FlowStep holdKey = Describe(e, "keyhold", detail, null, null, null, null);
                        holdKey.Ms = e.Data;
                        Emit(holdKey, e);
                        continue;
                    }

                    // 老文件里的两半写法仍然认（按下与抬起各成一步）。
                    case "keydown":
                        FlushWheel();
                        Emit(Describe(e, "keydown", detail, null, null, null, null), e);
                        continue;

                    case "keyup":
                        FlushWheel();
                        Emit(Describe(e, "keyup", detail, null, null, null, null), e);
                        continue;

                    case "type":
                        FlushWheel();
                        Emit(Describe(e, "type", detail, null, null, null, null), e);
                        continue;
                }
            }

            FlushWheel();
            FlushTrajectory();

            // 只在抬起没被送达时留下的一次按下：它几乎肯定是一次点击（谁也不会攥着鼠标录到结束），
            // 不能因为配对失败就整条丢掉。
            if (pressed is { } dangling)
            {
                Emit(Describe(dangling.Event, "click", dangling.Button,
                    new Point(dangling.Event.X, dangling.Event.Y), null, null, null), dangling.Event);
            }

            // A held key is recognized on release, after intervening mouse events may have
            // been buffered. Convert to relative delays in event order, not recognition order.
            long cursor = 0, recordedEnd = 0;
            foreach (var (step, start) in timed.OrderBy(item => item.Start))
            {
                long span = step.Type switch
                {
                    "drag" => step.DurationMs ?? 0,
                    "mousehold" or "keyhold" => step.Ms ?? 0,
                    "type" => (long)(step.IntervalMs ?? 0) * ((step.Text?.Length ?? 1) - 1),
                    _ => 0,
                };
                step.DelayMs = (int)Math.Max(0, start - cursor);
                cursor = Math.Max(cursor, start) + span;
                recordedEnd = Math.Max(recordedEnd, start + span);
                Steps.Add(step);
            }

            var document = new FlowDocument
            {
                RecordedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                DurationMs = _mode == Mode.Key ? null : (int)Math.Min(int.MaxValue, recordedEnd),
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

        /// <summary>
        /// Bind client coordinates from the event-time snapshot, never from the desktop at Finish().
        /// CLI legacy mode and cross-window gestures remain explicitly screen-relative.
        /// </summary>
        private FlowStep Describe(HookEvent e, string type, string? detail, Point? point, Point? to, int? duration, int? wheelDelta, HookEvent? release = null)
        {
            var step = new FlowStep { Type = type };
            Point actual = point ?? new Point(e.X, e.Y);
            IntPtr window = !_shots ? 0 : type is "click" or "drag" or "wheel" or "move" or "mousedown" or "mouseup" or "mousehold" or "keyhold"
                ? WindowLocator.WindowAt(actual.X, actual.Y)
                : NativeWindow.GetForegroundWindow();

            Point? reference = point;
            if (type is "click" or "wheel" or "move" or "drag" or "mousedown" or "mouseup" or "mousehold")
            {
                step.At = new FlowPoint { Space = FlowSpace.Screen, X = actual.X, Y = actual.Y };
            }
            else
            {
                // 键盘事件没有坐标；指针位置只用来对准截图。
                step.At = null;
                reference = _shots ? NativeHooks.GetCursorPosition() : null;
            }

            if (type == "drag" && to is { } destination)
            {
                step.From = step.At;
                step.To = new FlowPoint { Space = FlowSpace.Screen, X = destination.X, Y = destination.Y };
                step.DurationMs = duration;
            }
            // 按键必须写进文件：以前这里漏了，于是**右键/中键一律按左键回放**（实测反馈）。
            // 长按的按下/抬起**总是**写明按键（否则左键长按和右键长按就分不出来）；
            // 点击与拖动仍省略左键（默认值），少写一个字是一字。
            if (type is "mousedown" or "mouseup")
            {
                if (detail is { Length: > 0 } held) step.Button = held;
            }
            else if (type is "click" or "drag" && detail is { Length: > 0 } pressed && pressed != "left")
            {
                step.Button = pressed;
            }
            else if (type is "mousehold" or "keyhold" && detail is { Length: > 0 } holding)
            {
                // 鼠标长按才有 button；键盘长按（w、ctrl+s）走 text/combo——
                // 以前把键名塞进 button，界面上就变成"长按 鼠标左键 … @ 0,0"（实测反馈）。
                if (type == "mousehold" && holding is "left" or "right" or "middle" or "x1" or "x2")
                {
                    step.Button = holding;
                }
                else
                {
                    step.At = null;   // 键盘长按没有落点
                    step.Combo = holding;
                }
            }
            if (type == "wheel")
            {
                step.Delta = wheelDelta;
                // 竖向是默认，不写进文件；只有横向才留痕（回放走 mouse hwheel）。
                if (detail == "horizontal") step.Axis = FlowAxis.Horizontal;
            }
            if (type == "type")
            {
                step.Text = detail;
                if (_mode == Mode.Full) step.IntervalMs = e.Data;
            }
            // 单键与组合键共用一个字段：enter、ctrl+shift+s 都写进 combo。
            if (type is "key" or "keydown" or "keyup")
            {
                step.Combo = detail;
            }

            if (point != null && e.ClientTarget != null && (release == null ||
                (release is { ClientTarget: not null } up && up.Window == e.Window)))
            {
                static FlowPoint Relative(Point p) => new() { Space = FlowSpace.Client, X = p.X, Y = p.Y };
                step.Target = e.ClientTarget;
                if (type == "drag") { step.At = null; step.From = Relative(e.ClientPoint); step.To = Relative(release!.Value.ClientPoint); }
                else step.At = Relative(e.ClientPoint);
                window = e.Window;
            }
            if (e.Sampled)
            {
                // 老实说清楚：这一下不是钩子看见的，是轮询补上的。
                step.Note = "轮询补记：原始按钮事件没有到达钩子，位置与时间按当时光标采样";
            }

            if (_shots && reference is { } shotAt && type is "click" or "drag" or "wheel" or "type" or "key")
            {
                step.Shot = TakeShot(window, shotAt, type, step.Target == null ? null : e.ClientPoint, step.Target);
            }

            return step;
        }


        /// <summary>
        /// The step's screenshot: the whole client area of the window the action happened in, scaled
        /// down to fit, with a mark on the point the action used.
        ///
        /// Measured why: a 1:1 crop around the click (320x200) came out as a blank white square on a
        /// form with empty space, which answers nothing. The window as a whole answers the question
        /// the shot exists for - "what was on screen when I did this" - and the mark keeps the
        /// coordinates visible.
        /// </summary>
        private string? TakeShot(IntPtr window, Point actionPoint, string type, Point? clientPoint, FlowTarget? expected)
        {
            if (window == IntPtr.Zero) return null;
            if (!NativeWindow.GetClientRect(window, out NativeWindow.RECT client)) return null;
            int width = client.Right - client.Left;
            int height = client.Bottom - client.Top;
            if (width <= 0 || height <= 0) return null;

            var origin = new NativeWindow.POINT { X = 0, Y = 0 };
            if (!NativeWindow.ClientToScreen(window, ref origin)) return null;
            NativeCapture.Frame? frame;
            if (clientPoint != null && expected != null)
            {
                var info = WindowInfo.Capture(window);
                if (info.ClassName != expected.Class || info.ProcessName != expected.Process || info.Title != expected.Title) return null;
                try { frame = Probe.CaptureRegion(info, false, (0, 0, width, height), "print"); }
                catch (Exception) { return null; } // Evidence is optional; never substitute the covering window.
            }
            else frame = NativeCapture.TryCaptureScreen(origin.X, origin.Y, width, height);
            if (frame is null) return null;

            Directory.CreateDirectory(ShotDirectory);
            string png = Path.Combine(ShotDirectory, $"{++_shotIndex:d4}-{type}.png");
            try
            {
                using var full = ToBitmap(frame);
                double scale = Math.Min(1.0, Math.Min(480.0 / width, 320.0 / height));
                int scaledWidth = Math.Max(1, (int)(width * scale));
                int scaledHeight = Math.Max(1, (int)(height * scale));

                using var bitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppRgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(full, 0, 0, scaledWidth, scaledHeight);

                    // The pointer may have been outside the client area (a menu, another window):
                    // mark the middle then, so the picture still says "somewhere in here".
                    double relativeX = clientPoint?.X ?? actionPoint.X - origin.X;
                    double relativeY = clientPoint?.Y ?? actionPoint.Y - origin.Y;
                    if (relativeX < 0 || relativeY < 0 || relativeX > width || relativeY > height)
                    {
                        relativeX = width / 2.0;
                        relativeY = height / 2.0;
                    }

                    float x = (float)(relativeX * scale);
                    float y = (float)(relativeY * scale);
                    using var pen = new Pen(Color.FromArgb(235, 255, 64, 64), 2f);
                    graphics.DrawEllipse(pen, x - 9, y - 9, 18, 18);
                    graphics.DrawLine(pen, x - 15, y, x + 15, y);
                    graphics.DrawLine(pen, x, y - 15, x, y + 15);
                }

                bitmap.Save(png, ImageFormat.Png);
            }
            catch (Exception)
            {
                return null; // a missing screenshot must not lose the recording
            }

            ShotCount++;
            return Path.Combine(Path.GetFileName(ShotDirectory), Path.GetFileName(png)).Replace('\\', '/');
        }

        private static Bitmap ToBitmap(NativeCapture.Frame frame)
        {
            var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppRgb);
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
            return bitmap;
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
