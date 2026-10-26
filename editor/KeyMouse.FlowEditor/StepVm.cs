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
        nameof(IndentMargin), nameof(ConnectorBrush), nameof(ConnectorVisibility), nameof(ChildCount), nameof(ChildCountVisibility),
        nameof(ShotVisibility), nameof(ShotImage), nameof(BadgeBrush), nameof(BadgeInk),
        nameof(BeadBrush), nameof(BeadRingBrush), nameof(RowBrush), nameof(BeadSize),
        nameof(ActivityVisibility), nameof(NumberBrush),
        nameof(PreviewTag), nameof(RowOpacity), nameof(PreviewNote), nameof(PreviewNoteVisibility),
        nameof(ToggleVisibility), nameof(Chevron),
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
        ? (Brush)Application.Current.Resources["KpSignalBrush"]
        : (Brush)Application.Current.Resources["KpFieldBrush"];
    public Brush BadgeInk => IsSelected
        ? (Brush)Application.Current.Resources["KpOnSignalBrush"]
        : (Brush)Application.Current.Resources["KpInk2Brush"];

    /// <summary>
    /// 账本脊柱上的那颗珠。
    ///
    /// 全部步骤共用一条 1px 的脊线，珠子把脊线"咬断"：未选中的珠是纸色、圈一道细线，
    /// 看起来是脊线上的一个结；选中的珠实心朱砂——同一屏里唯一的一颗。
    /// 顺序由这条线的连续与断开表达，不靠行号加粗或底色。
    /// </summary>
    private bool _running;

    /// <summary>正在执行的那一行：行首一条朱砂细条。一屏只有一条，位置就是"现在到哪儿了"。</summary>
    public Visibility ActivityVisibility => Ui.Show(_running);

    /// <summary>跑动中的编号也翻朱砂——与那条细条是同一个信号。</summary>
    public Brush NumberBrush => _running
        ? (Brush)Application.Current.Resources["KpSignalBrush"]
        : (Brush)Application.Current.Resources["KpInk2Brush"];

    public Brush BeadBrush => IsSelected
        ? (Brush)Application.Current.Resources["KpSignalBrush"]
        : (Brush)Application.Current.Resources["KpCardBrush"];

    /// <summary>
    /// 珠的直径**恒定**。选中不靠变大表达——尺寸一跳，珠子就不再落在同一条线上，
    /// 深色态看起来就是"歪的"（实测反馈）。选中只改颜色。
    /// 11 是奇数：它和 1px 的连线能同时居中在同一个整数边界上。
    /// </summary>
    public const double BeadSize = 11;

    public Brush BeadRingBrush => IsSelected
        ? (Brush)Application.Current.Resources["KpCardBrush"]
        : (Brush)Application.Current.Resources["KpInk3Brush"];

    /// <summary>选中那一行的底色：一层极浅的纸黄，只用来把"当前"这条从纸面上轻轻托起来。</summary>
    public Brush RowBrush => IsSelected
        ? (Brush)Application.Current.Resources["KpSelectedBrush"]
        : (Brush)Application.Current.Resources["ControlFillColorTransparentBrush"];

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

    /// <summary>
    /// 耗时只在真的跑过之后显示。折叠行（748 次移动合成一行）显示**合计**：
    /// 只写其中一步的毫秒数会误导——那一行代表 748 步。
    /// </summary>
    public string RunDetail => _runState switch
    {
        "running" => "运行中",
        "skipped" => "已跳过",
        "ok" => _aggCount > 1 ? $"{_aggCount} 步 · {_runMs} ms" : $"{_runMs} ms",
        "failed" => _aggCount > 1 ? $"失败 {_aggCount} 步中 · {_runMs} ms" : $"失败 · {_runMs} ms",
        _ => "",
    };

    private int _aggCount;

    /// <summary>最近一次运行的清态：计数与毫秒都归零，屏幕上的旧结果不该留到下一次。</summary>
    internal void ResetRun()
    {
        _runState = "";
        _runMs = 0;
        _aggCount = 0;
        _running = false;
        Refresh();
    }

    internal void MarkRunning()
    {
        _runState = "running";
        _runMs = 0;
        _aggCount = 0;
        _running = true;
        Refresh();
    }

    internal void ClearRunning()
    {
        // 只清"正在运行"这一个状态：结果（ok/failed）不能被抹掉。
        // 以前这里只重置 _running，_runState 仍停在 "running"，于是 RunDetail 一直显示"运行中"，
        // 跑完以后几行还挂着那个提示（实测反馈）。
        if (_runState != "running") return;
        _running = false;
        _runState = "";
        Refresh();
    }

    /// <summary>折叠行：把子步骤的结果累加进去，而不是互相覆盖。</summary>
    internal void MarkAggregate(int exitCode, long durationMs)
    {
        _aggCount++;
        _runMs += durationMs;
        _runState = exitCode == 0 ? (_runState == "failed" ? "failed" : "ok") : "failed";
        _running = false;
        Refresh();
    }

    internal void MarkResult(int exitCode, long durationMs)
    {
        // 只如实反映 Runner 报的退出码：0 = 成功，其余 = 失败。哪些退出码可以重试、
        // "跳过"是什么意思，都由 Runner 的日志说明，这一行不替它解释。
        _runState = exitCode == 0 ? "ok" : "failed";
        _runMs = durationMs;
        _running = false;
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

    /// <summary>分层编号：顶层 1、2、3，组里的 2.1、2.2。等宽右对齐，个位永远在同一条竖线上。</summary>
    public string Number { get; internal set; } = "";

    /// <summary>0 = 顶层，1 = 组内。只支持两层——格式本身就只有两层（分组是唯一的容器）。</summary>
    internal int Depth { get; set; }

    /// <summary>属于哪个分组（顶层步骤为 null）。</summary>
    internal StepVm? Group { get; set; }

    internal bool FirstInGroup { get; set; }

    public bool IsChild => Depth > 0;

    /// <summary>
    /// 子步骤只缩进**内容**，不缩进珠与编号：整份流程共用一条脊柱，
    /// 珠子永远落在那条线上——分支线是衍生出来的第二根线，不该有。
    /// </summary>
    public Thickness IndentMargin => new(Depth * 22, 0, 0, 0);

    /// <summary>选中的那一行，把珠上下两段连线加粗成朱砂：结点之间的连接因此看得见。</summary>
    public Brush ConnectorBrush => (Brush)Application.Current.Resources["KpSignalBrush"];

    public Visibility ConnectorVisibility => Ui.Show(IsSelected);

    /// <summary>组内第一行：内容缩进的第一行，界面不必再画"肘"（脊柱只有一条）。</summary>
    internal bool IsFirstInGroup => IsChild && FirstInGroup;

    /// <summary>
    /// 分组：只有 repeat / foreach 带子步骤（call 引用另一个文件，它的步骤以只读预览出现）。
    /// 这个判断只用于"能不能往里加一步"和显示计数，不参与渲染哪些字段（那是契约的事）。
    /// </summary>
    public bool IsGroup => Step.Type is "repeat" or "foreach";

    /// <summary>子流程预览行：来自另一个文件，整行只读。</summary>
    public bool IsPreview { get; private set; }

    private string? _previewSource;

    internal void MarkPreview(string source)
    {
        IsPreview = true;
        _previewSource = source;
    }

    /// <summary>预览行右侧那行小字：这份步骤来自哪个文件。</summary>
    public string PreviewTag => IsPreview ? _previewSource ?? "" : "";

    public Visibility PreviewTagVisibility => Ui.Show(PreviewTag.Length > 0);

    /// <summary>预览行整行压暗——它不是这一份文档，改不动。</summary>
    public double RowOpacity => IsPreview ? 0.62 : 1.0;

    public string PreviewNote => IsPreview
        ? $"这一步来自子流程 {_previewSource}。它在这里只读——要改它，打开那个文件。"
        : "";

    public Visibility PreviewNoteVisibility => Ui.Show(IsPreview);

    /// <summary>call 行右侧显示被调用流程里有几步（预览加载成功才有）。</summary>
    internal int PreviewCount { get; set; }

    /// <summary>
    /// 这一行带几个子项（分组步骤 / 子流程预览 / 折叠起来的连续移动）。由 EditorModel 算好写进来。
    /// 少于 2 个子项的一律直接摊开——为一行折腾一次点击不值得。
    /// </summary>
    internal int ChildTotal { get; set; }

    /// <summary>展开状态只在界面里存在，不写进流程文件。</summary>
    internal bool IsExpanded { get; set; }

    public Visibility ToggleVisibility => Ui.Show(ChildTotal > 1);

    public string Chevron => IsExpanded ? "\uE70E" : "\uE70D";

    /// <summary>分组行在右侧说它带了几步；空组要说"空组"——否则没法把第一步放进去。</summary>
    public string ChildCount => Step.Steps is { Count: > 0 } children
        ? $"{children.Count} 步"
        : PreviewCount > 0 ? $"{PreviewCount} 步"
        : RunCount > 0 ? $"{RunCount} 次"
        : IsGroup ? "空组" : "";

    public Visibility ChildCountVisibility => Ui.Show(ChildCount.Length > 0);
    public string TypeName => StepCatalog.NameOf(Step.Type);
    public string Glyph => StepCatalog.GlyphOf(Step.Type);
    /// <summary>折叠行（连续鼠标移动的头一行）用一句话代替原摘要：起点 → 终点，跑了多少次。</summary>
    internal string? SummaryOverride { get; set; }

    /// <summary>
    /// 一行摘要。多行内容不塞进这一行——它会把行撑破、盖住下一行（实测），
    /// 所以只留一句"点击查看详细…"：要看清就去右边的框里看。
    /// </summary>
    public string Summary => SummaryOverride ?? OneLine(StepCatalog.Summarize(Step));

    private static string OneLine(string text) =>
        text.Contains('\n') || text.Contains('\r') ? "点击查看详细…" : text;

    /// <summary>这一行折叠了几步（连续 move 的头一行才有）。</summary>
    internal int RunCount { get; set; }
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

    /// <summary>
    /// 写回文本字段。**必须先把换行折成 \n**：WinUI 的 TextBox 用裸 \r 表示换行，
    /// 直接存进流程文件会让引擎把它当成 CRLF 的前半个字符跳掉（实测：编辑器里改过的多行文本打出来没有换行）。
    /// </summary>
    internal void SetText(string path, string? value) =>
        Write(path, value is null ? null : JsonValue.Create(value.Replace("\r\n", "\n").Replace('\r', '\n')));

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

        // 反序列化会造一个**新对象**。把它接上之前，必须让文档里那条引用也换过去——
        // 否则子步骤的编辑只活在界面上（实测：改了 2.1 的文字，跑起来还是旧文字）。
        FlowStep previous = Step;
        Step = JsonSerializer.Deserialize<FlowStep>(after, FlowDocument.Json)!;
        Owner.Rebind(previous, Step, this);
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
