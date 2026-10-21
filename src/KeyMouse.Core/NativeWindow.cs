using System.Runtime.InteropServices;
using System.Text;

namespace KeyMouse;

// Win32 窗口互操作层的声明和薄封装
internal static class NativeWindow
{
    // 取顶层根窗口
    public const uint GA_ROOT = 2;
    // 空消息，用于响应性探测
    public const uint WM_NULL = 0x0000;
    // 若目标线程挂起则立即返回，避免探测卡死
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    // 窗口是否被遮盖
    public const uint DWMWA_CLOAKED = 14;
    // 还原最小化窗口
    public const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    // 窗口矩形结构体，表示窗口在屏幕上的位置和大小
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    // 屏幕坐标点结构体，表示屏幕上的一个点
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // 封装一些原生的c接口
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowEnabled(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    // AttachThreadInput 是文档化的合法化方式，而且不注入任何按键。
    // 所以它应该在放弃之前使用，而不是之后。
    public static uint AttachToForegroundThread()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return 0;
        uint other = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        uint self = GetCurrentThreadId();
        if (other == 0 || other == self) return 0;
        return AttachThreadInput(other, self, true) ? other : 0;
    }

    // 分离线程
    public static void DetachFromThread(uint threadId)
    {
        if (threadId == 0) return;
        AttachThreadInput(threadId, GetCurrentThreadId(), false);
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetWindowAttributeRect(IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

    // DWM 时代 GetWindowRect 与用户实际可见区域不一致：它含不可见的缩放边框，实测每边约 8 像素，
    // 而 PrintWindow 只画可见部分——两者混用会把截图整体偏移那条边框（实测：抓到的字顶部被切掉）。
    // 所以优先查 DWM 的可见外框，它没答案时才退回 GetWindowRect。
    internal static bool TryGetVisibleFrame(IntPtr hwnd, out RECT bounds)
    {
        const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        if (DwmGetWindowAttributeRect(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT visible, Marshal.SizeOf<RECT>()) == 0 &&
            visible.Right > visible.Left && visible.Bottom > visible.Top)
        {
            bounds = visible;
            return true;
        }
        return GetWindowRect(hwnd, out bounds);
    }

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    private const uint GW_OWNER = 4;

    public static string GetTitle(IntPtr h)
    {
        var sb = new StringBuilder(512);
        GetWindowText(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClass(IntPtr h)
    {
        var sb = new StringBuilder(256);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    public static IntPtr Root(IntPtr h) => h == IntPtr.Zero ? IntPtr.Zero : GetAncestor(h, GA_ROOT);

    // 这个窗口的属主窗口；主窗口返回 0。对话框、弹层、输入法窗口都挂在应用真窗口名下，
    // 所以"有没有属主"是区分目标窗口和它的帮手窗口最可靠的一条。
    public static IntPtr GetOwner(IntPtr h) => h == IntPtr.Zero ? IntPtr.Zero : GetWindow(h, GW_OWNER);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private static readonly Dictionary<uint, string> ProcessNameCache = new();

    // 清空进程名缓存。只有性能基准需要它——它要量的是"冷查一次"的耗时。
    internal static void ClearProcessNameCache() => ProcessNameCache.Clear();

    // 将进程ID转换为进程名，对二者进行缓存避免重复查询造成不必要的开销，查询失败返回"?"。
    public static string ProcessNameOf(uint pid)
    {
        if (ProcessNameCache.TryGetValue(pid, out string? cached)) return cached;

        string name = "?";
        IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process != IntPtr.Zero)
        {
            try
            {
                var buffer = new StringBuilder(512);
                int size = buffer.Capacity;
                if (QueryFullProcessImageName(process, 0, buffer, ref size))
                    name = Path.GetFileNameWithoutExtension(buffer.ToString());
            }
            finally
            {
                CloseHandle(process);
            }
        }

        ProcessNameCache[pid] = name;
        return name;
    }

    // 用 DWMWA_CLOAKED 判断窗口是否被 DWM 遮盖。
    public static bool IsCloaked(IntPtr h)
    {
        try
        {
            return DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
        }
        catch (DllNotFoundException)
        {
            // 极老系统或非 Windows 环境没有 dwmapi.dll，此时返回 false
            return false;
        }
    }

    // 响应性探测
    public static long? ResponseMs(IntPtr h, uint timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // 给目标窗口发一个 WM_NULL 消息，测量它多久能响应；如果超时或窗口挂起，返回 null，否则返回耗时毫秒数
        // lpdwResult 对这个测量没有意义，所以被丢弃
        IntPtr ok = SendMessageTimeout(h, WM_NULL, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, timeoutMs, out _);
        sw.Stop();
        return ok == IntPtr.Zero ? null : sw.ElapsedMilliseconds;
    }
}
