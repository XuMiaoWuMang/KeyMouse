using System.Runtime.InteropServices;
using System.Text;

namespace KeyMouse;

/// <summary>Win32 window interop used for window targeting, eligibility checks and focus.</summary>
internal static class NativeWindow
{
    public const uint GA_ROOT = 2;
    public const uint WM_NULL = 0x0000;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const uint DWMWA_CLOAKED = 14;
    public const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
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
    public struct POINT
    {
        public int X;
        public int Y;
    }

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
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, uint dwAttribute, out int pvAttribute, int cbAttribute);

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

    /// <summary>
    /// The window that owns this one, or zero for a primary window. Dialogs, popups,
    /// composition bridges and IME UI are owned by the app's real window, which makes
    /// this the most reliable way to tell a target from its helpers.
    /// </summary>
    public static IntPtr GetOwner(IntPtr h) => h == IntPtr.Zero ? IntPtr.Zero : GetWindow(h, GW_OWNER);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private static readonly Dictionary<uint, string> ProcessNameCache = new();

    /// <summary>Drops the pid cache. Only the benchmark needs this, to time a cold lookup.</summary>
    internal static void ClearProcessNameCache() => ProcessNameCache.Clear();

    /// <summary>
    /// Image name for a pid (without extension), cached for the life of this process.
    /// System.Diagnostics.Process.GetProcessById costs ~1.3 ms per call - a command that
    /// enumerates a desktop with 69 distinct processes spent ~90 ms in it alone.
    /// QueryFullProcessImageName is one syscall. A pid recycled by a different process
    /// during a single CLI run is not a case worth the extra bookkeeping.
    /// </summary>
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

    /// <summary>True when DWM considers the window cloaked (suspended UWP app, other virtual desktop).</summary>
    public static bool IsCloaked(IntPtr h)
    {
        try
        {
            return DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Round-trips WM_NULL to the window's message queue. null = no answer within the timeout (hung).</summary>
    public static long? ResponseMs(IntPtr h, uint timeoutMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        IntPtr ok = SendMessageTimeout(h, WM_NULL, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, timeoutMs, out _);
        sw.Stop();
        return ok == IntPtr.Zero ? null : sw.ElapsedMilliseconds;
    }
}
