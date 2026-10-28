namespace KeyMouse.Windowing;
/// <summary>
/// Snapshot of one top-level window. The cheap identity facts (handle, pid, title, class)
/// are read up front; everything else is resolved from the handle on first read and cached.
/// That matters because a desktop has ~400 top-level windows and a selector is normally
/// matched against all of them: four API calls per window instead of a dozen is the
/// difference between ~200 ms and ~30 ms for every command that names a window.
/// </summary>
// 窗口快照，快速获取窗口的基本信息（句柄、进程 ID、标题、类名），其他属性在首次访问时从句柄解析并缓存。
internal sealed class WindowInfo
{
    // 必填属性
    public required IntPtr Handle { get; init; }
    // Title、ClassName、ProcessId 由 Capture 一次性读取
    // 窗口标题（可见文本）
    public string Title { get; init; } = "";
    // 类名（窗口类名）
    public string ClassName { get; init; } = "";
    // 进程ID（pid）
    public uint ProcessId { get; init; }
    ///////以下属性均为可选属性，首次访问时从句柄解析并缓存///////
    // 进程名称
    private string? _processName;
    public string ProcessName
    {
        get { _processName ??= NativeWindow.ProcessNameOf(ProcessId); return _processName; }
        init => _processName = value;
    }
    // 是否可见
    private bool? _visible;
    public bool Visible
    {
        get { _visible ??= NativeWindow.IsWindowVisible(Handle); return _visible.Value; }
        init => _visible = value;
    }
    // 是否最小化
    private bool? _minimized;
    public bool Minimized
    {
        get { _minimized ??= NativeWindow.IsIconic(Handle); return _minimized.Value; }
        init => _minimized = value;
    }
    // 	是否被 DWM 遮盖
    private bool? _cloaked;
    public bool Cloaked
    {
        get { _cloaked ??= NativeWindow.IsCloaked(Handle); return _cloaked.Value; }
        init => _cloaked = value;
    }
    // 是否启用（WS_DISABLED 为 false）
    private bool? _enabled;
    public bool Enabled
    {
        get { _enabled ??= NativeWindow.IsWindowEnabled(Handle); return _enabled.Value; }
        init => _enabled = value;
    }
    // 属主窗口，0 表示顶层主窗口
    private IntPtr? _owner;
    public IntPtr Owner
    {
        get { _owner ??= NativeWindow.GetOwner(Handle); return _owner.Value; }
        init => _owner = value;
    }
    // 窗口矩形
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
    ///////end///////
    
    // 响应探测往返耗时（毫秒）
    // 注：只有在 ResponseProbed 为 true 时才有意义。
    public long? ResponseMs { get; set; }
    // 是否已经探测过
    public bool ResponseProbed { get; set; }

    /// <summary>
    /// Reads the cheap identity facts. <paramref name="probeTimeoutMs"/> &gt; 0 additionally
    /// asks the window to answer WM_NULL, which is the only expensive bit callers opt into.
    /// </summary>
    // 拍摄快照（或者说：重新获取窗口的各种信息）
    public static WindowInfo Capture(IntPtr handle, uint probeTimeoutMs = 0)
    {
        // 获取pid
        NativeWindow.GetWindowThreadProcessId(handle, out uint pid);

        return new WindowInfo
        {
            Handle = handle,
            Title = NativeWindow.GetTitle(handle),
            ClassName = NativeWindow.GetClass(handle),
            ProcessId = pid,
            // 如果指定了探测超时时间，则需要额外进行一次探测
            ResponseMs = probeTimeoutMs > 0 ? NativeWindow.ResponseMs(handle, probeTimeoutMs) : null,
            ResponseProbed = probeTimeoutMs > 0
        };
    }

    // 用于附加响应时间信息，返回 this 方便链式调用
    public WindowInfo WithResponse(long? responseMs)
    {
        ResponseMs = responseMs;
        ResponseProbed = true;
        return this;
    }

    // 生成一句中文状态摘要
    // 注：会导致重新计算所有可选属性，所以在循环里调用时要注意性能。
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

    // 生成一句中文描述
    public string Describe() =>
        $"0x{Handle.ToInt64():X}  {ProcessName}({ProcessId})  \"{Title}\"  类名={ClassName}  [{StateSummary}]";

    // window list 的 格式化输出
    // 句柄固定 8 位十六进制。
    // 进程名宽度 22、标题截断到 42 宽、类名宽度 26。
    // 依赖 ConsoleText.Pad 和 ConsoleText.Truncate 做对齐和截断。
    public string TableRow() =>
        $"0x{Handle.ToInt64():X8}  " +
        ConsoleText.Pad(ProcessName, 22) +
        ConsoleText.Pad(ConsoleText.Truncate(Title, 42), 42) + " " +
        ConsoleText.Pad(ClassName, 26) + " " +
        StateSummary;
}
