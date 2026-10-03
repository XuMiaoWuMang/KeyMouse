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

    private static readonly string[] WithTarget =
        ["focus", "click", "drag", "move", "wheel", "type", "key", "wait-window", "wait-text"];

    public Visibility TargetVisibility => Ui.Show(WithTarget.Contains(Step.Type));
    public Visibility PointVisibility => Ui.Show(Step.Type is "click" or "move" or "wheel");
    public Visibility RegionVisibility => Ui.Show(Step.Type is "wait-text");
    public Visibility ButtonVisibility => Ui.Show(Step.Type is "click" or "drag");
    public Visibility DeltaVisibility => Ui.Show(Step.Type is "wheel");
    public Visibility TextVisibility => Ui.Show(Step.Type is "type" or "wait-text");
    public Visibility ComboVisibility => Ui.Show(Step.Type is "key");
    public Visibility MsVisibility => Ui.Show(Step.Type is "sleep");
    public Visibility DurationVisibility => Ui.Show(Step.Type is "drag");
    public Visibility TimeoutVisibility => Ui.Show(Step.Type is "wait-window" or "wait-text");
    public Visibility MatchVisibility => Ui.Show(Step.Type is "wait-text");
    public Visibility DragEndsVisibility => Ui.Show(Step.Type is "drag");
    public Visibility NoteVisibility => Ui.Show(Step.Type is not "sleep");

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

    public string Note
    {
        get => Step.Note ?? "";
        set => Edit(Step.Note ?? "", value, v => Step.Note = Blank(v));
    }
}
