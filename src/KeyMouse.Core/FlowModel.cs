using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyMouse;

/// <summary>Where a step's coordinates are measured. Screen is the fallback for points that belong
/// to no window (a title bar click, the desktop): replayable only if the window is where it was.</summary>
internal static class FlowSpace
{
    internal const string Client = "client";
    internal const string Screen = "screen";
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
    public string Space { get; set; } = FlowSpace.Client;
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>A rectangle a condition reads, in the same vocabulary `probe` uses.</summary>
internal sealed class FlowRegion
{
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

internal sealed class FlowStep
{
    public string Type { get; set; } = "";
    public FlowTarget? Target { get; set; }
    public FlowRegion? Region { get; set; }
    public FlowCondition? When { get; set; }
    public FlowPoint? At { get; set; }
    public FlowPoint? From { get; set; }
    public FlowPoint? To { get; set; }
    public string? Button { get; set; }
    public string? Combo { get; set; }
    public string? Text { get; set; }
    public int? IntervalMs { get; set; }
    public int? TimeoutMs { get; set; }
    public string? Match { get; set; }
    public int? MaxErrors { get; set; }
    public int? Confirm { get; set; }
    public int? Delta { get; set; }
    public int? Ms { get; set; }
    public int? DurationMs { get; set; }
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
}

internal sealed class FlowOptions
{
    public int MinGapMs { get; set; } = 300;
    public int MoveThresholdPx { get; set; } = 8;
    public bool Shots { get; set; } = true;
}

internal sealed class FlowScreen
{
    public int Width { get; set; }
    public int Height { get; set; }
}

internal sealed class FlowDocument
{
    internal const string FormatName = "keymouse-flow";
    internal const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;
    public string? RecordedAt { get; set; }
    public FlowScreen? Screen { get; set; }
    public FlowOptions? Options { get; set; }
    public List<FlowStep> Steps { get; set; } = new();

    /// <summary>Document-level variables. A value is a string, or an array of strings when a
    /// `foreach` needs a list. `--set name=value` overrides a string one (and reduces a list to a
    /// single element, documented rather than surprising).</summary>
    public Dictionary<string, JsonElement>? Variables { get; set; }

    [JsonIgnore] internal Dictionary<string, string> Texts { get; } = new(StringComparer.Ordinal);
    [JsonIgnore] internal Dictionary<string, string[]> Lists { get; } = new(StringComparer.Ordinal);

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static FlowDocument Load(string path, IReadOnlyDictionary<string, string>? provided = null, IReadOnlyList<string>? stack = null, IEnumerable<string>? inherited = null)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CommandFailure(1, $"读不到流程 '{path}'：{ex.Message}");
        }

        FlowDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<FlowDocument>(text, Json);
        }
        catch (JsonException ex)
        {
            throw new CommandFailure(2, $"流程 '{path}' 不是合法 JSON：{ex.Message}");
        }

        if (document is null) throw new CommandFailure(2, $"流程 '{path}' 是空的");
        if (!string.Equals(document.Format, FormatName, StringComparison.Ordinal))
            throw new CommandFailure(2, $"'{path}' 的 format 是 '{document.Format}'，期望 '{FormatName}'");
        if (document.Version is < 1 or > CurrentVersion)
            throw new CommandFailure(2, $"'{path}' 的 version 是 {document.Version}，这个版本只认 1");

        foreach (var (name, value) in document.Variables ?? [])
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    document.Texts[name] = value.GetString() ?? "";
                    break;
                case JsonValueKind.Array:
                    document.Lists[name] = value.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .ToArray();
                    break;
                default:
                    throw new CommandFailure(2, $"变量 '{name}' 只能是字符串或字符串数组");
            }
        }

        // --set wins over the document, which is what makes one file reusable with other data.
        foreach (var (name, value) in provided ?? new Dictionary<string, string>())
        {
            document.Texts[name] = value;
            if (document.Lists.ContainsKey(name)) document.Lists[name] = [value];
        }

        // A subflow sees what its caller had (frames are chained, not copied), so the caller's names
        // are in scope while validating it too - otherwise a subflow that documents "call me with a
        // `who`" would be refused for using it.
        var scope = new HashSet<string>(document.Texts.Keys, StringComparer.Ordinal);
        foreach (string name in inherited ?? []) scope.Add(name);
        var calls = new CallContext(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".", stack ?? [], 0);

        for (int i = 0; i < document.Steps.Count; i++)
        {
            var step = document.Steps[i];
            if (step is null) throw new CommandFailure(2, $"第 {i + 1} 步是 null");
            if (!KnownTypes.Contains(step.Type))
            {
                throw new CommandFailure(2,
                    $"第 {i + 1} 步的类型 '{step.Type}' 不认识（可用：{string.Join(" | ", KnownTypes)}）");
            }

            // Conditions are validated when the file is loaded, not half-way through a replay: a
            // typo in a match mode should cost a second, not the five minutes the step might wait.
            if (step.When is { } when)
            {
                if (when.Region is null || string.IsNullOrEmpty(when.Text))
                    throw new CommandFailure(2, $"第 {i + 1} 步的 when 需要 region 与 text（前提是什么）");
                if (when.Match is { } conditionMode && !TextPredicate.IsKnownMode(conditionMode))
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步 when 的 match '{conditionMode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
                FlowTarget? conditionTarget = when.Target ?? step.Target;
                if (string.IsNullOrWhiteSpace(conditionTarget?.Process) && string.IsNullOrWhiteSpace(conditionTarget?.Class))
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步的 when 需要 target（自己给，或者步骤本身有）——否则不知道该读哪个窗口");
                if (when.Region.Space != FlowSpace.Client)
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步 when 的 region 只支持客户区坐标（space=client）");
                if (when.Else is { } otherwise && otherwise is not ("skip" or "fail"))
                    throw new CommandFailure(2, $"第 {i + 1} 步 when 的 else '{otherwise}' 不认识（可用：skip | fail）");
            }

            if (step.Type is "wait-text" or "click-text" or "read-text")
            {
                if (step.Region is null)
                    throw new CommandFailure(2, $"第 {i + 1} 步 {step.Type} 需要 region（读哪一块）");
                if (step.Match is { } mode && !TextPredicate.IsKnownMode(mode))
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步 {step.Type} 的 match '{mode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
                if (step.Region.Space != FlowSpace.Client)
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步 {step.Type} 的 region 只支持客户区坐标（space=client）：屏幕坐标会因为窗口移动而失效");
                if (step.Type is "wait-text" or "click-text" && string.IsNullOrEmpty(step.Text))
                    throw new CommandFailure(2, $"第 {i + 1} 步 {step.Type} 需要 text（等什么字）");
                if (step.Type == "read-text" && string.IsNullOrEmpty(step.Into))
                    throw new CommandFailure(2, $"第 {i + 1} 步 read-text 需要 into（读到的东西存进哪个变量）");
            }

            // Groups: a `repeat` needs a count, a `foreach` needs a list, and both need steps to run.
            if (step.Type == "repeat" && (step.Times is not int times || times < 0 || times > MaxLoopIterations))
                throw new CommandFailure(2, $"第 {i + 1} 步的 repeat 需要 times（0 到 {MaxLoopIterations} 之间）");
            if (step.Type is "repeat" or "foreach" && step.When is not null)
            {
                // Loops are flattened before execution, so a condition on the group itself would have
                // nowhere to live. Say so instead of quietly ignoring it.
                throw new CommandFailure(2,
                    $"第 {i + 1} 步的 {step.Type} 不能带 when：把前提写到里面的步骤上（循环体会各自判断）");
            }
            if (step.Type == "foreach")
            {
                if (string.IsNullOrEmpty(step.In))
                    throw new CommandFailure(2, $"第 {i + 1} 步的 foreach 需要 in（遍历哪个列表变量）");
                if (!document.Lists.ContainsKey(step.In!))
                    throw new CommandFailure(2,
                        $"第 {i + 1} 步的 foreach 遍历 '{step.In}'，但文档 variables 里没有这个列表");
            }

            CheckPlaceholders(step, scope, $"第 {i + 1} 步");

            // A read-text defines a variable for the steps after it, so the check above can stay a
            // straightforward forward pass instead of a guess about what a capture might produce.
            if (step.Type == "read-text" && !string.IsNullOrEmpty(step.Into)) scope.Add(step.Into!);

            // An export is the same promise one level out: the caller may use those names afterwards.
            if (step.Type == "call" && step.Export is { Length: > 0 })
            {
                foreach (string name in step.Export) scope.Add(name);
            }

            if (step.Type == "call") ValidateCall(step, $"第 {i + 1} 步", calls);

            if (step.Steps is { Count: > 0 })
            {
                var inner = new HashSet<string>(scope, StringComparer.Ordinal) { "index" };
                if (step.Type == "foreach") inner.Add("item");
                ValidateSteps(step.Steps, inner, $"第 {i + 1} 步里", calls);
            }
            else if (step.Type is "repeat" or "foreach")
            {
                throw new CommandFailure(2, $"第 {i + 1} 步的 {step.Type} 里没有步骤");
            }
        }

        return document;
    }

    /// <summary>
    /// The same checks for a group's children: a file is walked the way it will be executed, carrying
    /// the names in scope, so a `{{typo}}` is refused when the file is loaded - with the path to the
    /// step that used it - instead of half-way through a run.
    /// </summary>
    private static void ValidateSteps(List<FlowStep> steps, HashSet<string> scope, string path, CallContext calls)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            FlowStep step = steps[i] ?? throw new CommandFailure(2, $"{path}第 {i + 1} 步是 null");
            string where = $"{path}第 {i + 1} 步";

            if (!KnownTypes.Contains(step.Type))
                throw new CommandFailure(2, $"{where}的类型 '{step.Type}' 不认识（可用：{string.Join(" | ", KnownTypes)}）");
            if (step.When is { } when && (when.Region is null || string.IsNullOrEmpty(when.Text)))
                throw new CommandFailure(2, $"{where}的 when 需要 region 与 text（前提是什么）");
            if (step.When?.Match is { } conditionMode && !TextPredicate.IsKnownMode(conditionMode))
                throw new CommandFailure(2, $"{where}的 when 的 match '{conditionMode}' 不认识");
            if (step.When is { } condition && condition.Target is null && step.Target is null)
                throw new CommandFailure(2, $"{where}的 when 需要 target（自己给，或者步骤本身有）");
            if (step.Type is "wait-text" or "click-text" or "read-text" && step.Region is null)
                throw new CommandFailure(2, $"{where}的 {step.Type} 需要 region（读哪一块）");
            if (step.Type == "repeat" && (step.Times is not int t || t < 0 || t > MaxLoopIterations))
                throw new CommandFailure(2, $"{where}的 repeat 需要 times（0 到 {MaxLoopIterations} 之间）");
            if (step.Type == "foreach" && string.IsNullOrEmpty(step.In))
                throw new CommandFailure(2, $"{where}的 foreach 需要 in（遍历哪个列表变量）");

            CheckPlaceholders(step, scope, where);
            if (step.Type == "read-text" && !string.IsNullOrEmpty(step.Into)) scope.Add(step.Into!);

            // An export is the same promise one level out: the caller may use those names afterwards.
            if (step.Type == "call" && step.Export is { Length: > 0 })
            {
                foreach (string name in step.Export) scope.Add(name);
            }

            if (step.Type == "call") ValidateCall(step, where, calls);

            if (step.Steps is { Count: > 0 } children)
            {
                var inner = new HashSet<string>(scope, StringComparer.Ordinal) { "index" };
                if (step.Type == "foreach") inner.Add("item");
                ValidateSteps(children, inner, $"{where}里", calls);
            }
            else if (step.Type is "repeat" or "foreach")
            {
                throw new CommandFailure(2, $"{where}的 {step.Type} 里没有步骤");
            }
        }
    }

    /// <summary>Where a `call` is being validated from: its own directory, the files already on the
    /// call stack (a subflow may not call back into one of them), and how deep we already are.</summary>
    private readonly record struct CallContext(string BaseDirectory, IReadOnlyList<string> Stack, int Depth);

    /// <summary>
    /// A subflow is checked here rather than at the first step: a missing file, a cycle or a call
    /// tower is a property of the file, not of the moment it happens to run.
    /// </summary>
    private static void ValidateCall(FlowStep step, string where, CallContext calls)
    {
        if (string.IsNullOrEmpty(step.Flow))
            throw new CommandFailure(2, $"{where}的 call 需要 flow（子流程文件，相对当前文件）");

        string path = Path.GetFullPath(Path.Combine(calls.BaseDirectory, step.Flow!));
        if (!File.Exists(path))
            throw new CommandFailure(2, $"{where}的 call 指向的子流程不存在：{path}");
        if (calls.Depth + 1 > FlowPlan.MaxCallDepth)
            throw new CommandFailure(2, $"{where}的 call 嵌套超过 {FlowPlan.MaxCallDepth} 层（子流程树不是用来堆栈的）");
        if (calls.Stack.Contains(path, StringComparer.OrdinalIgnoreCase))
            throw new CommandFailure(2,
                $"{where}的 call 成环了：{string.Join(" → ", calls.Stack)} → {path}");
    }

    /// <summary>Every `{{name}}` a step mentions, taken from the step's own JSON so a new field cannot
    /// be forgotten here, and checked against what is actually in scope.</summary>
    private static void CheckPlaceholders(FlowStep step, HashSet<string> scope, string where)
    {
        foreach (string name in Placeholders(step))
        {
            bool loopVariable = name is "index" or "item";
            if (!scope.Contains(name) && !loopVariable)
            {
                throw new CommandFailure(2,
                    $"{where}用了未定义的变量 {{{{{name}}}}}（文档 variables、循环变量 index/item，或者 --set）");
            }
        }
    }

    internal static IEnumerable<string> Placeholders(FlowStep step)
    {
        // A group's children are validated on their own, with the scope their loop provides - so a
        // group is checked for the placeholders in its *own* fields. Serializing the whole step here
        // would make `foreach` look like it used `{{item}}` before the loop variable existed.
        string json;
        if (step.Steps is null)
        {
            json = JsonSerializer.Serialize(step, Json);
        }
        else
        {
            FlowStep shallow = JsonSerializer.Deserialize<FlowStep>(JsonSerializer.Serialize(step, Json), Json)!;
            shallow.Steps = null;
            json = JsonSerializer.Serialize(shallow, Json);
        }

        return PlaceholderPattern.Matches(json).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal);
    }

    internal static readonly System.Text.RegularExpressions.Regex PlaceholderPattern =
        new(@"\{\{([A-Za-z0-9_.]+)\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Replaces `{{name}}` with what the scope holds. Called at the last moment before dispatch, on the
    /// compiled arguments, so every step type gets variables without a line of per-type code.
    /// </summary>
    internal static string Expand(string text, VariableFrame frame)
    {
        if (!text.Contains("{{", StringComparison.Ordinal)) return text;
        return PlaceholderPattern.Replace(text, match =>
        {
            string name = match.Groups[1].Value;
            if (frame.Lookup(name) is { } value) return value;
            throw new CommandFailure(2, $"用了未定义的变量 {{{{{name}}}}}（文档 variables、循环变量，或者 --set）");
        });
    }

    /// <summary>
    /// The step as it will run: a copy with every `{{name}}` resolved against the frame it runs in.
    /// Unknown names are refused here, at the last moment before dispatch, because the loader can only
    /// check what is statically in scope - not what a `read-text` captured two steps ago, and not what
    /// a `call` handed down.
    /// </summary>
    internal static FlowStep Resolve(FlowStep step, VariableFrame frame)
    {
        FlowStep copy = JsonSerializer.Deserialize<FlowStep>(JsonSerializer.Serialize(step, Json), Json)!;
        if (copy.Target is { } target)
        {
            target.Process = Fill(target.Process);
            target.Class = Fill(target.Class);
            target.Title = Fill(target.Title);
        }
        copy.Text = Fill(copy.Text);
        copy.Combo = Fill(copy.Combo);
        copy.Into = Fill(copy.Into);
        copy.Note = Fill(copy.Note);
        return copy;

        string? Fill(string? value) => string.IsNullOrEmpty(value) ? value : Expand(value, frame);
    }

    internal void Save(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }

    internal static readonly string[] KnownTypes =
    [
        "focus", "click", "drag", "move", "wheel", "type", "key", "sleep", "wait-window", "wait-text",
        "click-text", "read-text", "repeat", "foreach", "call",
    ];

    /// <summary>Steps that read a region: they share the region/target/match rules and the runner.</summary>
    internal static readonly string[] ReadingTypes = ["wait-text", "click-text", "read-text"];

    /// <summary>Steps that only exist for the runner: they carry steps of their own or store a value.</summary>
    internal static readonly string[] RunnerTypes = ["sleep", "wait-window", "wait-text", "click-text", "read-text", "repeat", "foreach"];

    /// <summary>One loop cannot run forever: the cap is stated, not discovered at 3 a.m.</summary>
    internal const int MaxLoopIterations = 10_000;

    /// <summary>
    /// Turns a step into the command line that does it. This is the whole point of the flow format:
    /// a flow is not a second execution language, it is a way to produce the same commands, so
    /// selectors, the eligibility gate, focus verification and the exit codes are shared with the
    /// text syntax and with a hand-typed command.
    /// </summary>
    internal static string[] ToArguments(FlowStep step)
    {
        string[] selector = step.Target?.Selector() ?? [];
        var args = new List<string>();
        switch (step.Type)
        {
            case "focus":
                args.AddRange(["window", "focus"]);
                args.AddRange(selector);
                break;

            case "click":
                args.AddRange(["mouse", "click", step.Button ?? "left"]);
                args.AddRange(PointArguments(step, step.At, "-x", "-y", "-wx", "-wy"));
                args.AddRange(selector);
                break;

            case "move":
                if (step.At is null) throw new CommandFailure(2, "move 步骤需要 at");
                args.Add("mouse");
                args.Add("move");
                if (step.At.Space == FlowSpace.Client)
                {
                    RequireTarget(step);
                    args.AddRange(["-wx", Int(step.At.X), "-wy", Int(step.At.Y)]);
                }
                else
                {
                    args.AddRange([Int(step.At.X), Int(step.At.Y)]); // mouse move takes x y positionally
                }
                args.AddRange(selector);
                break;

            case "wheel":
                args.AddRange(["mouse", "wheel", (step.Delta ?? 120).ToString(CultureInfo.InvariantCulture)]);
                args.AddRange(PointArguments(step, step.At, "-x", "-y", "-wx", "-wy"));
                args.AddRange(selector);
                break;

            case "drag":
                if (step.From is null || step.To is null) throw new CommandFailure(2, "drag 步骤需要 from 与 to");
                if (step.From.Space != FlowSpace.Client || step.To.Space != FlowSpace.Client)
                    throw new CommandFailure(2, "drag 目前只支持客户区坐标（space=client）");
                args.AddRange(["mouse", "drag",
                    "-wx", step.From.X.ToString(CultureInfo.InvariantCulture),
                    "-wy", step.From.Y.ToString(CultureInfo.InvariantCulture),
                    "--wx2", step.To.X.ToString(CultureInfo.InvariantCulture),
                    "--wy2", step.To.Y.ToString(CultureInfo.InvariantCulture)]);
                if (step.DurationMs is int duration)
                {
                    args.AddRange(["--duration", duration.ToString(CultureInfo.InvariantCulture)]);
                }
                args.AddRange(selector);
                break;

            case "type":
                args.AddRange(["key", "type", step.Text ?? ""]);
                if (step.IntervalMs is int interval)
                {
                    args.AddRange(["--interval", interval.ToString(CultureInfo.InvariantCulture)]);
                }
                args.AddRange(selector);
                break;

            case "key":
                if (!string.IsNullOrEmpty(step.Combo)) args.AddRange(["key", "combo", step.Combo]);
                else args.AddRange(["key", "press", step.Text ?? ""]);
                args.AddRange(selector);
                break;

            default:
                throw new CommandFailure(2, $"'{step.Type}' 由执行器自己处理，不该编译成命令");
        }

        return args.ToArray();
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void RequireTarget(FlowStep step)
    {
        if (step.Target is null || step.Target.Selector().Length == 0)
        {
            throw new CommandFailure(2, $"'{step.Type}' 用客户区坐标时必须带 target（窗口选择器），否则坐标无从换算");
        }
    }

    private static IEnumerable<string> PointArguments(
        FlowStep step, FlowPoint? point, string? absoluteX, string? absoluteY, string relativeX, string relativeY)
    {
        if (point is null) return [];
        if (point.Space == FlowSpace.Client)
        {
            RequireTarget(step);
            return [relativeX, Int(point.X), relativeY, Int(point.Y)];
        }
        if (point.Space != FlowSpace.Screen)
        {
            throw new CommandFailure(2, $"'{step.Type}' 的坐标空间 '{point.Space}' 不认识（client | screen）");
        }
        if (absoluteX is null || absoluteY is null)
        {
            throw new CommandFailure(2, $"'{step.Type}' 只支持客户区坐标（space=client）：屏幕坐标会因为窗口移动而失效");
        }
        return [absoluteX, Int(point.X), absoluteY, Int(point.Y)];
    }
}
