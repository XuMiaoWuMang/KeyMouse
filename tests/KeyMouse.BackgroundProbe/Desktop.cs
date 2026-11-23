using System.Runtime.InteropServices;
using BackgroundUIA;

namespace KeyMouse.BackgroundProbe;

internal sealed class DesktopChanged(string message) : Exception(message);

internal static class Desktop
{
    [DllImport("user32")] internal static extern nint GetForegroundWindow();
    [DllImport("user32")] private static extern bool GetCursorPos(out Point pt);
    [DllImport("user32")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32", SetLastError = true, ExactSpelling = true)]
    private static extern nint SendMessageTimeoutW(nint hwnd, uint msg, nuint wParam, nint lParam,
        uint flags, uint timeout, out nuint result);

    internal static (nint Foreground, Point Cursor) Sample()
    {
        var front = GetForegroundWindow();
        if (front == 0 || !GetCursorPos(out var pt)) throw new DesktopChanged("no observable foreground or cursor; use an unlocked interactive desktop");
        return (front, pt);
    }

    private static bool Owned(long handle, int pid) =>
        GetWindowThreadProcessId((nint)handle, out var owner) != 0 && owner == pid;

    // Only handles announced by our one fixture process. No title lookup, foreground
    // substitution, screen hit-testing, focus manipulation or global input fallback.
    internal static bool LiveChild(int pid, long root, long child) =>
        Owned(root, pid) && Owned(child, pid) && IsChild((nint)root, (nint)child);

    internal static IUIAutomation2 NoFocusClient()
    {
        var client = (IUIAutomation2)new CUIAutomation8();
        client.AutoSetFocus = 0;
        if (client.AutoSetFocus != 0) throw new InvalidOperationException("UIA automatic focus is not disabled");
        client.ConnectionTimeout = client.TransactionTimeout = 1000;
        return client;
    }

    internal static string Pattern(IUIAutomation2 client, int pid, long root, long child, string? value = null)
    {
        if (!LiveChild(pid, root, child)) return "invalid-target";
        var element = client.ElementFromHandle((nint)child);
        if (element.CurrentProcessId != pid) throw new InvalidOperationException("Unexpected UIA provider owner");
        if (value is not null)
        {
            if (element.GetCurrentPattern(10002 /* UIA_ValuePatternId */) is not IUIAutomationValuePattern pattern) return "unsupported";
            pattern.SetValue(value);
        }
        else
        {
            if (element.GetCurrentPattern(10000 /* UIA_InvokePatternId */) is not IUIAutomationInvokePattern pattern) return "unsupported";
            pattern.Invoke();
        }
        return "dispatched";
    }

    internal static void EditMessage(int pid, Handles h, uint message, nuint key, nint keyData)
    {
        if (!LiveChild(pid, h.Root, h.Edit)) throw new InvalidOperationException("Target edit no longer exists");
        if (SendMessageTimeoutW((nint)h.Edit, message, key, keyData, 0x22 /* ABORTIFHUNG | ERRORONEXIT */,
                1000, out _) == 0) throw new InvalidOperationException("Edit did not handle message within deadline");
    }
}
