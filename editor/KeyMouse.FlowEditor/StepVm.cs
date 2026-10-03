using System.ComponentModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
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
    private static readonly PropertyChangedEventArgs Everything = new(string.Empty);

    internal StepVm(FlowStep step, EditorModel owner)
    {
        Step = step;
        Owner = owner;
    }

    internal FlowStep Step { get; }
    internal EditorModel Owner { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed()
    {
        PropertyChanged?.Invoke(this, Everything);
        Owner.MarkDirty();
    }

    /// <summary>Lets the document refresh a row without pretending it changed.</summary>
    internal void Refresh() => PropertyChanged?.Invoke(this, Everything);

    private void Edit<T>(T current, T value, Action<T> write)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        write(value);
        Changed();
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // ---------------------------------------------------------------- display

    public int Number { get; internal set; }
    public string TypeName => StepCatalog.NameOf(Step.Type);
    public string Glyph => StepCatalog.GlyphOf(Step.Type);
    public string Summary => StepCatalog.Summarize(Step);
    public string Json => JsonSerializer.Serialize(Step, FlowDocument.Json);
    public BitmapImage? ShotImage => Ui.Thumbnail(Step.Shot, Owner.DocumentPath);
    public Visibility ShotVisibility => Ui.Show(ShotImage is not null);

    // ---------------------------------------------------------------- which fields matter

    /// <summary>
    /// Which fields this step gets, straight from the format's own table. The hide/show decisions used
    /// to be hand-written here and had drifted: `click-text` (find the text and click it) showed only a
    /// target and a note, so none of its real parameters could be edited, and `read-text` had the same
    /// hole. Asking <see cref="FlowStepSchema"/> means a type can no longer have fields the inspector
    /// does not know about.
    /// </summary>
    private StepFields Fields => FlowStepSchema.For(Step.Type);

    private Visibility Show(StepFields field) => Ui.Show((Fields & field) != 0);

    public Visibility TargetVisibility => Show(StepFields.Target);
    public Visibility PointVisibility => Show(StepFields.Point);
    public Visibility RegionVisibility => Show(StepFields.Region);
    public Visibility ButtonVisibility => Show(StepFields.Button);
    public Visibility DeltaVisibility => Show(StepFields.Delta);
    public Visibility TextVisibility => Show(StepFields.Text);
    public Visibility ComboVisibility => Show(StepFields.Combo);
    public Visibility MsVisibility => Show(StepFields.Ms);
    public Visibility DurationVisibility => Show(StepFields.Duration);
    public Visibility TimeoutVisibility => Show(StepFields.Timeout);
    public Visibility MatchVisibility => Show(StepFields.Match);
    public Visibility DragEndsVisibility => Show(StepFields.DragEnds);
    public Visibility NoteVisibility => Show(StepFields.Note);

    // ---------------------------------------------------------------- target window

    public string TargetProcess
    {
        get => Step.Target?.Process ?? "";
        set => Edit(Step.Target?.Process ?? "", value, v => (Step.Target ??= new FlowTarget()).Process = Blank(v));
    }

    public string TargetClass
    {
        get => Step.Target?.Class ?? "";
        set => Edit(Step.Target?.Class ?? "", value, v => (Step.Target ??= new FlowTarget()).Class = Blank(v));
    }

    public string TargetTitle
    {
        get => Step.Target?.Title ?? "";
        set => Edit(Step.Target?.Title ?? "", value, v => (Step.Target ??= new FlowTarget()).Title = Blank(v));
    }

    // ---------------------------------------------------------------- coordinates

    /// <summary>0 = 客户区相对（推荐），1 = 绝对屏幕坐标。</summary>
    public int SpaceIndex
    {
        get => Step.At?.Space == FlowSpace.Screen ? 1 : 0;
        set => Edit(SpaceIndex, value, v => (Step.At ??= new FlowPoint()).Space = v == 1 ? FlowSpace.Screen : FlowSpace.Client);
    }

    public double X
    {
        get => Step.At?.X ?? 0;
        set => Edit(Step.At?.X ?? 0, value, v => (Step.At ??= new FlowPoint()).X = (int)v);
    }

    public double Y
    {
        get => Step.At?.Y ?? 0;
        set => Edit(Step.At?.Y ?? 0, value, v => (Step.At ??= new FlowPoint()).Y = (int)v);
    }

    public double FromX
    {
        get => Step.From?.X ?? 0;
        set => Edit(Step.From?.X ?? 0, value, v => (Step.From ??= new FlowPoint()).X = (int)v);
    }

    public double FromY
    {
        get => Step.From?.Y ?? 0;
        set => Edit(Step.From?.Y ?? 0, value, v => (Step.From ??= new FlowPoint()).Y = (int)v);
    }

    public double ToX
    {
        get => Step.To?.X ?? 0;
        set => Edit(Step.To?.X ?? 0, value, v => (Step.To ??= new FlowPoint()).X = (int)v);
    }

    public double ToY
    {
        get => Step.To?.Y ?? 0;
        set => Edit(Step.To?.Y ?? 0, value, v => (Step.To ??= new FlowPoint()).Y = (int)v);
    }

    public double RegionX
    {
        get => Step.Region?.X ?? 0;
        set => Edit(Step.Region?.X ?? 0, value, v => (Step.Region ??= new FlowRegion()).X = (int)v);
    }

    public double RegionY
    {
        get => Step.Region?.Y ?? 0;
        set => Edit(Step.Region?.Y ?? 0, value, v => (Step.Region ??= new FlowRegion()).Y = (int)v);
    }

    public double RegionWidth
    {
        get => Step.Region?.Width ?? 0;
        set => Edit(Step.Region?.Width ?? 0, value, v => (Step.Region ??= new FlowRegion()).Width = (int)v);
    }

    public double RegionHeight
    {
        get => Step.Region?.Height ?? 0;
        set => Edit(Step.Region?.Height ?? 0, value, v => (Step.Region ??= new FlowRegion()).Height = (int)v);
    }

    // ---------------------------------------------------------------- per-type fields

    /// <summary>0 = 左键，1 = 右键，2 = 中键。</summary>
    public int ButtonIndex
    {
        get => (Step.Button ?? "left") switch { "right" => 1, "middle" => 2, _ => 0 };
        set => Edit(ButtonIndex, value, v => Step.Button = v switch { 1 => "right", 2 => "middle", _ => "left" });
    }

    public double Delta
    {
        get => Step.Delta ?? 0;
        set => Edit(Step.Delta ?? 0, value, v => Step.Delta = (int)v);
    }

    /// <summary>
    /// The same field means different things per type, and a mislabelled box is a trap: "要输入的文字"
    /// over the box you fill in for "find the text and click it" reads like it should be typed.
    /// </summary>
    public string TextLabel => Step.Type switch
    {
        "type" => "要输入的文字",
        "wait-text" => "要等的文字",
        "click-text" => "要找的文字",
        "read-text" => "只取匹配到的文字（可留空 = 整块）",
        "key" => "要按的键（单个键名，配合组合键时留空）",
        _ => "文字",
    };

    public string Text
    {
        get => Step.Text ?? "";
        set => Edit(Step.Text ?? "", value, v => Step.Text = v);
    }

    public string Combo
    {
        get => Step.Combo ?? "";
        set => Edit(Step.Combo ?? "", value, v => Step.Combo = Blank(v));
    }

    public double Ms
    {
        get => Step.Ms ?? 0;
        set => Edit(Step.Ms ?? 0, value, v => Step.Ms = (int)v);
    }

    public double DurationMs
    {
        get => Step.DurationMs ?? 400;
        set => Edit(Step.DurationMs ?? 400, value, v => Step.DurationMs = (int)v);
    }

    public double TimeoutMs
    {
        get => Step.TimeoutMs ?? 5000;
        set => Edit(Step.TimeoutMs ?? 5000, value, v => Step.TimeoutMs = (int)v);
    }

    public double IntervalMs
    {
        get => Step.IntervalMs ?? (Step.Type == "type" ? 15 : 250);
        set => Edit(Step.IntervalMs ?? (Step.Type == "type" ? 15 : 250), value, v => Step.IntervalMs = (int)v);
    }

    /// <summary>0 = contains，1 = exact，2 = fuzzy。</summary>
    public int MatchIndex
    {
        get => (Step.Match ?? TextPredicate.Contains) switch
        {
            TextPredicate.Exact => 1,
            TextPredicate.Fuzzy => 2,
            _ => 0,
        };
        set => Edit(MatchIndex, value, v => Step.Match =
            v switch { 1 => TextPredicate.Exact, 2 => TextPredicate.Fuzzy, _ => TextPredicate.Contains });
    }

    public double MaxErrors
    {
        get => Step.MaxErrors ?? 1;
        set => Edit(Step.MaxErrors ?? 1, value, v => Step.MaxErrors = (int)v);
    }

    public double Confirm
    {
        get => Step.Confirm ?? 2;
        set => Edit(Step.Confirm ?? 2, value, v => Step.Confirm = (int)v);
    }

    /// <summary>`repeat`: how many times. `foreach`: the list variable. `read-text`: where the value
    /// goes. The children of a group are edited in the JSON preview for now - they are preserved on
    /// save either way, because the view model wraps whole step objects.</summary>
    public Visibility GroupVisibility => Ui.Show(Step.Type is "repeat" or "foreach");
    public Visibility CallVisibility => Ui.Show(Step.Type == "call");

    /// <summary>`call`: the subflow file, relative to this one. `vars` and `export` are edited in the
    /// JSON card - a table widget for them would be more UI than the feature needs right now.</summary>
    public string Flow
    {
        get => Step.Flow ?? "";
        set => Edit(Step.Flow ?? "", value, v => Step.Flow = Blank(v));
    }

    public Visibility IntoVisibility => Ui.Show(Step.Type is "read-text");
    public Visibility TimesVisibility => Ui.Show(Step.Type is "repeat");
    public Visibility InVisibility => Ui.Show(Step.Type is "foreach");

    public double Times
    {
        get => Step.Times ?? 0;
        set => Edit(Step.Times ?? 0, value, v => Step.Times = (int)v);
    }

    public string In
    {
        get => Step.In ?? "";
        set => Edit(Step.In ?? "", value, v => Step.In = Blank(v));
    }

    public string Into
    {
        get => Step.Into ?? "";
        set => Edit(Step.Into ?? "", value, v => Step.Into = Blank(v));
    }

    /// <summary>How many child steps a group carries, for the inspector's one-line summary.</summary>
    public string ChildrenLabel => Step.Steps is { Count: > 0 } children
        ? $"{children.Count} 个子步骤（在「这一步的 JSON」里编辑）"
        : "没有子步骤";

    // ---------------------------------------------------------------- when（前提条件）

    /// <summary>
    /// A step's precondition, editable here for the same reason every other field is: a feature the UI
    /// cannot express is a feature the UI does not have. Nothing is created until the first edit, so a
    /// step without a `when` stays without one.
    /// </summary>
    public Visibility WhenVisibility => Show(StepFields.When);

    private FlowCondition EnsureWhen()
    {
        Step.When ??= new FlowCondition
        {
            Region = new FlowRegion { Space = FlowSpace.Client, Width = 400, Height = 32 },
            Match = TextPredicate.Contains,
            MaxErrors = 1,
            TimeoutMs = 2000,
            IntervalMs = 250,
            Confirm = 2,
            Else = "skip",
        };
        return Step.When;
    }

    public string WhenText
    {
        get => Step.When?.Text ?? "";
        set => Edit(Step.When?.Text ?? "", value, v => EnsureWhen().Text = Blank(v));
    }

    public int WhenMatchIndex
    {
        get => Array.IndexOf(TextPredicate.Modes, Step.When?.Match ?? TextPredicate.Contains) is var i && i >= 0 ? i : 0;
        set => Edit(MatchIndex, value, v => EnsureWhen().Match = TextPredicate.Modes[Math.Clamp(v, 0, TextPredicate.Modes.Length - 1)]);
    }

    public double WhenMaxErrors
    {
        get => Step.When?.MaxErrors ?? 1;
        set => Edit(Step.When?.MaxErrors ?? 1, value, v => EnsureWhen().MaxErrors = (int)v);
    }

    public double WhenTimeoutMs
    {
        get => Step.When?.TimeoutMs ?? 2000;
        set => Edit(Step.When?.TimeoutMs ?? 2000, value, v => EnsureWhen().TimeoutMs = (int)v);
    }

    public bool WhenElseIsFail
    {
        get => Step.When?.Else == "fail";
        set => Edit(Step.When?.Else == "fail", value, v => EnsureWhen().Else = v ? "fail" : "skip");
    }

    public string WhenProcess
    {
        get => Step.When?.Target?.Process ?? "";
        set => Edit(Step.When?.Target?.Process ?? "", value, v =>
        {
            if (string.IsNullOrEmpty(v) && Step.When?.Target is { } existing && string.IsNullOrEmpty(existing.Class))
            {
                EnsureWhen().Target = null;   // 留空就是"用本步骤的目标"，不要留下空壳
                return;
            }
            EnsureWhen().Target = new FlowTarget { Process = Blank(v), Class = Step.When?.Target?.Class };
        });
    }

    public double WhenRegionX
    {
        get => Step.When?.Region?.X ?? 0;
        set => Edit(Step.When?.Region?.X ?? 0, value, v => EnsureWhen().Region!.X = (int)v);
    }

    public double WhenRegionY
    {
        get => Step.When?.Region?.Y ?? 0;
        set => Edit(Step.When?.Region?.Y ?? 0, value, v => EnsureWhen().Region!.Y = (int)v);
    }

    public double WhenRegionWidth
    {
        get => Step.When?.Region?.Width ?? 400;
        set => Edit(Step.When?.Region?.Width ?? 400, value, v => EnsureWhen().Region!.Width = (int)v);
    }

    public double WhenRegionHeight
    {
        get => Step.When?.Region?.Height ?? 32;
        set => Edit(Step.When?.Region?.Height ?? 32, value, v => EnsureWhen().Region!.Height = (int)v);
    }

    public string WhenLabel => Step.When is null
        ? "还没有前提：这一步每次都会执行。填上文字就会添加一个前提。"
        : $"前提：读到「{Step.When.Text}」才执行，否则 {(Step.When.Else == "fail" ? "失败（退出码 3）" : "跳过")}";

    public string Note
    {
        get => Step.Note ?? "";
        set => Edit(Step.Note ?? "", value, v => Step.Note = Blank(v));
    }
}
