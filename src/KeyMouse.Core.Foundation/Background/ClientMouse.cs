using System.Diagnostics;
using System.Runtime.InteropServices;
using W = KeyMouse.Windowing.NativeWindow;

namespace KeyMouse.Input;

/// <summary>JSON-flow client coordinates. All messages in a gesture stay bound to its initial
/// child HWND; no foreground lookup, activation, global cursor positioning or SendInput fallback.</summary>
internal static class ClientMouse
{
    [DllImport("user32")] private static extern nint ChildWindowFromPointEx(nint parent, W.POINT pt, uint flags);
    [DllImport("user32")] private static extern int GetWindowLongW(nint h, int index);
    [DllImport("user32", ExactSpelling = true)] private static extern nint SendMessageTimeoutW(nint h, uint msg, nuint w, nint l, uint flags, uint ms, out nuint result);

    internal static bool Handles(FlowStep s) => s.Type switch
    {
        "drag" => s.From?.Space == FlowSpace.Client || s.To?.Space == FlowSpace.Client,
        "click" or "move" or "wheel" or "mousehold" or "mousedown" or "mouseup" => s.At?.Space == FlowSpace.Client,
        "hold" => !string.IsNullOrEmpty(s.Button) && s.At?.Space == FlowSpace.Client,
        _ => false,
    };

    private static W.POINT Translate(nint root, nint dest, FlowPoint p)
    {
        if (!W.GetClientRect(root, out var bounds) || p.X < 0 || p.Y < 0 || p.X >= bounds.Width || p.Y >= bounds.Height)
            throw new CommandFailure(4, "坐标超出当前客户区；没有改用屏幕坐标。");
        var pt = new W.POINT { X = p.X, Y = p.Y };
        if (root != dest && (!W.ClientToScreen(root, ref pt) || (dest != 0 && !W.ScreenToClient(dest, ref pt))))
            throw new CommandFailure(4, "目标窗口坐标无法转换。");
        return pt;
    }
    private static nint Pack(W.POINT pt) => pt.X is < short.MinValue or > short.MaxValue || pt.Y is < short.MinValue or > short.MaxValue
        ? throw new CommandFailure(4, "坐标超出 Windows 鼠标消息范围。") : unchecked((int)((ushort)pt.X | (uint)(ushort)pt.Y << 16));

    internal static int Execute(FlowStep s)
    {
        var start = s.Type == "drag" ? s.From : s.At;
        var end = s.Type == "drag" ? s.To : start;
        if (start?.Space != FlowSpace.Client || end?.Space != FlowSpace.Client || s.Target?.Selector().Length is null or 0)
            throw new CommandFailure(2, "后台客户区操作需要目标窗口，拖动两端必须都在客户区。");
        var resolution = WindowResolver.ResolveClient(CommandTarget.SelectorOf(s.Target));
        if (resolution.Usable.Count != 1) throw new CommandFailure(resolution.Matched.Count == 1 ? 4 : 3,
            "后台目标不存在、不可用或不唯一。请核对进程、窗口类与标题。");
        var root = resolution.Usable[0];
        if (s.ReadWindow != 0 && (s.ReadWindow != root.Handle || s.ReadPid != root.ProcessId)) throw new CommandFailure(4, "OCR 后目标窗口已变化，未点击。");
        var request = new BackgroundRequest("invoke", checked((int)root.ProcessId), root.Handle, null, null, null);
        try { new BackgroundTarget(request).CheckPeer(); }
        catch (CommandFailure) { throw; }
        catch (Exception e) { throw new CommandFailure(4, "不能验证目标权限：" + e.Message); }
        nint child = root.Handle;
        void Check()
        {
            if (W.Root(child) != root.Handle || !W.IsWindowVisible(child) || !W.IsWindowEnabled(child) || !W.IsWindowEnabled(root.Handle) ||
                W.GetWindowThreadProcessId(root.Handle, out var owner) == 0 || owner != root.ProcessId ||
                W.GetWindowThreadProcessId(child, out owner) == 0 || owner != root.ProcessId) throw new CommandFailure(4, "后台操作目标已变化或不可用。");
        }
        for (int depth = 0; ; depth++)
        {
            Check();
            var next = ChildWindowFromPointEx(child, Translate(root.Handle, child, start), 1 | 4 /* skip invisible, transparent; refuse disabled */);
            if (next == 0) throw new CommandFailure(4, "客户区落点不再属于目标。");
            if (next == child) break;
            if (depth >= 64) throw new CommandFailure(4, "子窗口嵌套过深。");
            child = next;
        }
        bool wheel = s.Type == "wheel";
        var first = Pack(Translate(root.Handle, wheel ? 0 : child, start));
        var last = Pack(Translate(root.Handle, wheel ? 0 : child, end));
        (uint down, uint up, uint mask, uint high) = (s.Button ?? "left").ToLowerInvariant() switch
        {
            "left" => (0x201u, 0x202u, 1u, 0u), "right" => (0x204u, 0x205u, 2u, 0u), "middle" => (0x207u, 0x208u, 16u, 0u),
            "x1" => (0x20Bu, 0x20Cu, 32u, 1u << 16), "x2" => (0x20Bu, 0x20Cu, 64u, 2u << 16), _ => throw new CommandFailure(2, "未知鼠标键"),
        };
        int delta = s.Delta ?? 120;
        if (wheel && (delta is < short.MinValue or > short.MaxValue || s.Axis is not (null or "horizontal" or "vertical"))) throw new CommandFailure(2, "滚轮方向或增量无法表示。");
        if (ExecutionMode.DryRun) { ExecutionMode.NoteSuppressed(1); return 0; }
        string cls = W.GetClass(child);
        if (s.Type == "click" && mask == 1 && (cls == "Button" || cls.StartsWith("WindowsForms10.BUTTON.", StringComparison.OrdinalIgnoreCase)) && (GetWindowLongW(child, -16) & 15) is 0 or 1 or 11)
        {
            // Native/WinForms push buttons may validate the physical pointer on WM_LBUTTONUP.
            // Use the already-verified no-focus semantic path rather than faking a foreground click.
            var code = BackgroundCommand.RunRequest(request with { Control = child });
            if (code == 0) ExecutionMode.NoteInjected(1);
            return code;
        }
        bool attempted = false;
        nint previousFront = W.GetForegroundWindow();
        bool wasMinimized = W.IsIconic(root.Handle);
        void Send(uint msg, uint flags, nint pos)
        {
            Check(); attempted = true;
            if (SendMessageTimeoutW(child, msg, flags, pos, 0x22, 500, out _) == 0) throw new IOException("目标没有确认鼠标消息。");
            ExecutionMode.NoteInjected(1);
        }
        void Delay(int ms)
        {
            for (; ms > 0; ms -= 25) { if (Execution.Cancelled) throw new OperationCanceledException(); Thread.Sleep(Math.Min(ms, 25)); }
        }
        try
        {
            if (wheel) Send(s.Axis == "horizontal" ? 0x20Eu : 0x20Au, (uint)(ushort)delta << 16, first);
            else if (s.Type == "move") Send(0x200, 0, first);
            else if (s.Type == "mousedown") Send(down, high | mask, first);
            else if (s.Type == "mouseup") Send(up, high, first);
            else
            {
                Send(down, high | mask, first);
                nint pos = first;
                try
                {
                    if (s.Type == "drag")
                    {
                        var clock = Stopwatch.StartNew();
                        for (int i = 1; i <= 20; i++)
                        {
                            Delay((int)Math.Max(0, (long)(s.DurationMs ?? 400) * i / 20 - clock.ElapsedMilliseconds));
                            var point = new FlowPoint { X = start.X + (end.X - start.X) * i / 20, Y = start.Y + (end.Y - start.Y) * i / 20 };
                            pos = Pack(Translate(root.Handle, child, point)); Send(0x200, mask, pos);
                        }
                    }
                    else if (s.Type is "mousehold" or "hold") Delay(s.Ms ?? 0);
                }
                finally { Send(up, high, pos); }
            }
            if ((previousFront != root.Handle && W.GetForegroundWindow() == root.Handle) ||
                (wasMinimized && W.IsWindow(root.Handle) && !W.IsIconic(root.Handle)))
                throw new InvalidOperationException("观察到目标在执行期间成为前台或被还原。");
            CommandOutput.Out.WriteLine($"后台 {s.Type}：已向目标客户区提交消息。");
            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) when (attempted) { throw new CommandFailure(1, "后台动作可能已部分执行，不可自动重试：" + e.Message); }
    }
}
