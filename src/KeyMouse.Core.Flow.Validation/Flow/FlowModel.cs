using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyMouse.Flow;
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

    /// <summary>
    /// 完全录制的原始总时长（毫秒），用于展示。回放时的节奏取决于各步的等待与动作耗时。
    /// </summary>
    public int? DurationMs { get; set; }

    public FlowScreen? Screen { get; set; }
    public FlowOptions? Options { get; set; }
    public List<FlowStep> Steps { get; set; } = new();

    /// <summary>Document-level variables. A value is a string, or an array of strings when a
    /// `foreach` needs a list. `--set name=value` overrides a string one (and reduces a list to a
    /// single element, documented rather than surprising).</summary>
    /// <summary>
    /// Whether this flow may restore a minimized window. Off by default - bringing a window back is a
    /// visible change to someone's desktop, so it is opted into, exactly like `--allow-restore` on the
    /// command line. Set it when the flow starts from a window the user left minimized.
    /// </summary>
    public bool AllowRestore { get; set; }

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
        // A `when` with no text is not a broken precondition, it is the absence of one - the field's own
        // label says "leave it empty and the step always runs". The editor can produce that state by
        // touching any other `when` box, so refusing it would punish the user for our UI's shape.
        foreach (FlowStep step in AllSteps(document.Steps))
        {
            if (step.When is { } condition && string.IsNullOrWhiteSpace(condition.Text)) step.When = null;
        }
        NormalizeKeySteps(document.Steps);

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

        FlowValidator.Validate(document, path, stack, inherited);
        return document;
    }

    /// <summary>
    /// 2.2.0 起 key/keydown/keyup/keyhold 只有一个 `combo` 字段：
    /// 单键写 `enter`，组合键写 `ctrl+shift+s`。旧文件里的 `text` 在加载时搬进 combo，
    /// 保存后自然消失；不能让旧文件因为少一个 combo 就拒绝加载。
    /// </summary>
    private static void NormalizeKeySteps(List<FlowStep> steps)
    {
        foreach (FlowStep step in steps)
        {
            if (step.Type is "key" or "keydown" or "keyup" or "keyhold")
            {
                if (string.IsNullOrWhiteSpace(step.Combo)) step.Combo = step.Text;
                step.Text = null;
            }
            if (step.Steps is { } children) NormalizeKeySteps(children);
            if (step.Else is { } otherwise) NormalizeKeySteps(otherwise);
        }
    }

    internal static IEnumerable<string> Placeholders(FlowStep step)    {
        // A group's children are validated on their own, with the scope their loop provides - so a
        // group is checked for the placeholders in its *own* fields. Serializing the whole step here
        // would make `foreach` look like it used `{{item}}` before the loop variable existed.
        FlowStep shallow = JsonSerializer.Deserialize<FlowStep>(JsonSerializer.Serialize(step, Json), Json)!;
        // Expression fields are checked by the expression parser (they use bare names, not {{...}}),
        // and the else branch has its own steps with their own scope. Scanning them here would either
        // double-report or invent a placeholder that the runtime never expands.
        shallow.Steps = null;
        shallow.Else = null;
        shallow.Cond = null;
        shallow.Init = null;
        shallow.Step = null;
        shallow.Value = null;
        string json = JsonSerializer.Serialize(shallow, Json);

        return PlaceholderPattern.Matches(json).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal);
    }

    internal static readonly System.Text.RegularExpressions.Regex PlaceholderPattern =
        new(@"\{\{([\p{L}\p{Nd}_.]+)\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

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
        if (copy.Probe?.Target is { } probeTarget)
        {
            probeTarget.Process = Fill(probeTarget.Process);
            probeTarget.Class = Fill(probeTarget.Class);
            probeTarget.Title = Fill(probeTarget.Title);
        }
        if (copy.When is { } when)
        {
            when.Text = Fill(when.Text);
            if (when.Target is { } whenTarget)
            {
                whenTarget.Process = Fill(whenTarget.Process);
                whenTarget.Class = Fill(whenTarget.Class);
                whenTarget.Title = Fill(whenTarget.Title);
            }
        }
        copy.Text = Fill(copy.Text);
        copy.Combo = Fill(copy.Combo);
        copy.Into = Fill(copy.Into);
        copy.Note = Fill(copy.Note);
        copy.Message = Fill(copy.Message);
        return copy;

        string? Fill(string? value) => string.IsNullOrEmpty(value) ? value : Expand(value, frame);
    }

    /// <summary>
    /// A probe may name its window with a placeholder too; resolve it at the moment the condition is
    /// evaluated, exactly like a step's target.
    /// </summary>
    internal static FlowProbe? ResolveProbe(FlowProbe? probe, VariableFrame frame)
    {
        if (probe?.Target is null) return probe;
        FlowTarget target = probe.Target;
        return new FlowProbe
        {
            Target = new FlowTarget
            {
                Process = Fill(target.Process),
                Class = Fill(target.Class),
                Title = Fill(target.Title),
            },
            Region = probe.Region,
            Match = probe.Match,
            MaxErrors = probe.MaxErrors,
        };

        string? Fill(string? value) => string.IsNullOrEmpty(value) ? value : Expand(value, frame);
    }

    /// <summary>Every step in the file, groups included (used where a whole-file rule applies).</summary>
    private static IEnumerable<FlowStep> AllSteps(List<FlowStep> steps)
    {
        foreach (FlowStep step in steps)
        {
            yield return step;
            foreach (FlowStep child in AllSteps(step.Steps ?? [])) yield return child;
            foreach (FlowStep child in AllSteps(step.Else ?? [])) yield return child;
        }
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
        "if", "while", "do", "for", "break", "continue", "return", "set", "assert",
        "keydown", "keyup", "mousedown", "mouseup", "hold", "mousehold", "keyhold",
    ];

    /// <summary>Steps that read a region: they share the region/target/match rules and the runner.</summary>
    internal static readonly string[] ReadingTypes = ["wait-text", "click-text", "read-text"];

    /// <summary>Steps that only exist for the runner: they carry steps of their own or store a value.</summary>
    internal static readonly string[] RunnerTypes =
    [
        "sleep", "hold", "mousehold", "keyhold", "wait-window", "wait-text", "click-text", "read-text",
        "repeat", "foreach", "if", "while", "do", "for", "break", "continue", "return", "set", "assert",
    ];

    /// <summary>Step types that carry a block of child steps in `steps`.</summary>
    internal static readonly string[] BlockTypes = ["repeat", "foreach", "if", "while", "do", "for"];

    /// <summary>One loop cannot run forever: the cap is stated, not discovered at 3 a.m.</summary>
    internal const int MaxLoopIterations = 10_000;

}
