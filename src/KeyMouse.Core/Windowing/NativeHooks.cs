using System.Runtime.InteropServices;

namespace KeyMouse.Windowing;
/// <summary>What a low-level hook saw. One shape for both keyboards and mice, so callers switch on
/// <see cref="Kind"/> instead of juggling two callback signatures.</summary>
internal enum HookKind
{
    KeyDown,
    KeyUp,
    MouseMove,
    ButtonDown,
    ButtonUp,
    Wheel,

    /// <summary>横向滚轮（WM_MOUSEHWHEEL）。与竖向分开，因为回放命令不同（mouse hwheel）。</summary>
    HWheel,
}

/// <summary>
/// One observed input event. Times come from the hook itself (milliseconds since boot), which is
/// what makes the recorded pauses meaningful: they are the gaps the user actually left.
/// </summary>
internal readonly record struct HookEvent(
    HookKind Kind, int VirtualKey, int X, int Y, int Data, uint Time, IntPtr Window)
{
    /// <summary>这条事件不是钩子送来的，而是轮询按钮状态补记的（位置/时间为采样值）。</summary>
    public bool Sampled { get; init; }
}
/// <summary>
/// Global low-level keyboard and mouse hooks (WH_KEYBOARD_LL / WH_MOUSE_LL).
///
/// Two deliberate choices:
///
/// * The hooks **observe and pass through**: the callback returns CallNextHookEx without touching
///   the event, so the user keeps typing into whatever they are actually using. A recorder that
///   swallowed input would be unusable.
/// * No filtering of injected events either. "Injected" only means "some program sent it", and a
///   session that mixes a human and a KeyMouse command should record what actually happened. It
///   also means this recorder can be tested by driving it with KeyMouse's own SendInput.
///
/// The message loop is a PeekMessage poll rather than a blocking GetMessage: low-level hooks need a
/// pumping thread, and the recorder also has to notice its own stop conditions (a timer, a flag)
/// with no input arriving.
/// </summary>
internal static class NativeHooks
{
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;

    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseMove = 0x0200;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int WmMouseHWheel = 0x020E;
    private const uint PmRemove = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public POINT Point;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG message, IntPtr window, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern uint MsgWaitForMultipleObjects(uint count, IntPtr handles, bool waitAll, uint timeoutMs, uint wakeMask);

    private const uint QsAllInput = 0x04FF;

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG message);

    [DllImport("user32.dll")]
    internal static extern bool GetKeyboardState(byte[] state);

    /// <summary>Layout-aware key translation: what character this key really produces.</summary>
    [DllImport("user32.dll")]
    internal static extern int ToUnicodeEx(
        uint virtualKey, uint scanCode, byte[] state, [Out] char[] buffer, int count, uint flags, IntPtr layout);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr window, out int pid);
    // CharSet 必须写死 Unicode：不写就按 ANSI 编排，char[] 会被当成字节数组写，
    // 于是中文标题读出来是「蛻刧y橆v觺刧」这种两字节错位的乱码（实测报错过一次）。
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetWindowTextW(IntPtr window, [Out] char[] text, int count);

    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private const int VkLeftButton = 0x01;
    private const int VkRightButton = 0x02;
    private const int VkMiddleButton = 0x04;

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int index);

    internal const int SmXVirtualScreen = 76;
    internal const int SmYVirtualScreen = 77;
    internal const int SmCxVirtualScreen = 78;
    internal const int SmCyVirtualScreen = 79;

    /// <summary>Current pointer position; the recorder uses it to aim screenshots of key steps.</summary>
    internal static System.Drawing.Point GetCursorPosition() =>
        GetCursorPos(out POINT point) ? new System.Drawing.Point(point.X, point.Y) : default;

    /// <summary>
    /// Installs both hooks and pumps messages until <paramref name="stop"/> returns true (or the
    /// callback throws). Everything the hooks see is handed to <paramref name="onEvent"/>, which
    /// must return quickly: it runs inside the hook chain of every input event on the machine.
    /// </summary>
    /// <summary>
    /// 钩子委托必须活到卸载为止：原生侧只握着函数指针，委托一被 GC 回收，钩子就指向空气。
    /// 用静态字段钉住——局部变量在 Release 下可能被判成死引用（这类"偶发丢事件"通常就是它）。
    /// </summary>
    private static HookProc? _keyboardHook;
    private static HookProc? _mouseHook;
    private static Action<HookEvent>? _sink;

    /// <summary>每个按钮最近一次由钩子报告的时间（毫秒 tick）：轮询到同一状态时用来去重。</summary>
    private static readonly Dictionary<int, uint> LastHookButton = [];

    private static readonly int[] PolledButtons = [VkLeftButton, VkRightButton, VkMiddleButton];

    /// <summary>
    /// 录制期间光标停在过、但**权限比我们高**的窗口（进程令牌打不开）。
    /// Windows 不会把发往更高完整性窗口的输入交给低权限进程的全局钩子——
    /// 这些窗口上的点击既收不到也补不了，只能如实告诉用户。（实测：哔哩哔哩客户端就是这种）
    /// </summary>
    internal static readonly Dictionary<int, string> OutOfReach = [];

    /// <summary>光标底下的顶层窗口，能不能读到它的进程令牌；读不到就把 pid + 标题记下来。</summary>
    private static void CheckReach()
    {
        var cursor = new POINT();
        if (!GetCursorPos(out cursor)) return;

        IntPtr window = GetAncestor(WindowFromPoint(cursor), 2 /*GA_ROOT*/);
        if (window == IntPtr.Zero) return;
        if (GetWindowThreadProcessId(window, out int pid) == 0 || pid <= 4) return;

        IntPtr process = OpenProcess(0x1000 /*PROCESS_QUERY_LIMITED_INFORMATION*/, false, pid);
        if (process == IntPtr.Zero)
        {
            // 连进程都打不开：多半是别的用户/更强的隔离
            OutOfReach[pid] = Title(window);
            return;
        }

        try
        {
            if (!OpenProcessToken(process, 0x0008 /*TOKEN_QUERY*/, out IntPtr token))
            {
                OutOfReach[pid] = Title(window);
                return;
            }
            CloseHandle(token);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static string Title(IntPtr window)
    {
        var buffer = new char[512];
        int length = GetWindowTextW(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, Math.Min(length, buffer.Length)) : "（无标题）";
    }

    /// <summary>这一轮录制里钩子真正收到的事件条数，按类型分开计数（诊断用）。</summary>
    internal static readonly Dictionary<string, int> Seen = [];

    private static void Count(string kind)
    {
        Seen[kind] = Seen.TryGetValue(kind, out int n) ? n + 1 : 1;
    }

    /// <summary>某个屏幕点属于哪个窗口（录制的忽略区判断用）。</summary>
    internal static IntPtr WindowAt(int x, int y) => WindowFromPoint(new POINT { X = x, Y = y });

    /// <summary>某个虚拟键现在是否按着（录制器用它确认停止组合键，不依赖自己记的修饰键表）。</summary>
    internal static short KeyState(int virtualKey) => GetAsyncKeyState(virtualKey);

    internal static void Record(Action<HookEvent> onEvent, Func<bool> stop, Action? ready = null)
    {
        _sink = onEvent;
        Seen.Clear();
        OutOfReach.Clear();
        HookProc keyboard = _keyboardHook = (code, wParam, lParam) =>
        {
            int message = wParam.ToInt32();
            if (code >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                HookKind kind = message is WmKeyDown or WmSysKeyDown ? HookKind.KeyDown : HookKind.KeyUp;
                // Data carries the scan code for keys: ToUnicodeEx needs it to give the character
                // the layout actually produces (and it is the only reliable way to record text).
                Count(kind == HookKind.KeyDown ? "keyDown" : "keyUp");
                _sink?.Invoke(new HookEvent(kind, (int)data.VkCode, 0, 0, (int)data.ScanCode, data.Time, IntPtr.Zero));
            }
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        };
        HookProc mouse = _mouseHook = (code, wParam, lParam) =>
        {
            // 回调里抛出的异常会穿回原生钩子链，把输入留在半坏的状态（鼠标被粘住的一种成因）。
            // 录制器宁可漏一条事件，也不能让整机输入出问题。
            try
            {
            int message = wParam.ToInt32();
            if (code >= 0)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                HookKind kind = message switch
                {
                    WmMouseMove => HookKind.MouseMove,
                    WmLeftButtonDown or WmRightButtonDown or WmMiddleButtonDown or WmXButtonDown => HookKind.ButtonDown,
                    WmLeftButtonUp or WmRightButtonUp or WmMiddleButtonUp or WmXButtonUp => HookKind.ButtonUp,
                    WmMouseWheel => HookKind.Wheel,
                WmMouseHWheel => HookKind.HWheel,
                    _ => HookKind.MouseMove,
                };
                int button = message switch
                {
                    WmRightButtonDown or WmRightButtonUp => 2,
                    WmMiddleButtonDown or WmMiddleButtonUp => 3,
                    WmXButtonDown or WmXButtonUp => 4,
                    _ => 1,
                };
                int data2 = kind is HookKind.Wheel or HookKind.HWheel
                    ? (short)(data.MouseData >> 16)
                    : kind is HookKind.ButtonDown or HookKind.ButtonUp ? button : 0;
                // 按按键分开计数：右键录不到时，这一行能直接区分"钩子没收到"和"我们丢了"。
                Count(kind is HookKind.ButtonDown or HookKind.ButtonUp
                    ? kind + "(" + (data2 switch { 2 => "right", 3 => "middle", 4 => "x1", _ => "left" }) + ")"
                    : kind.ToString());
                if (kind is HookKind.ButtonDown or HookKind.ButtonUp) LastHookButton[data2] = data.Time;
                _sink?.Invoke(new HookEvent(kind, 0, data.Point.X, data.Point.Y, data2, data.Time, IntPtr.Zero));
            }
            }
            catch (Exception)
            {
                // 见上：录制失败可以重来，输入卡死不行
            }
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        };

        IntPtr module = GetModuleHandle(null);
        IntPtr installed = SetWindowsHookEx(WhKeyboardLl, keyboard, module, 0);
        IntPtr installedMouse = SetWindowsHookEx(WhMouseLl, mouse, module, 0);
        if (installed == IntPtr.Zero || installedMouse == IntPtr.Zero)
        {
            if (installed != IntPtr.Zero) UnhookWindowsHookEx(installed);
            if (installedMouse != IntPtr.Zero) UnhookWindowsHookEx(installedMouse);
            throw new CommandFailure(4, "装不上全局键鼠钩子——录制需要交互式桌面（锁屏/远程会话里录不了）");
        }

        // 钩子真装好了才算"开始录制"。界面靠这一下把"正在初始化"换成"停止录制"，
        // 而不是点下去就先假装开始了（用户看到的按钮状态必须与事实一致）。
        ready?.Invoke();

        try
        {
            // 低级钩子是在带内执行的：钩子线程睡 5ms，整机输入就跟着多 5ms 延迟。
            // 任务栏这种悬停事件最密的地方最先卡住——实测里鼠标会贴在任务栏上出不来。
            // 所以不再空转：有事件就泵，没事件就睡着，超时只是为了看得见 stop()。
            var held = new Dictionary<int, bool>();
            foreach (int button in PolledButtons) held[button] = false;
            uint lastReachCheck = 0;

            while (!stop())
            {
                MsgWaitForMultipleObjects(0, IntPtr.Zero, false, 20, QsAllInput);

                // 每 250ms 看一眼光标底下是谁：权限更高的窗口要记下来，最后如实告诉用户。
                uint tick = (uint)Environment.TickCount64;
                if (tick - lastReachCheck > 250)
                {
                    lastReachCheck = tick;
                    try { CheckReach(); } catch (Exception) { }
                }

                // 有些输入到不了低级钩子（触摸/触控笔被提升成鼠标消息、或目标进程的完整性更高）。
                // 钩子本来只负责"看"，这里补一层轮询：状态变了就记一条，并标明是采样来的。
                foreach (int button in PolledButtons)
                {
                    bool down = (GetAsyncKeyState(button) & 0x8000) != 0;
                    if (down == held[button]) continue;
                    held[button] = down;

                    uint now = (uint)Environment.TickCount64;
                    if (LastHookButton.TryGetValue(button, out uint hookTime) && now - hookTime < 250)
                        continue;   // 钩子刚报过同一个动作，别重复记

                    System.Drawing.Point cursor = GetCursorPosition();
                    _sink?.Invoke(new HookEvent(
                        down ? HookKind.ButtonDown : HookKind.ButtonUp, 0, cursor.X, cursor.Y, button, now, IntPtr.Zero)
                    {
                        Sampled = true,
                    });
                }
                while (PeekMessage(out MSG message, IntPtr.Zero, 0, 0, PmRemove))
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
        }
        finally
        {
            UnhookWindowsHookEx(installed);
            UnhookWindowsHookEx(installedMouse);
        }
    }
}
