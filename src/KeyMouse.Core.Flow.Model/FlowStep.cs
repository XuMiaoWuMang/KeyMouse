namespace KeyMouse.Flow;

/// <summary>Where a step's coordinates are measured. Screen is the fallback for points that belong
/// to no window (a title bar click, the desktop): replayable only if the window is where it was.</summary>
internal static class FlowSpace
{
    internal const string Client = "client";
    internal const string Screen = "screen";

    /// <summary>整个窗口（含标题栏与边框）。读屏时用得上：标题栏那类区域不属于客户区。</summary>
    internal const string Window = "window";
}

/// <summary>滚轮的方向。竖向是默认，所以只有横向需要在文件里写出来。</summary>
internal static class FlowAxis
{
    internal const string Vertical = "vertical";
    internal const string Horizontal = "horizontal";
}

/// <summary>The window a step applies to. Process and class are the durable selector; the title is
/// recorded for the human (and for the editor) because titles change.</summary>
internal sealed class FlowTarget
{
    public string? Process { get; set; }
    public string? Class { get; set; }
    public string? Title { get; set; }

    internal string[] Selector()
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(Process)) { args.Add("--process"); args.Add(Process!); }
        if (!string.IsNullOrWhiteSpace(Class)) { args.Add("--class"); args.Add(Class!); }
        return args.ToArray();
    }
}

internal sealed class FlowPoint
{
    /// <summary>
    /// 默认客户区相对：不写 space 就是「这个窗口的客户区里 x,y」，必须给 target。
    /// 屏幕坐标仍然支持，但要显式写 `"space":"screen"`——录制器就是这么写的。
    /// </summary>
    public string Space { get; set; } = FlowSpace.Client;
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>A rectangle a condition reads, in the same vocabulary `probe` uses.</summary>
internal sealed class FlowRegion
{
    /// <summary>
    /// client（客户区）、window（整个窗口）或 screen（屏幕绝对坐标，不牵扯窗口）。
    /// screen 是 2.2.4 之后加的：不填 target 也能读桌面上一块，所以 target 变成条件必填。
    /// </summary>
    public string Space { get; set; } = FlowSpace.Client;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    internal (int X, int Y, int W, int H) Rect() => (X, Y, Width, Height);
}

/// <summary>
/// One recorded or hand-written step. The shape is deliberately flat and nullable: a JSON document
/// a human edits by hand has to survive missing fields, and the editor writes back only what a step
/// actually uses. Unknown step types are rejected on load, not at replay time.
/// </summary>
/// <summary>
/// A step's precondition: the step runs only when this text is on screen in time. It is the tool's
/// one conditional, deliberately shaped like the other text conditions instead of introducing a block
/// grammar - `else` says what happens when it does not hold (skip the step, or fail with exit 3).
/// </summary>
internal sealed class FlowCondition
{
    /// <summary>Which window to read. Falls back to the step's own target, so a condition on a step
    /// that names a window does not have to repeat it - and a condition may deliberately watch a
    /// different window than the one the step acts on.</summary>
    public FlowTarget? Target { get; set; }
    public FlowRegion? Region { get; set; }
    public string? Text { get; set; }
    public string? Match { get; set; }
    public int? MaxErrors { get; set; }
    public int? TimeoutMs { get; set; }
    public int? IntervalMs { get; set; }
    public int? Confirm { get; set; }

    /// <summary>skip (default) or fail.</summary>
    public string? Else { get; set; }
}

/// <summary>
/// Where a block's predicates look. `text()` reads this one region; both target and region are
/// required whenever a condition uses `text()` - the engine never guesses which window to watch.
/// </summary>
internal sealed class FlowProbe
{
    public FlowTarget? Target { get; set; }
    public FlowRegion? Region { get; set; }
    public string? Match { get; set; }
    public int? MaxErrors { get; set; }
}

internal sealed class FlowStep
{
    // Runtime-only binding between click-text's OCR frame and its click. Never serialized.
    internal nint ReadWindow { get; set; }
    internal uint ReadPid { get; set; }
    private string? _text;

    public string Type { get; set; } = "";

    /// <summary>
    /// 要输入的文字。换行**只允许 \n**：WinUI 的 TextBox 存的是裸 \r，Windows 文件里是 \r\n，
    /// 两者直接写进流程文件后，引擎会把 \r 当作 CRLF 的前半个字符跳掉——"编辑器里明明三行，打出来一行"。
    /// 在模型这一层折平，读入、编辑、保存三条路就都干净了。
    /// </summary>
    public string? Text
    {
        get => _text;
        set => _text = value?.Replace("\r\n", "\n").Replace('\r', '\n');
    }
    public FlowTarget? Target { get; set; }
    public FlowRegion? Region { get; set; }
    public FlowCondition? When { get; set; }
    public FlowPoint? At { get; set; }
    public FlowPoint? From { get; set; }
    public FlowPoint? To { get; set; }
    public string? Button { get; set; }
    public string? Combo { get; set; }
    public int? IntervalMs { get; set; }
    public int? TimeoutMs { get; set; }
    public string? Match { get; set; }
    public int? MaxErrors { get; set; }
    public int? Confirm { get; set; }
    public int? Delta { get; set; }

    /// <summary>`wheel`: vertical（默认，可不写）或 horizontal。</summary>
    public string? Axis { get; set; }
    public int? Ms { get; set; }
    public int? DurationMs { get; set; }

    /// <summary>执行前的相对等待（毫秒）；0 表示立即执行。</summary>
    public int? DelayMs { get; set; }

    /// <summary>旧录制的绝对时间戳。只有没指定 DelayMs 时才按原时间线调度。</summary>
    public int? AtMs { get; set; }
    public string? Shot { get; set; }
    public string? Note { get; set; }

    /// <summary>Nested steps of a `repeat` / `foreach` group. Groups are the only steps that carry
    /// steps of their own, so the format stays two levels deep and linear everywhere else.</summary>
    public List<FlowStep>? Steps { get; set; }

    /// <summary>`foreach`: the variable that holds the list to walk.</summary>
    public string? In { get; set; }

    /// <summary>`repeat`: how many times.</summary>
    public int? Times { get; set; }

    /// <summary>`read-text`: the variable to store what was read into.</summary>
    public string? Into { get; set; }

    /// <summary>`call`: the subflow file, relative to the file that calls it.</summary>
    public string? Flow { get; set; }

    /// <summary>`call`: values handed to the subflow (each may use `{{...}}` from the caller).</summary>
    public Dictionary<string, string>? Vars { get; set; }

    /// <summary>`call`: names the subflow hands back to the caller once it has run.</summary>
    public string[]? Export { get; set; }

    // ---------------------------------------------------------------- control flow

    /// <summary>
    /// `if` / `while` / `do` / `for` / `assert`: the condition, written in the small expression language
    /// (see the expression language reference). Bare variable names, no <c>{{...}}</c>.
    /// </summary>
    public string? Cond { get; set; }

    /// <summary>Where <c>text()</c> inside <see cref="Cond"/> reads. Required exactly when the condition uses it.</summary>
    public FlowProbe? Probe { get; set; }

    /// <summary>
    /// `while` / `do` / `for`: how many rounds the loop may take before the flow stops with exit 3.
    /// Defaults: 200 for the waiting loops, 10000 for `for`; the contract caps both at 10000.
    /// </summary>
    public int? MaxRounds { get; set; }

    /// <summary>`if`: the other branch. An `else if` is an `if` nested here, exactly like C.</summary>
    public List<FlowStep>? Else { get; set; }

    /// <summary>`for`: names to evaluate once before the loop, in order (name → expression).</summary>
    public Dictionary<string, string>? Init { get; set; }

    /// <summary>`for`: names to evaluate after each round, in order (name → expression).</summary>
    public Dictionary<string, string>? Step { get; set; }

    /// <summary>`set`: the variable to write.</summary>
    public string? Name { get; set; }

    /// <summary>`set`: the expression to store.</summary>
    public string? Value { get; set; }

    /// <summary>`assert`: what to say when the assertion is false.</summary>
    public string? Message { get; set; }
}
