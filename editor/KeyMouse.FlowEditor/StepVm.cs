using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyMouse.FlowEditor;

/// <summary>
/// One step, as the UI sees it: editable primitives that read and write the shared model directly.
///
/// Every setter raises PropertyChanged with an empty name, so the row's summary, the JSON preview
/// and the visible inspector sections all refresh together. That is deliberate: a step is small, and
/// a per-property notification table would be more code than the thing it describes.
///
/// Every setter also goes through <see cref="Edit"/>, which ignores a write of the value that is
/// already there. Measured why: a collapsed card's controls are still bound, so opening a file wrote
/// defaults back into the model (a click step gained "ms": 0) and the editor claimed unsaved changes
/// before anything had been touched.
/// </summary>
public sealed class StepVm : INotifyPropertyChanged
{

    internal StepVm(FlowStep step, EditorModel owner)
    {
        Step = step;
        Owner = owner;
    }

    internal FlowStep Step { get; private set; }
    internal EditorModel Owner { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed()
    {
        Refresh();
        Owner.MarkDirty();
    }

    /// <summary>Lets the document refresh a row without pretending it changed.</summary>
    /// <summary>
    /// 通知界面上这一行需要重画。
    ///
    /// 注意：不能用"属性名为空 = 全部刷新"那个约定。`x:Bind` 是编译期绑定，它按名字精确匹配，
    /// 发一个空名字它**收不到**（实测：运行结果标记一直不出现）。所以这里逐个名字发。
    /// </summary>
    private static readonly string[] RowProperties =
    [
        nameof(Number), nameof(Summary), nameof(RunGlyph), nameof(RunVisibility), nameof(RunDetail),
        nameof(ShotVisibility), nameof(ShotImage), nameof(BadgeBrush), nameof(BadgeInk),
    ];

    internal void RefreshBadge() => Refresh();

    internal void Refresh()
    {
        foreach (string name in RowProperties)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    private void Edit<T>(T current, T value, Action<T> write)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        write(value);
        Changed();
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    /// <summary>这一行是不是当前选中的那一步（窗口在选中变化时设置）。</summary>
    public bool IsSelected { get; private set; }

    internal void SetSelected(bool selected)
    {
        if (IsSelected == selected) return;
        IsSelected = selected;
        Refresh();
    }

    /// <summary>
    /// 类型徽标的面色：**只有当前选中的那一行**用强调色。
    /// 强调色的意义是"这里可以动"；铺在每一行上就变成了装饰，选中态也失去对比。
    /// </summary>
    public Brush BadgeBrush => IsSelected
        ? (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"]
        : (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
    public Brush BadgeInk => IsSelected
        ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
        : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    // ---------------------------------------------------------------- 运行证据（每一步留下的截图）

    private BitmapImage? _runShot;
    private string? _runShotPath;

    /// <summary>
    /// 这一步的截图：优先显示**这次运行**留下的证据；没有就退回流程文件里录制时的那张。
    /// 两者都是真实文件，界面不合成、不占位。
    /// </summary>
    public BitmapImage? ShotImage => _runShot ?? Ui.Thumbnail(Step.Shot, Owner.DocumentPath);
    public Visibility ShotVisibility => Ui.Show(ShotImage is not null);
    public string? ShotPath => _runShotPath ?? Step.Shot;

    internal void SetShot(string path)
    {
        _runShotPath = path;
        _runShot = Ui.Thumbnail(path, Owner.DocumentPath);
        Refresh();
    }

    internal void ClearShot()
    {
        _runShotPath = null;
        _runShot = null;
        Refresh();
    }

    // ---------------------------------------------------------------- 运行结果（每行原位显示）

    /// <summary>这一行上一次跑成什么样。数据来自 Runner 的逐步事件，不是猜的。</summary>
    private string _runState = "";
    private long _runMs;

    /// <summary>整行左侧那枚标记：跑过才有，且只表达真实结果，不做装饰。</summary>
    public string RunGlyph => _runState switch
    {
        "ok" => "\uE73E",
        "failed" => "\uEA39",
        "skipped" => "\uE738",
        "running" => "\uE768",
        _ => "",
    };

    public Visibility RunVisibility => Ui.Show(_runState.Length > 0);

    /// <summary>耗时只在真的跑过之后显示。</summary>
    public string RunDetail => _runState switch
    {
        "running" => "运行中",
        "skipped" => "已跳过",
        "ok" => $"{_runMs} ms",
        "failed" => $"失败 · {_runMs} ms",
        _ => "",
    };

    internal void MarkRunning()
    {
        _runState = "running";
        _runMs = 0;
        Refresh();
    }

    internal void MarkResult(int exitCode, long durationMs)
    {
        // 只如实反映 Runner 报的退出码：0 = 成功，其余 = 失败。哪些退出码可以重试、
        // "跳过"是什么意思，都由 Runner 的日志说明，这一行不替它解释。
        _runState = exitCode == 0 ? "ok" : "failed";
        _runMs = durationMs;
        Refresh();
    }

    // ---------------------------------------------------------------- 就地校验（错误显示在字段下方）

    private readonly Dictionary<string, string> _issues = new(StringComparer.Ordinal);

    public bool HasIssues => _issues.Count > 0;

    /// <summary>某个参数的错误文字；没有就是空。检查器把它显示在该字段下方（规则 4.6）。</summary>
    internal string IssueFor(string path) => _issues.TryGetValue(path, out string? message) ? message : "";

    /// <summary>
    /// 在交给加载器之前先按**同一份契约**检查一遍，这样错误能落到具体字段上，而不是等一句
    /// "第 3 步的 click-text 需要 region"。契约仍是唯一权威，这里只是把同一条规则提前、就地讲。
    /// </summary>
    internal int Validate()
    {
        _issues.Clear();
        if (FlowSchema.TryGetStep(Step.Type, out FlowStepContract contract))
        {
            foreach (FlowField field in contract.Own.Where(f => f.Required))
            {
                if (!FlowSchema.HasValue(Step, field.Name))
                {
                    _issues[field.Name] = $"这一项是必填的：{field.LabelZh}";
                }
            }

            // 嵌套结构里的必填项同样要查：区域少了 y、点少了 x，加载器会用默认值悄悄跑过去，
            // 但那是"没填"，不该由程序替用户决定。
            foreach (FlowField shape in contract.All)
            {
                IReadOnlyList<FlowField> inner = FlowSchema.ShapeFields(shape.Kind);
                if (inner.Count == 0 || !FlowSchema.HasValue(Step, shape.Name)) continue;
                foreach (FlowField required in inner.Where(f => f.Required && f.Kind != "const"))
                {
                    if (!FlowSchema.HasValue(Step, $"{shape.Name}.{required.Name}"))
                    {
                        _issues[$"{shape.Name}.{required.Name}"] = $"这一项是必填的：{required.LabelZh}";
                    }
                }
            }

            if (contract.OneOf.Count > 0 && !contract.OneOf.Any(name => FlowSchema.HasValue(Step, name)))
            {
                string labels = string.Join(" 或 ", contract.OneOf.Select(name =>
                    contract.Own.FirstOrDefault(f => f.Name == name)?.LabelZh is { Length: > 0 } label ? label : name));
                foreach (string name in contract.OneOf) _issues[name] = $"至少要给一个：{labels}";
            }
        }

        Refresh();
        return _issues.Count;
    }

    // ---------------------------------------------------------------- display

    public int Number { get; internal set; }
    public string TypeName => StepCatalog.NameOf(Step.Type);
    public string Glyph => StepCatalog.GlyphOf(Step.Type);
    public string Summary => StepCatalog.Summarize(Step);
    public string Json => JsonSerializer.Serialize(Step, FlowDocument.Json);
    // ---------------------------------------------------------------- 契约驱动的字段读写

    /// <summary>
    /// Reading and writing a step's parameters **by their contract name** (点分路径，如 `region.x`、
    /// `when.text`）。这里没有任何"哪种步骤有哪些字段"的判断：界面问契约，契约说什么就渲染什么，
    /// 于是格式里有的参数不会在界面上缺席，加参数也只需要改契约一个文件。
    /// </summary>
    internal IReadOnlyList<FlowField> Fields => FlowSchema.FieldsFor(Step.Type);

    private JsonObject Snapshot() =>
        JsonNode.Parse(JsonSerializer.Serialize(Step, FlowDocument.Json)) as JsonObject ?? new JsonObject();

    private JsonNode? Read(string path)
    {
        JsonNode? node = Snapshot();
        foreach (string part in path.Split('.')) node = node?[part];
        return node;
    }

    internal string? GetText(string path) => Read(path) switch
    {
        JsonValue value when value.TryGetValue(out string? text) => text,
        JsonValue value => value.ToString(),
        _ => null,
    };

    internal bool GetBool(string path) => Read(path) is JsonValue value && value.TryGetValue(out bool flag) && flag;

    internal FlowPoint? GetPoint(string path) => Read(path) is JsonObject point
        ? new FlowPoint
        {
            Space = point["space"]?.ToString() == FlowSpace.Screen ? FlowSpace.Screen : FlowSpace.Client,
            X = int.TryParse(point["x"]?.ToString(), out int x) ? x : 0,
            Y = int.TryParse(point["y"]?.ToString(), out int y) ? y : 0,
        }
        : null;

    internal FlowRegion? GetRegion(string path) => Read(path) is JsonObject region
        ? new FlowRegion
        {
            Space = FlowSpace.Client,
            X = int.TryParse(region["x"]?.ToString(), out int x) ? x : 0,
            Y = int.TryParse(region["y"]?.ToString(), out int y) ? y : 0,
            Width = int.TryParse(region["width"]?.ToString(), out int w) ? w : 0,
            Height = int.TryParse(region["height"]?.ToString(), out int h) ? h : 0,
        }
        : null;

    internal void SetText(string path, string? value) =>
        Write(path, string.IsNullOrEmpty(value) ? null : JsonValue.Create(value));

    internal void SetBool(string path, bool value) => Write(path, JsonValue.Create(value));

    internal void SetNumber(string path, int? value) =>
        Write(path, value is int number ? JsonValue.Create(number) : null);

    internal void SetPoint(string path, FlowPoint point) => Write(path, new JsonObject
    {
        ["space"] = point.Space == FlowSpace.Screen ? "screen" : "client",
        ["x"] = point.X,
        ["y"] = point.Y,
    });

    internal void SetRegion(string path, FlowRegion region) => Write(path, new JsonObject
    {
        ["space"] = "client",
        ["x"] = region.X,
        ["y"] = region.Y,
        ["width"] = region.Width,
        ["height"] = region.Height,
    });

    /// <summary>
    /// Writes one parameter and rebuilds the step from the JSON, so the model stays the single source of
    /// truth: the row summary, the JSON card and the saved file all follow from it.
    /// </summary>
    private void Write(string path, JsonNode? value)
    {
        JsonObject root = Snapshot();
        string[] parts = path.Split('.');
        JsonObject cursor = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (cursor[parts[i]] is not JsonObject next)
            {
                next = new JsonObject();
                cursor[parts[i]] = next;
            }
            cursor = next;
        }
        if (value is null) cursor.Remove(parts[^1]); else cursor[parts[^1]] = value;
        PruneEmpty(root);

        string before = JsonSerializer.Serialize(Step, FlowDocument.Json);
        string after = root.ToJsonString();
        if (after == before) return;

        Step = JsonSerializer.Deserialize<FlowStep>(after, FlowDocument.Json)!;
        Changed();
    }

    /// <summary>
    /// 空对象不写进文件：在 `when` 里点了一下又清空，不该留下一个什么都不含的前提（加载器会把它
    /// 当作"没有前提"，但文件里也不该多这么一层壳）。
    /// </summary>
    private static void PruneEmpty(JsonObject root)
    {
        foreach (string name in root.Select(pair => pair.Key).ToList())
        {
            if (root[name] is JsonObject nested)
            {
                PruneEmpty(nested);
                if (!nested.Any()) root.Remove(name);
            }
        }
    }
}
