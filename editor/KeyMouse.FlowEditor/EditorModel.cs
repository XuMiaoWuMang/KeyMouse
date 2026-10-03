using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using KeyMouse;

namespace KeyMouse.FlowEditor;

/// <summary>
/// The document being edited: the steps in order, where they came from, and whether they have been
/// touched. Saving rebuilds a real <see cref="FlowDocument"/> (the same type the console tool
/// writes) so what leaves the editor is byte-compatible with what `run` accepts.
/// </summary>
public sealed class EditorModel : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs Everything = new(string.Empty);

    private string? _documentPath;
    private bool _dirty;
    private string _savedJson = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StepVm> Steps { get; } = [];

    public string? DocumentPath => _documentPath;

    public string FileLabel => _documentPath is null ? "未命名流程" : Path.GetFileName(_documentPath);

    public string Subtitle => _documentPath is null
        ? "还没有保存到文件"
        : _documentPath;

    public string DirtyMark => _dirty ? "●" : "";

    public bool IsDirty => _dirty;

    public string Footer => $"{Steps.Count} 步" + (_dirty ? "，有未保存的改动" : "");

    /// <summary>
    /// Marks the document as edited - but decides by comparing content, not by counting edits.
    /// Measured: the inspector's NumberBox bindings write their value back on load, so a plain
    /// "something was touched" flag made a freshly opened file look modified.
    /// </summary>
    public void MarkDirty()
    {
        _dirty = Serialize() != _savedJson;
        Notify();
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, Everything);
        foreach (var step in Steps) step.Refresh();
    }

    public void Renumber()
    {
        for (int i = 0; i < Steps.Count; i++) Steps[i].Number = i + 1;
        PropertyChanged?.Invoke(this, Everything);
    }

    internal FlowDocument BuildDocument()
    {
        var document = new FlowDocument
        {
            RecordedAt = _documentPath is null ? null : null,
        };
        document.Steps.Clear();
        foreach (var step in Steps) document.Steps.Add(step.Step);
        return document;
    }

    /// <summary>Reads a flow file. Throws <see cref="CommandFailure"/> with the loader's own message.</summary>
    public void Load(string path)
    {
        FlowDocument document = FlowDocument.Load(path);
        Steps.Clear();
        foreach (var step in document.Steps) Steps.Add(new StepVm(step, this));
        _documentPath = path;
        _dirty = false;
        _savedJson = Serialize();
        Renumber();
        Notify();
    }

    public void Save(string path)
    {
        BuildDocument().Save(path);
        _documentPath = path;
        _dirty = false;
        _savedJson = Serialize();
        Renumber();
        Notify();
    }

    /// <summary>
    /// The file to hand to `run`. A saved, untouched document is used as it is; anything else is
    /// written to a scratch file instead, because "试运行/播放" must never silently overwrite the
    /// file the human is still editing.
    /// </summary>
    public string SaveForRun()
    {
        if (_documentPath is { Length: > 0 } && !_dirty) return _documentPath;
        string path = Path.Combine(Path.GetTempPath(), "keymouse-editor-run.json");
        BuildDocument().Save(path);
        return path;
    }

    public string Serialize() => JsonSerializer.Serialize(BuildDocument(), FlowDocument.Json);

    public void Add(string type, int index)
    {
        var step = new StepVm(StepCatalog.Create(type), this);
        index = Math.Clamp(index, 0, Steps.Count);
        Steps.Insert(index, step);
        _dirty = true;
        Renumber();
    }

    public void Remove(StepVm step)
    {
        if (!Steps.Remove(step)) return;
        _dirty = true;
        Renumber();
    }

    public void Duplicate(StepVm step)
    {
        int index = Steps.IndexOf(step);
        if (index < 0) return;
        var copy = StepVmFactory.Copy(step, this);
        Steps.Insert(index + 1, copy);
        _dirty = true;
        Renumber();
    }

    public void Move(StepVm step, int offset)
    {
        int index = Steps.IndexOf(step);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= Steps.Count) return;
        Steps.Move(index, target);
        _dirty = true;
        Renumber();
    }

    /// <summary>Called after the list reorders itself by drag: the collection is the order.</summary>
    public void AfterReorder()
    {
        _dirty = true;
        Renumber();
    }
}

/// <summary>Copies a step by round-tripping it through the shared JSON model.</summary>
internal static class StepVmFactory
{
    internal static StepVm Copy(StepVm source, EditorModel owner)
    {
        string json = JsonSerializer.Serialize(source.Step, FlowDocument.Json);
        var step = JsonSerializer.Deserialize<FlowStep>(json, FlowDocument.Json) ?? new FlowStep();
        return new StepVm(step, owner);
    }
}
