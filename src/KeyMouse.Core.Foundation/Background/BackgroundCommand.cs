using System.Runtime.InteropServices;
using System.Text.Json;
using BackgroundUIA;
using Window = KeyMouse.Windowing.NativeWindow;

namespace KeyMouse.Input;

internal static class BackgroundCommand
{
    private const uint AbortOnHungOrExit = 0x22;
    [DllImport("user32", ExactSpelling = true)] private static extern nint SendMessageTimeoutW(nint h, uint msg, nuint w, nint l, uint flags, uint timeout, out nuint result);
    [DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern nint SendMessageTimeoutW(nint h, uint msg, nuint w, string l, uint flags, uint timeout, out nuint result);

    // 0 = preflight, 1 = mutation may have begun, 2 = expired before dispatch.
    // A late-returning provider must not cause a mutation after a preflight timeout.
    private sealed class Dispatch
    {
        private int state;
        internal void Begin()
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) throw new TimeoutException();
        }
        internal bool Close() => Interlocked.CompareExchange(ref state, 2, 0) == 1;
    }

    internal static int Run(string[] args)
    {
        ConsoleText.ConfigureOutputEncoding();
        if (args is ["--help"] or ["help"] or ["-h"]) { Console.WriteLine(BackgroundRequest.Help); return 0; }
        BackgroundRequest request;
        try { request = BackgroundRequest.Parse(args); }
        catch (ArgumentException ex) { return CommandLine.Fail(2, ex.Message); }
        return RunRequest(request);
    }

    internal static int RunRequest(BackgroundRequest request)
    {
        var dispatch = new Dispatch();
        try
        {
            // UIA runs on an MTA background thread; the deadline covers enumeration and provider calls too.
            string result = Task.Run(() => Execute(request, dispatch)).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            CommandOutput.Out.WriteLine(result);
            return 0;
        }
        catch (Exception ex)
        {
            if (dispatch.Close()) return CommandLine.Fail(1, "后台操作可能已经执行，结果未确认；请检查目标状态，不要自动重试。" + ex.Message);
            return CommandLine.Fail(ex is CommandFailure f ? f.Code : 4, "未执行后台动作：" + ex.Message);
        }
    }

    private static string Execute(BackgroundRequest r, Dispatch dispatch)
    {
        var target = new BackgroundTarget(r);
        target.CheckPeer();
        var client = (IUIAutomation2)new CUIAutomation8();
        client.AutoSetFocus = 0;
        if (client.AutoSetFocus != 0) throw new CommandFailure(4, "无法关闭 UIA 自动聚焦。");
        client.ConnectionTimeout = client.TransactionTimeout = 1000;
        var controls = target.Find(client);
        static string Hex(nint h) => $"0x{h:X}";
        if (r.Action == "controls") return JsonSerializer.Serialize(new { pid = r.Pid, hwnd = Hex(r.Root),
            controls = controls.Select(c => new { control = Hex(c.Handle), kind = c.Kind, id = c.Id }) });
        if (controls.Count != 1) throw new CommandFailure(controls.Count == 0 && r.Control.HasValue ? 4 : 3,
            $"匹配到 {controls.Count} 个支持的控件，需要唯一目标。请重新运行 background controls。");
        var c = controls[0];
        nint previousFront = Window.GetForegroundWindow();
        bool wasMinimized = Window.IsIconic(r.Root);
        string Finish(string message)
        {
            // Never try to undo a focus change: that would interrupt the user a second time.
            var front = Window.GetForegroundWindow();
            if ((front != previousFront && front == r.Root) ||
                (wasMinimized && Window.IsWindow(r.Root) && !Window.IsIconic(r.Root)))
                throw new InvalidOperationException("观察到目标在执行期间成为前台或被还原。");
            return message;
        }
        target.CheckAction(c);
        if (r.Action == "invoke")
        {
            if (c.Element.GetCurrentPattern(10000) is not IUIAutomationInvokePattern invoke) throw new CommandFailure(4, "按钮不提供 InvokePattern。");
            target.CheckAction(c);
            dispatch.Begin(); invoke.Invoke();
            return Finish("已向按钮提交 Invoke；应用的后续业务结果需自行确认。");
        }
        if (c.Element.GetCurrentPattern(10002) is not IUIAutomationValuePattern value || value.CurrentIsReadOnly != 0)
            throw new CommandFailure(4, "编辑框不支持可写 ValuePattern。");
        string expected = r.Text!;
        if (r.Action == "insert-text")
        {
            // EM_REPLACESEL is one edit, not a sequence of keystrokes. EM_GETSEL packs two 16-bit indices.
            string before = value.CurrentValue;
            if (before.Length >= ushort.MaxValue || SendMessageTimeoutW(c.Handle, 0x00B0, 0, 0, AbortOnHungOrExit, 1000, out var selection) == 0)
                throw new CommandFailure(4, "无法可靠读取编辑框选区，未写入。");
            int start = (int)(selection & 0xffff), end = (int)((selection >> 16) & 0xffff);
            if (start > end || end > before.Length) throw new CommandFailure(4, "选区越界，未写入。");
            expected = string.Concat(before.AsSpan(0, start), r.Text, before.AsSpan(end));
            target.CheckAction(c);
            dispatch.Begin();
            if (SendMessageTimeoutW(c.Handle, 0x00C2, 1, r.Text!, AbortOnHungOrExit, 1000, out _) == 0)
                throw new TimeoutException("编辑框未确认替换选区。");
        }
        else
        {
            target.CheckAction(c);
            dispatch.Begin(); value.SetValue(expected);
        }
        if (value.CurrentValue != expected) throw new InvalidOperationException("回读的文本与预期不同。");
        return Finish("文本已写入并回读确认。");
    }
}
