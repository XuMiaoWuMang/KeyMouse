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

/// <summary>
/// One recorded or hand-written step. The shape is deliberately flat and nullable: a JSON document
/// a human edits by hand has to survive missing fields, and the editor writes back only what a step
/// actually uses. Unknown step types are rejected on load, not at replay time.
/// </summary>
internal sealed class FlowStep
{
    public string Type { get; set; } = "";
    public FlowTarget? Target { get; set; }
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

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static FlowDocument Load(string path)
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

        for (int i = 0; i < document.Steps.Count; i++)
        {
            var step = document.Steps[i];
            if (step is null) throw new CommandFailure(2, $"第 {i + 1} 步是 null");
            if (!KnownTypes.Contains(step.Type))
            {
                throw new CommandFailure(2,
                    $"第 {i + 1} 步的类型 '{step.Type}' 不认识（可用：{string.Join(" | ", KnownTypes)}）");
            }
        }

        return document;
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
    ];

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
