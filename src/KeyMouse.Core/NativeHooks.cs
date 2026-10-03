using System.Runtime.InteropServices;

namespace KeyMouse;

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
}

/// <summary>
/// One observed input event. Times come from the hook itself (milliseconds since boot), which is
/// what makes the recorded pauses meaningful: they are the gaps the user actually left.
/// </summary>
internal readonly record struct HookEvent(
    HookKind Kind, int VirtualKey, int X, int Y, int Data, uint Time, IntPtr Window);
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
    internal static void Record(Action<HookEvent> onEvent, Func<bool> stop)
    {
        // The delegates must outlive the call: the OS keeps raw pointers to them.
        HookProc keyboard = (code, wParam, lParam) =>
        {
            int message = wParam.ToInt32();
            if (code >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                HookKind kind = message is WmKeyDown or WmSysKeyDown ? HookKind.KeyDown : HookKind.KeyUp;
                // Data carries the scan code for keys: ToUnicodeEx needs it to give the character
                // the layout actually produces (and it is the only reliable way to record text).
                onEvent(new HookEvent(kind, (int)data.VkCode, 0, 0, (int)data.ScanCode, data.Time, IntPtr.Zero));
            }
            return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        };
        HookProc mouse = (code, wParam, lParam) =>
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
                    WmMouseWheel or WmMouseHWheel => HookKind.Wheel,
                    _ => HookKind.MouseMove,
                };
                int button = message switch
                {
                    WmRightButtonDown or WmRightButtonUp => 2,
                    WmMiddleButtonDown or WmMiddleButtonUp => 3,
                    WmXButtonDown or WmXButtonUp => 4,
                    _ => 1,
                };
                int data2 = kind == HookKind.Wheel
                    ? (short)(data.MouseData >> 16)
                    : kind is HookKind.ButtonDown or HookKind.ButtonUp ? button : 0;
                onEvent(new HookEvent(kind, 0, data.Point.X, data.Point.Y, data2, data.Time, IntPtr.Zero));
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

        try
        {
            while (!stop())
            {
                if (PeekMessage(out MSG message, IntPtr.Zero, 0, 0, PmRemove))
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
                else
                {
                    Thread.Sleep(5);
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
