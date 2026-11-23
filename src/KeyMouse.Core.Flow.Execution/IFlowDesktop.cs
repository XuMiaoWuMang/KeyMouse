namespace KeyMouse.Flow;

/// <summary>All observational desktop operations used by the interpreter. Passing a fixture is local
/// to a single run, including nested calls. Input is a separate ExecuteFlowCommand dependency.</summary>
internal interface IFlowDesktop
{
    WindowResolution Resolve(WindowSelector selector, bool allowRestore);
    WindowResolution ResolveClient(WindowSelector selector) => Resolve(selector, false);
    bool IsFront(string process);
    (int X, int Y)? WindowOrigin(WindowInfo window);
    void Validate(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect);
    (string Text, double? Confidence, LineInfo[] Lines) Read(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect);
}

internal sealed class NativeFlowDesktop : IFlowDesktop
{
    internal static readonly NativeFlowDesktop Instance = new();
    public WindowResolution Resolve(WindowSelector selector, bool allowRestore) => WindowResolver.Resolve(selector, allowRestore);
    public WindowResolution ResolveClient(WindowSelector selector) => WindowResolver.ResolveClient(selector);
    public bool IsFront(string process)
    {
        var handle = NativeWindow.GetForegroundWindow();
        return handle != IntPtr.Zero && NativeWindow.GetWindowThreadProcessId(handle, out uint pid) != 0 &&
            pid != 0 && process.Equals(NativeWindow.ProcessNameOf(pid), StringComparison.OrdinalIgnoreCase);
    }
    public (int X, int Y)? WindowOrigin(WindowInfo window) => NativeWindow.GetWindowRect(window.Handle, out var rect) ? (rect.Left, rect.Top) : null;
    public void Validate(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect)
    {
        if (window == null) Probe.ValidateScreenRegion(rect); else Probe.ValidateRegion(window, windowSpace, rect);
    }
    public (string Text, double? Confidence, LineInfo[] Lines) Read(WindowInfo? window, bool windowSpace, (int X, int Y, int W, int H) rect) =>
        window == null ? Probe.ReadOnceScreen(rect) : Probe.ReadOnce(window, windowSpace, rect, windowSpace ? "screen" : "print");
}
