namespace KeyMouse;

/// <summary>Snapshot of one top-level window plus the facts the eligibility gate needs.</summary>
internal sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public string ClassName { get; init; } = "";
    public uint ProcessId { get; init; }
    public string ProcessName { get; init; } = "?";
    public bool Visible { get; init; }
    public bool Minimized { get; init; }
    public bool Cloaked { get; init; }

    /// <summary>Result of the WM_NULL round-trip: null means "no answer" only when <see cref="ResponseProbed"/> is true.</summary>
    public long? ResponseMs { get; set; }
    public bool ResponseProbed { get; set; }

    public NativeWindow.RECT Rect { get; init; }

    /// <summary>
    /// Reads every property in one go. <paramref name="probeTimeoutMs"/> = 0 skips the
    /// WM_NULL round-trip (used when a previous snapshot already answered it).
    /// </summary>
    public static WindowInfo Capture(IntPtr handle, uint probeTimeoutMs = 0)
    {
        NativeWindow.GetWindowThreadProcessId(handle, out uint pid);

        string processName = "?";
        try { processName = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { /* protected or exited process */ }

        NativeWindow.GetWindowRect(handle, out var rect);

        return new WindowInfo
        {
            Handle = handle,
            Title = NativeWindow.GetTitle(handle),
            ClassName = NativeWindow.GetClass(handle),
            ProcessId = pid,
            ProcessName = processName,
            Visible = NativeWindow.IsWindowVisible(handle),
            Minimized = NativeWindow.IsIconic(handle),
            Cloaked = NativeWindow.IsCloaked(handle),
            ResponseMs = probeTimeoutMs > 0 ? NativeWindow.ResponseMs(handle, probeTimeoutMs) : null,
            ResponseProbed = probeTimeoutMs > 0,
            Rect = rect
        };
    }

    /// <summary>Stores a probe performed later (the gate probes what the cheap enumeration skipped).</summary>
    public WindowInfo WithResponse(long? responseMs)
    {
        ResponseMs = responseMs;
        ResponseProbed = true;
        return this;
    }

    public string StateSummary
    {
        get
        {
            var flags = new List<string>();
            flags.Add(Visible ? "visible" : "HIDDEN");
            if (Minimized) flags.Add("minimized");
            if (Cloaked) flags.Add("cloaked");
            flags.Add(!ResponseProbed ? "unprobed" : ResponseMs is null ? "NO-RESPONSE" : $"wm_null={ResponseMs}ms");
            return string.Join(", ", flags);
        }
    }

    public string Describe() =>
        $"0x{Handle.ToInt64():X}  {ProcessName}({ProcessId})  \"{Title}\"  class={ClassName}  [{StateSummary}]";

    /// <summary>One row for `window list`.</summary>
    public string TableRow() =>
        $"0x{Handle.ToInt64():X8}  {ProcessName,-20} {Truncate(Title, 42),-42} {ClassName,-26} {StateSummary}";

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
