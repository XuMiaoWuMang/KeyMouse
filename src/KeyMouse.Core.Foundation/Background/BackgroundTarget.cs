using System.Runtime.InteropServices;
using BackgroundUIA;
using Window = KeyMouse.Windowing.NativeWindow;

namespace KeyMouse.Input;

// Only native HWND-backed Edit and push Button controls (including their WinForms wrappers).
// Never search the desktop UIA tree, use screen points, or ask a control to take focus.
internal sealed class BackgroundTarget(BackgroundRequest request)
{
    private const int GwlStyle = -16, EsPassword = 0x20, EsReadOnly = 0x800, EsMultiline = 4;
    [DllImport("user32", ExactSpelling = true)] private static extern bool EnumChildWindows(nint root, Window.EnumWindowsProc visit, nint param);
    [DllImport("user32", ExactSpelling = true)] private static extern int GetWindowLongW(nint hwnd, int index);
    [DllImport("user32", ExactSpelling = true)] private static extern bool IsChild(nint root, nint child);

    internal sealed record Control(nint Handle, string Kind, string Id, IUIAutomationElement Element);

    private static bool Owned(nint h, int pid) => Window.GetWindowThreadProcessId(h, out uint owner) != 0 && owner == pid;
    internal void CheckRoot()
    {
        if (!Owned(request.Root, request.Pid) || Window.Root(request.Root) != request.Root ||
            !Window.IsWindowVisible(request.Root) || !Window.IsWindowEnabled(request.Root) || Window.IsCloaked(request.Root))
            throw new CommandFailure(4, "顶层窗口已失效、不属于指定进程、不可见或已禁用。请重新列举窗口。");
    }

    internal void CheckPeer()
    {
        CheckRoot();
        var self = TokenCapabilities.Read(Environment.ProcessId);
        var peer = TokenCapabilities.Read(request.Pid);
        if (self.UserSid != peer.UserSid || self.SessionId != peer.SessionId || self.IntegrityRid != peer.IntegrityRid || self.UiAccess != peer.UiAccess)
            throw new CommandFailure(4, "后台动作仅支持同用户、同会话、同完整性级别及 UIAccess 状态的窗口。");
    }

    private string? Kind(nint h)
    {
        if (!Owned(h, request.Pid) || !IsChild(request.Root, h) || Window.Root(h) != request.Root) return null;
        string cls = Window.GetClass(h);
        bool Matches(string name) => cls.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            cls.StartsWith($"WindowsForms10.{name}.", StringComparison.OrdinalIgnoreCase);
        int style = GetWindowLongW(h, GwlStyle);
        if (Matches("Edit") && (style & EsPassword) == 0) return "edit";
        int buttonStyle = style & 0xf;
        // WinForms Button can be BS_OWNERDRAW. Still require UIA Button (not CheckBox/Radio)
        // and require InvokePattern before dispatch.
        if (Matches("Button") && (buttonStyle is 0 or 1 || (buttonStyle == 11 && cls.StartsWith("WindowsForms10.", StringComparison.OrdinalIgnoreCase))))
            return "button";
        return null;
    }

    internal List<Control> Find(IUIAutomation2 client)
    {
        List<nint> candidates = [];
        if (request.Control is nint exact) candidates.Add(exact);
        else
        {
            // Don't call COM or throw on an unmanaged callback stack. Never act on a truncated list.
            EnumChildWindows(request.Root, (h, _) => { candidates.Add(h); return candidates.Count <= 4096; }, 0);
            if (candidates.Count > 4096) throw new CommandFailure(4, "子窗口太多，请使用明确的 --control 句柄。");
        }
        List<Control> matches = [];
        foreach (var h in candidates)
        {
            string? kind = Kind(h);
            if (kind == null) continue;
            var element = client.ElementFromHandle(h);
            if (element.CurrentProcessId != request.Pid || element.CurrentNativeWindowHandle != h)
                throw new CommandFailure(4, "控件归属发生变化，请重新列举。");
            if (element.CurrentIsPassword != 0 || element.CurrentControlType != (kind == "edit" ? 50004 : 50000)) continue;
            string id = element.CurrentAutomationId ?? "";
            if (request.Id == null || request.Id == id) matches.Add(new(h, kind, id, element));
        }
        CheckRoot();
        return matches;
    }

    internal void CheckAction(Control c)
    {
        CheckRoot();
        if (Kind(c.Handle) != c.Kind || c.Element.CurrentNativeWindowHandle != c.Handle || c.Element.CurrentProcessId != request.Pid ||
            c.Element.CurrentAutomationId != c.Id || c.Element.CurrentIsEnabled == 0 || c.Element.CurrentIsPassword != 0 || !Window.IsWindowVisible(c.Handle))
            throw new CommandFailure(4, "控件已变化、隐藏、受保护或已禁用。");
        for (nint h = c.Handle; h != request.Root; h = Window.GetAncestor(h, 1 /* GA_PARENT */))
            if (h == 0 || !Window.IsWindowEnabled(h)) throw new CommandFailure(4, "控件或其父窗口不可用。");
        if (request.Action == "invoke" ? c.Kind != "button" : c.Kind != "edit") throw new CommandFailure(4, "该控件不支持此动作。");
        if (c.Kind != "edit") return;
        int style = GetWindowLongW(c.Handle, GwlStyle);
        if ((style & EsReadOnly) != 0) throw new CommandFailure(4, "编辑框只读。");
        if ((style & EsMultiline) == 0 && request.Text!.IndexOfAny(['\r', '\n']) >= 0) throw new CommandFailure(4, "不能向单行编辑框写入换行符。");
    }
}
