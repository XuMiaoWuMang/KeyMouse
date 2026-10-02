namespace KeyMouse;

/// <summary>
/// Snapshot of one top-level window. The cheap identity facts (handle, pid, title, class)
/// are read up front; everything else is resolved from the handle on first read and cached.
/// That matters because a desktop has ~400 top-level windows and a selector is normally
/// matched against all of them: four API calls per window instead of a dozen is the
/// difference between ~200 ms and ~30 ms for every command that names a window.
/// </summary>
internal sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public string ClassName { get; init; } = "";
    public uint ProcessId { get; init; }

    private string? _processName;
    public string ProcessName
    {
        get { _processName ??= NativeWindow.ProcessNameOf(ProcessId); return _processName; }
        init => _processName = value;
    }

    private bool? _visible;
    public bool Visible
    {
        get { _visible ??= NativeWindow.IsWindowVisible(Handle); return _visible.Value; }
        init => _visible = value;
    }

    private bool? _minimized;
    public bool Minimized
    {
        get { _minimized ??= NativeWindow.IsIconic(Handle); return _minimized.Value; }
        init => _minimized = value;
    }

    private bool? _cloaked;
    public bool Cloaked
    {
        get { _cloaked ??= NativeWindow.IsCloaked(Handle); return _cloaked.Value; }
        init => _cloaked = value;
    }

    private bool? _enabled;
    /// <summary>False when the window is disabled (WS_DISABLED): it ignores input by design.</summary>
    public bool Enabled
    {
        get { _enabled ??= NativeWindow.IsWindowEnabled(Handle); return _enabled.Value; }
        init => _enabled = value;
    }

    private IntPtr? _owner;
    /// <summary>Owning window, or zero when this is a primary window (see NativeWindow.GetOwner).</summary>
    public IntPtr Owner
    {
        get { _owner ??= NativeWindow.GetOwner(Handle); return _owner.Value; }
        init => _owner = value;
    }

    private NativeWindow.RECT? _rect;
    public NativeWindow.RECT Rect
    {
        get
        {
            if (_rect is null)
            {
                NativeWindow.GetWindowRect(Handle, out var rect);
                _rect = rect;
            }
            return _rect.Value;
        }
        init => _rect = value;
    }

    /// <summary>Result of the WM_NULL round-trip: null means "no answer" only when <see cref="ResponseProbed"/> is true.</summary>
    public long? ResponseMs { get; set; }
    public bool ResponseProbed { get; set; }

    /// <summary>
    /// Reads the cheap identity facts. <paramref name="probeTimeoutMs"/> &gt; 0 additionally
    /// asks the window to answer WM_NULL, which is the only expensive bit callers opt into.
    /// </summary>
    public static WindowInfo Capture(IntPtr handle, uint probeTimeoutMs = 0)
    {
        NativeWindow.GetWindowThreadProcessId(handle, out uint pid);

        return new WindowInfo
        {
            Handle = handle,
            Title = NativeWindow.GetTitle(handle),
            ClassName = NativeWindow.GetClass(handle),
            ProcessId = pid,
            ResponseMs = probeTimeoutMs > 0 ? NativeWindow.ResponseMs(handle, probeTimeoutMs) : null,
            ResponseProbed = probeTimeoutMs > 0
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
