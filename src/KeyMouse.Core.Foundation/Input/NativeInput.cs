using System.Runtime.InteropServices;

namespace KeyMouse.Input;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private static void Send(params INPUT[] inputs)
    {
        if (ExecutionMode.DryRun)
        {
            ExecutionMode.NoteSuppressed(inputs.Length);
            return;
        }

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        int err = sent == inputs.Length ? 0 : Marshal.GetLastPInvokeError();
        AccountSend(inputs.Length, sent, err);
    }

    internal static void AccountSend(int requested, uint sent, int err)
    {
        ExecutionMode.NoteInjected((int)sent);
        if (sent != requested)
        {
            // SendInput 的返回值和 last-error 都无法证明/排除 UIPI，错误 0 也可能是权限边界。
            string hint = "——若目标是管理员窗口，请检查受信任 UIAccess 输入组件是否已安装并启用；"
                + "Windows 权限隔离或目标程序的输入保护均可能阻止注入，切换坐标模式不能绕过";
            throw new InvalidOperationException($"SendInput 只注入了 {sent}/{requested} 个事件（Win32 错误 {err}）{hint}");
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
        if (!GetCursorPos(out POINT p)) throw NativeError.Last("GetCursorPos");
        return (p.X, p.Y);
    }

    /// <summary>Moves the cursor to an absolute virtual-desktop pixel.</summary>
    public static void MoveTo(int x, int y)
    {
        // A suppressed move cannot reach its target: never retry it via SetCursorPos.
        if (ExecutionMode.DryRun)
        {
            ExecutionMode.NoteSuppressed(1);
            return;
        }
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
            // 不睡觉：SendInput 是同步的，光标位置在返回时已经更新。
            // 以前这里固定 Thread.Sleep(8)——619 次移动就是 5 秒，回放因此"均匀而缓慢"（实测反馈）。
            // 只有真的没落到位（绝对坐标量化误差）才短暂等待后重试。
            var p = GetCursor();
            if (p.X == x && p.Y == y) return;
            Thread.Sleep(1);
        }

        if (!SetCursorPos(x, y))
            throw NativeError.Last("SetCursorPos");
    }

    public static void MoveBy(int dx, int dy) => Send(Mouse(dx, dy, 0, MOUSEEVENTF_MOVE));

    private static (uint Down, uint Up, uint Data) ButtonFlags(string button) => button.ToLowerInvariant() switch
    {
        "left" => (0x0002u, 0x0004u, 0u),
        "right" => (0x0008u, 0x0010u, 0u),
        "middle" => (0x0020u, 0x0040u, 0u),
        "x1" => (0x0080u, 0x0100u, 1u),
        "x2" => (0x0080u, 0x0100u, 2u),
        _ => throw new ArgumentException($"未知鼠标键 '{button}'（可用 left|right|middle|x1|x2）")
    };

    internal static void ValidateButton(string button) => _ = ButtonFlags(button);

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
            try { Thread.Sleep(15); } finally { ButtonUp(button); }
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
        try { Thread.Sleep(20); } finally { Key(vk, extended, true); }
    }

    public static void Chord(IReadOnlyList<(ushort Vk, bool Ext)> keys, int holdMs) =>
        Chord(keys, holdMs, Key, Thread.Sleep);

    // On a failed key-down release only keys that actually went down. A failed release must
    // not strand the remaining modifiers or hide the original injection failure.
    internal static void Chord(IReadOnlyList<(ushort Vk, bool Ext)> keys, int holdMs,
        Action<ushort, bool, bool> send, Action<int> wait)
    {
        int held = 0;
        Exception? failure = null;
        try
        {
            for (; held < keys.Count; held++) send(keys[held].Vk, keys[held].Ext, false);
            wait(Math.Max(1, holdMs));
        }
        catch (Exception ex) { failure = ex; }
        while (held > 0)
        {
            var key = keys[--held];
            try { send(key.Vk, key.Ext, true); }
            catch (Exception ex) { failure ??= ex; }
        }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
    }

    private const ushort VkReturn = 0x0D;

    /// <summary>Types text through KEYEVENTF_UNICODE - layout and IME independent.</summary>
    public static void TypeText(string text, int intervalMs)
    {
        // 换行有三种写法：\r\n（Windows 文件）、\n（JSON 转义）、\r（WinUI TextBox 就是这么存换行的）。
        // 一律折成 \n，否则裸 \r 会被当成 CRLF 的前半个字符跳掉——编辑器里改过的多行文本就一个回车都不发。
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // 换行必须按 Enter：Unicode 键事件把 0x0A 当成一个普通字符，目标程序收不到换行，
            // 多行文本会被压成一行（实测）。\t 同理，但它会改变焦点，不替用户决定。
            if (c == '\n')
            {
                // 按下和抬起之间必须留一点时间：连发的 down/up 会被目标吞掉，
                // 实测 Notepad（RichEdit）四个换行只收到两个，两段文字挤在一行。
                Key(VkReturn, extended: false, up: false);
                try { Thread.Sleep(15); } finally { Key(VkReturn, extended: false, up: true); }
                Thread.Sleep(30);   // 抬起之后也要留时间：紧接着的字符会把这次换行挤掉
            }
            else
            {
                Send(Unicode(c, false), Unicode(c, true));
            }

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
