using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyMouse.FlowEditor;

/// <summary>
/// What the editor knows about each step type: how it is named, which glyph it gets, and what a new
/// one looks like. Keeping this in one table means the add menu, the list and the inspector cannot
/// disagree about the set of steps - and the set itself is checked against the loader's list.
/// </summary>
internal static class StepCatalog
{
    internal sealed record Kind(string Type, string Name, string Glyph, string Hint);

    /// <summary>Fluent icon glyphs (Segoe Fluent Icons).</summary>
    internal static readonly Kind[] All =
    [
        new("focus", "切换窗口", "\uE8A7", "把某个窗口切到前台（其余步骤的动作都会发给它）"),
        new("click", "点击", "\uE8B8", "在客户区某点按一下鼠标"),
        new("drag", "拖拽", "\uE8B0", "按住从一点拖到另一点"),
        new("move", "移动", "\uE8B9", "把指针移到某点（悬停用）"),
        new("wheel", "滚轮", "\uE8B9", "滚动一定增量（正数向上）"),
        new("type", "输入文字", "\uE8BD", "把一串文字打进去（按字符，不按键盘布局）"),
        new("key", "按键/组合键", "\uE765", "按一个键或一组组合键，例如 ctrl+s"),
        new("sleep", "等待时间", "\uE916", "什么都不做，等这么多毫秒"),
        new("wait-window", "等窗口出现", "\uE7C4", "轮询直到目标窗口可用，超时退出码 3"),
        new("wait-text", "等文字出现", "\uE8D4", "读一块区域，等某段文字出现（可给容错预算）"),
        new("click-text", "找字并点它", "\uE8B8", "等文字出现，然后点匹配框的中心"),
        new("read-text", "读进变量", "\uE8D5", "读一块区域，把读到的东西存进一个变量"),
        new("repeat", "重复 N 次", "\uE8EE", "把里面的步骤重复若干次（子步骤在 JSON 里编辑）"),
        new("foreach", "遍历列表", "\uE8FD", "对列表变量里的每一项执行一次里面的步骤"),
        new("call", "调用子流程", "\uE8F4", "运行另一个流程文件；子流程有独立的变量作用域，结果靠 export 交回来"),
    ];

    internal static Kind? Find(string type) => All.FirstOrDefault(k => k.Type == type);

    internal static string NameOf(string type) => Find(type)?.Name ?? type;

    internal static string GlyphOf(string type) => Find(type)?.Glyph ?? "\uE9CE";

    /// <summary>A new step of this type, with the fields the loader requires already filled in.</summary>
    internal static FlowStep Create(string type) => type switch
    {
        "focus" => new FlowStep { Type = type, Target = new FlowTarget() },
        "click" => new FlowStep
        {
            Type = type,
            Button = "left",
            At = new FlowPoint { Space = FlowSpace.Client },
            Target = new FlowTarget(),
        },
        "drag" => new FlowStep
        {
            Type = type,
            Button = "left",
            From = new FlowPoint { Space = FlowSpace.Client },
            To = new FlowPoint { Space = FlowSpace.Client, X = 200, Y = 100 },
            DurationMs = 400,
            Target = new FlowTarget(),
        },
        "move" => new FlowStep
        {
            Type = type,
            At = new FlowPoint { Space = FlowSpace.Client },
            Target = new FlowTarget(),
        },
        "wheel" => new FlowStep
        {
            Type = type,
            Delta = -120,
            At = new FlowPoint { Space = FlowSpace.Client },
            Target = new FlowTarget(),
        },
        "type" => new FlowStep
        {
            Type = type,
            Text = "",
            IntervalMs = 15,
            Target = new FlowTarget(),
        },
        "key" => new FlowStep { Type = type, Combo = "ctrl+s", Target = new FlowTarget() },
        "sleep" => new FlowStep { Type = type, Ms = 500 },
        "wait-window" => new FlowStep { Type = type, Target = new FlowTarget(), TimeoutMs = 5000, IntervalMs = 200 },
        "wait-text" => new FlowStep
        {
            Type = type,
            Target = new FlowTarget(),
            Region = new FlowRegion { Space = FlowSpace.Client, Width = 400, Height = 32 },
            Text = "",
            Match = TextPredicate.Contains,
            MaxErrors = 1,
            TimeoutMs = 5000,
            IntervalMs = 250,
            Confirm = 2,
        },
        "click-text" => new FlowStep
        {
            Type = type,
            Target = new FlowTarget(),
            Region = new FlowRegion { Space = FlowSpace.Client, Width = 400, Height = 32 },
            Text = "",
            Match = TextPredicate.Contains,
            MaxErrors = 1,
            TimeoutMs = 8000,
            IntervalMs = 250,
            Confirm = 2,
            Button = "left",
        },
        "read-text" => new FlowStep
        {
            Type = type,
            Target = new FlowTarget(),
            Region = new FlowRegion { Space = FlowSpace.Client, Width = 400, Height = 32 },
            Into = "seen",
        },
        "repeat" => new FlowStep { Type = type, Times = 3, Steps = [new FlowStep { Type = "sleep", Ms = 200 }] },
        "foreach" => new FlowStep { Type = type, In = "rows", Steps = [new FlowStep { Type = "sleep", Ms = 200 }] },
        "call" => new FlowStep { Type = type, Flow = "sub.json" },
        _ => new FlowStep { Type = type },
    };

    /// <summary>One line a human can scan in the list, e.g. "点击 左键 @ 客户区 120,159 → notepad".</summary>
    internal static string Summarize(FlowStep step)
    {
        static string Point(FlowPoint? p) => p is null ? "" : $"{p.X},{p.Y}";
        string target = step.Target?.Process is { Length: > 0 } process ? $" → {process}" : "";
        string space = step.At?.Space == FlowSpace.Screen || step.From?.Space == FlowSpace.Screen ? "（屏幕坐标）" : "";

        return step.Type switch
        {
            "focus" => $"切到 {step.Target?.Process ?? step.Target?.Class ?? "?"}",
            "click" => $"{(step.Button ?? "left") switch { "right" => "右键", "middle" => "中键", _ => "左键" }} 点击 @ {Point(step.At)}{space}{target}",
            "drag" => $"{Point(step.From)} → {Point(step.To)}，{step.DurationMs ?? 0} ms{target}",
            "move" => $"移到 {Point(step.At)}{space}{target}",
            "wheel" => $"滚轮 {step.Delta ?? 0} @ {Point(step.At)}{target}",
            "type" => $"输入「{Clip(step.Text)}」，间隔 {step.IntervalMs ?? 15} ms{target}",
            "key" => step.Combo is { Length: > 0 } combo ? $"按 {combo}{target}" : $"按 {step.Text}{target}",
            "sleep" => $"等 {step.Ms ?? 0} ms",
            "wait-window" => $"等窗口 {step.Target?.Process ?? step.Target?.Class ?? "?"}（上限 {step.TimeoutMs ?? 5000} ms）",
            "wait-text" => $"等「{Clip(step.Text)}」（{step.Match ?? TextPredicate.Contains}，" +
                           $"容错 {step.MaxErrors ?? 1}，区域 {step.Region?.Width}x{step.Region?.Height}，" +
                           $"上限 {step.TimeoutMs ?? 5000} ms）",
            "click-text" => $"找「{Clip(step.Text)}」并点它（{step.Match ?? TextPredicate.Contains}，容错 {step.MaxErrors ?? 1}）",
            "read-text" => $"读一块区域 → 变量 {step.Into}",
            "repeat" => $"重复 {(step.Times ?? 0)} 次（{step.Steps?.Count ?? 0} 步）",
            "foreach" => $"遍历 {step.In}（{step.Steps?.Count ?? 0} 步）",
            "call" => $"调用子流程 {step.Flow}" + (step.Vars is { Count: > 0 } vars ? $"（传 {vars.Count} 个变量）" : "") + (step.Export is { Length: > 0 } export ? $"，导出 {string.Join("/", export)}" : ""),
            _ => step.Type,
        };
    }

    private static string Clip(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= 28 ? text : text[..26] + "…";
    }
}

/// <summary>Small helpers the UI needs that are not worth a file of their own.</summary>
internal static class Ui
{
    internal static Visibility Show(bool condition) => condition ? Visibility.Visible : Visibility.Collapsed;

    internal static BitmapImage? Thumbnail(string? relative, string? documentPath)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        string baseDirectory = documentPath is { Length: > 0 }
            ? Path.GetDirectoryName(Path.GetFullPath(documentPath)) ?? "."
            : ".";
        string full = Path.GetFullPath(Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(full) ? new BitmapImage(new Uri(full)) : null;
    }
}
