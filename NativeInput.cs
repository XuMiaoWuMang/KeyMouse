using System.Runtime.InteropServices;

namespace KeyMouse;

/// <summary>
/// Thin wrapper over Win32 SendInput. Every call injects real input events into the
/// system input queue, exactly as a physical mouse/keyboard would.
/// </summary>
internal static class NativeInput
{
    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x1000;

    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private static void Send(params INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            int err = Marshal.GetLastWin32Error();
            string hint = err == 5
                ? " - blocked by UIPI: the target window probably runs elevated; run KeyMouse elevated too"
                : "";
            throw new InvalidOperationException($"SendInput injected {sent}/{inputs.Length} events (win32 error {err}){hint}");
        }
    }

    private static INPUT Mouse(int dx, int dy, uint data, uint flags) => new INPUT
    {
        type = INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags }
        }
    };

    // ---------------------------------------------------------------- mouse

    public static (int X, int Y) GetCursor()
    {
        if (!GetCursorPos(out POINT p)) throw new InvalidOperationException("GetCursorPos failed");
        return (p.X, p.Y);
    }

    /// <summary>Moves the cursor to an absolute virtual-desktop pixel.</summary>
    public static void MoveTo(int x, int y)
    {
        int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 1);
        int vh = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 1);

        // Absolute mouse input is quantised to 0..65535, so verify and retry before
        // falling back to the pixel-exact (but non-event) SetCursorPos.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int nx = (int)Math.Round((x - vx) * 65535.0 / vw);
            int ny = (int)Math.Round((y - vy) * 65535.0 / vh);
            Send(Mouse(nx, ny, 0, MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK));
            Thread.Sleep(8);
            var p = GetCursor();
            if (p.X == x && p.Y == y) return;
        }

        SetCursorPos(x, y);
    }

    public static void MoveBy(int dx, int dy) => Send(Mouse(dx, dy, 0, MOUSEEVENTF_MOVE));

    private static (uint Down, uint Up, uint Data) ButtonFlags(string button) => button.ToLowerInvariant() switch
    {
        "left" => (0x0002u, 0x0004u, 0u),
        "right" => (0x0008u, 0x0010u, 0u),
        "middle" => (0x0020u, 0x0040u, 0u),
        "x1" => (0x0080u, 0x0100u, 1u),
        "x2" => (0x0080u, 0x0100u, 2u),
        _ => throw new ArgumentException($"unknown mouse button '{button}' (use left|right|middle|x1|x2)")
    };

    public static void ButtonDown(string button)
    {
        var (down, _, data) = ButtonFlags(button);
        Send(Mouse(0, 0, data, down));
    }

    public static void ButtonUp(string button)
    {
        var (_, up, data) = ButtonFlags(button);
        Send(Mouse(0, 0, data, up));
    }

    public static void Click(string button, int count, int intervalMs)
    {
        for (int i = 0; i < count; i++)
        {
            ButtonDown(button);
            Thread.Sleep(15);
            ButtonUp(button);
            if (i < count - 1) Thread.Sleep(Math.Max(1, intervalMs));
        }
    }

    public static void Wheel(int delta, bool horizontal)
        => Send(Mouse(0, 0, unchecked((uint)delta), horizontal ? MOUSEEVENTF_HWHEEL : MOUSEEVENTF_WHEEL));

    public static void Drag(int x1, int y1, int x2, int y2, string button, int steps, int durationMs)
    {
        steps = Math.Max(1, steps);
        MoveTo(x1, y1);
        Thread.Sleep(30);
        ButtonDown(button);
        try
        {
            int perStepMs = Math.Max(1, durationMs / steps);
            for (int i = 1; i <= steps; i++)
            {
                MoveTo(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps);
                Thread.Sleep(perStepMs);
            }
        }
        finally
        {
            ButtonUp(button);
        }
    }

    // ------------------------------------------------------------- keyboard

    public static void Key(ushort vk, bool extended, bool up)
    {
        uint flags = (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0);
        Send(new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)MapVirtualKey(vk, 0),
                    dwFlags = flags
                }
            }
        });
    }

    public static void Tap(ushort vk, bool extended)
    {
        Key(vk, extended, false);
        Thread.Sleep(20);
        Key(vk, extended, true);
    }

    public static void Chord(IReadOnlyList<(ushort Vk, bool Ext)> keys, int holdMs)
    {
        foreach (var k in keys) Key(k.Vk, k.Ext, false);
        Thread.Sleep(Math.Max(1, holdMs));
        for (int i = keys.Count - 1; i >= 0; i--) Key(keys[i].Vk, keys[i].Ext, true);
    }

    /// <summary>Types text through KEYEVENTF_UNICODE - layout and IME independent.</summary>
    public static void TypeText(string text, int intervalMs)
    {
        foreach (char c in text)
        {
            Send(Unicode(c, false), Unicode(c, true));
            if (intervalMs > 0) Thread.Sleep(intervalMs);
        }

        // Grace period: let the target drain the tail of the queue before this process exits.
        Thread.Sleep(60);
    }

    private static INPUT Unicode(char c, bool up) => new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = c,
                dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0)
            }
        }
    };
}
