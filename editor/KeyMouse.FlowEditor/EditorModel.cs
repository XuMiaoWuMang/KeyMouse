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

    /// <summary>
    /// 列表真正显示的东西：顶层步骤 + 每个分组里的子步骤，按文件里的顺序摊平。
    /// <see cref="Steps"/> 仍然是权威（写文件、增删顶层都只认它），这里只负责"看得见的顺序"。
    /// 子步骤的 VM 按 FlowStep 节点缓存，重建显示行时不会把用户正选着的那一行换成另一个对象。
    /// </summary>
    public ObservableCollection<StepVm> Rows { get; } = [];

    private readonly Dictionary<FlowStep, StepVm> _byNode = [];

    public string? DocumentPath => _documentPath;

    public string FileLabel => _documentPath is null ? "未命名流程" : Path.GetFileName(_documentPath);

    public string Subtitle => _documentPath is null
        ? "还没有保存到文件"
        : _documentPath;

    public string DirtyMark => _dirty ? "●" : "";

    public bool IsDirty => _dirty;

    public string Footer => $"{Rows.Count} 步" + (_dirty ? "，有未保存的改动" : "");

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
        foreach (var step in Rows) step.Refresh();
    }

    /// <summary>
    /// 重排编号并重建显示行。编号是分层的：顶层 1、2、3；组里的子步骤 2.1、2.2。
    /// 子步骤与它的分组之间靠 <see cref="StepVm.Group"/> 连着，界面据此画分支。
    /// </summary>
    public void Renumber()
    {
        Rows.Clear();
        for (int i = 0; i < Steps.Count; i++)
        {
            StepVm top = Steps[i];
            top.Number = (i + 1).ToString();
            top.Depth = 0;
            top.Group = null;
            Rows.Add(top);

            if (top.Step.Type == "call" && top.Step.Flow is { Length: > 0 } flow)
            {
                // 被调用流程的步骤以**只读预览**接在 call 行下面：读流程的人不必来回切文件。
                // 只展开一层——嵌套调用的完整树会把账本变成翻页器。
                List<FlowStep>? preview = Preview(flow);
                top.PreviewCount = preview?.Count ?? 0;
                for (int j = 0; j < top.PreviewCount; j++)
                {
                    StepVm child = PreviewVm(preview![j], Path.GetFileName(flow));
                    child.Number = $"{i + 1}.{j + 1}";
                    child.Depth = 1;
                    child.Group = top;
                    child.FirstInGroup = j == 0;
                    Rows.Add(child);
                }
            }

            if (top.Step.Steps is not { Count: > 0 } children) continue;
            for (int j = 0; j < children.Count; j++)
            {
                StepVm child = VmFor(children[j]);
                child.Number = $"{i + 1}.{j + 1}";
                child.Depth = 1;
                child.Group = top;
                child.FirstInGroup = j == 0;
                Rows.Add(child);
            }
        }

        // 行号与缩进都是 OneWay 绑定：不逐个通知，插一步之后后面的行就会重号（实测 1,2,3,3）
        foreach (var row in Rows) row.Refresh();
        PropertyChanged?.Invoke(this, Everything);
    }

    /// <summary>
    /// 按 FlowStep 节点找回它对应的显示行。Runner 报的是**执行序号**（循环已展开、子流程已
    /// 内联），要落到界面上哪一行，必须回到同一个节点上认人——内联进来的子流程步骤不属于
    /// 这个文件，所以返回 null，调用方只记日志、不点错行。
    /// </summary>
    internal StepVm? VmForNode(FlowStep? node)
    {
        if (node is null) return null;
        foreach (StepVm vm in Rows)
        {
            if (ReferenceEquals(vm.Step, node)) return vm;
        }
        return VmFor(node) is { } cached && _byNode.ContainsKey(node) ? cached : null;
    }

    private readonly Dictionary<FlowStep, StepVm> _previewVms = [];
    private readonly Dictionary<string, (DateTime Stamp, List<FlowStep>? Steps)> _previewCache = [];

    /// <summary>
    /// 读一个子流程文件，只为**预览**它的步骤（相对调用它的文档解析路径）。
    /// 缓存按文件时间戳失效；读不出来就返回 null——坏掉的子流程不该拦住父流程的编辑。
    /// </summary>
    private List<FlowStep>? Preview(string flow)
    {
        string? baseDir = _documentPath is { Length: > 0 } ? Path.GetDirectoryName(_documentPath) : null;
        if (baseDir is null) return null;

        string full = Path.GetFullPath(Path.Combine(baseDir, flow));
        DateTime stamp = File.Exists(full) ? File.GetLastWriteTimeUtc(full) : default;
        if (_previewCache.TryGetValue(full, out (DateTime Stamp, List<FlowStep>? Steps) hit) && hit.Stamp == stamp) return hit.Steps;

        List<FlowStep>? steps = null;
        if (File.Exists(full))
        {
            try
            {
                steps = FlowDocument.Load(full).Steps;
            }
            catch (CommandFailure)
            {
                steps = null;   // 子流程本身不合法：只在它自己的文件里报，不在这里造第二个错误源
            }
            catch (Exception)
            {
                steps = null;
            }
        }

        _previewCache[full] = (stamp, steps);
        return steps;
    }

    private StepVm PreviewVm(FlowStep step, string source)
    {
        if (!_previewVms.TryGetValue(step, out StepVm? vm))
        {
            vm = new StepVm(step, this);
            _previewVms[step] = vm;
        }
        vm.MarkPreview(source);
        return vm;
    }

    private StepVm VmFor(FlowStep node)
    {
        if (_byNode.TryGetValue(node, out StepVm? existing)) return existing;
        var vm = new StepVm(node, this);
        _byNode[node] = vm;
        return vm;
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
        _byNode.Clear();
        foreach (var step in document.Steps) Steps.Add(new StepVm(step, this));
        _documentPath = path;
        _dirty = false;
        _savedJson = Serialize();
        Renumber();
        Notify();
    }

    /// <summary>
    /// 把每一步按契约检查一遍，返回有问题的字段总数。保存与运行之前先跑它：错误必须落到
    /// 具体字段上（检查器就地显示），而不是等加载器回一句"第 N 步需要某某"。
    /// </summary>
    internal int Validate() => Rows.Where(row => !row.IsPreview).Sum(step => step.Validate());
    /// <summary>有没有未保存的改动。工具在丢掉别人的东西之前应该先问一句。</summary>

    /// <summary>新建：清空步骤与文件路径，回到空状态。</summary>
    internal void Reset()
    {
        Steps.Clear();
        _byNode.Clear();
        _documentPath = null;
        _dirty = false;
        _savedJson = Serialize();
        Notify();
    }

    public void Save(string path)
    {
        BuildDocument().Save(path);

        // Read it back through the loader before calling it saved. The editor can now build a step the
        // format will refuse (a `when` with no text is the easy one, since filling in the box is what
        // creates it), and finding that out here costs a second instead of costing a run.
        try
        {
            FlowDocument.Load(path);
        }
        catch (CommandFailure ex)
        {
            _documentPath = path;      // 文件已经写出去了，但先别当它"干净"
            _dirty = true;
            Notify();
            throw new CommandFailure(ex.Code, $"文件已写出，但格式检查没过：{ex.Message}");
        }

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

    /// <summary>
    /// 在 anchor 之后加一步。锚点是组里的子步骤时，新步骤加进那个组——所见即所得，
    /// 不需要用户先知道"这是组内"这件事。
    /// </summary>
    public StepVm Add(string type, StepVm? anchor)
    {
        // 锚点在子流程预览里：往它所属的 call 行之后加，绝不改另一个文件。
        if (anchor is { IsPreview: true }) anchor = anchor.Group;

        var fresh = new StepVm(StepCatalog.Create(type), this);
        if (anchor is null) Steps.Add(fresh);
        else if (anchor.Group?.Step.Steps is { } nested)
        {
            int at = nested.IndexOf(anchor.Step);
            nested.Insert(at < 0 ? nested.Count : at + 1, fresh.Step);
        }
        else if (anchor.IsGroup)
        {
            // 选中分组行再点一个动作 = 往这个组里加第一步。空组也能因此被填上。
            anchor.Step.Steps ??= [];
            anchor.Step.Steps.Add(fresh.Step);
        }
        else
        {
            int at = Steps.IndexOf(anchor);
            Steps.Insert(at < 0 ? Steps.Count : at + 1, fresh);
        }

        _dirty = true;
        Renumber();
        return _byNode.TryGetValue(fresh.Step, out StepVm? display) ? display : fresh;
    }

    public void Remove(StepVm step)
    {
        if (step.IsPreview) return;   // 只读预览行
        if (step.Group?.Step.Steps is { } nested)
        {
            if (!nested.Remove(step.Step)) return;
            _byNode.Remove(step.Step);
        }
        else if (!Steps.Remove(step)) return;

        _dirty = true;
        Renumber();
    }

    public void Duplicate(StepVm step)
    {
        if (step.IsPreview) return;
        StepVm copy = StepVmFactory.Copy(step, this);
        if (step.Group?.Step.Steps is { } nested)
        {
            int at = nested.IndexOf(step.Step);
            if (at < 0) return;
            nested.Insert(at + 1, copy.Step);
        }
        else
        {
            int at = Steps.IndexOf(step);
            if (at < 0) return;
            Steps.Insert(at + 1, copy);
        }

        _dirty = true;
        Renumber();
    }

    public void Move(StepVm step, int offset)
    {
        if (step.IsPreview) return;
        if (step.Group?.Step.Steps is { } nested)
        {
            int from = nested.IndexOf(step.Step);
            int to = from + offset;
            if (from < 0 || to < 0 || to >= nested.Count) return;
            (nested[from], nested[to]) = (nested[to], nested[from]);
        }
        else
        {
            int from = Steps.IndexOf(step);
            int to = from + offset;
            if (from < 0 || to < 0 || to >= Steps.Count) return;
            Steps.Move(from, to);
        }

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
