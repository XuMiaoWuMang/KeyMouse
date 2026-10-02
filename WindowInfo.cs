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

    /// <summary>Owning window, or zero when this is a primary window (see NativeWindow.GetOwner).</summary>
    public IntPtr Owner { get; init; }

    /// <summary>False when the window is disabled (WS_DISABLED): it ignores input by design.</summary>
    public bool Enabled { get; init; } = true;

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
            Owner = NativeWindow.GetOwner(handle),
            Enabled = NativeWindow.IsWindowEnabled(handle),
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
            flags.Add(Visible ? "可见" : "隐藏");
            if (Minimized) flags.Add("最小化");
            if (Cloaked) flags.Add("已遮盖");
            if (Owner != IntPtr.Zero) flags.Add($"属主=0x{Owner.ToInt64():X}");
            if (!Enabled) flags.Add("已禁用");
            flags.Add(!ResponseProbed ? "未探测" : ResponseMs is null ? "无响应" : $"响应={ResponseMs}ms");
            return string.Join("，", flags);
        }
    }

    public string Describe() =>
        $"0x{Handle.ToInt64():X}  {ProcessName}({ProcessId})  \"{Title}\"  类名={ClassName}  [{StateSummary}]";

    /// <summary>One row for `window list`.</summary>
    public string TableRow() =>
        $"0x{Handle.ToInt64():X8}  " +
        ConsoleText.Pad(ProcessName, 22) +
        ConsoleText.Pad(ConsoleText.Truncate(Title, 42), 42) + " " +
        ConsoleText.Pad(ClassName, 26) + " " +
        StateSummary;
}
