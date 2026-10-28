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

    private int? _durationMs;

    public string FileLabel => _documentPath is null ? "未命名流程" : Path.GetFileName(_documentPath);

    public string Subtitle => (_documentPath is null ? "还没有保存到文件" : _documentPath)
        + (_durationMs is int ms ? $" · 录制时长 {ms / 1000.0:0.#} 秒" : "");

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

        // 连续的鼠标移动先折叠：3 次以上的一组，头一行说"起点 → 终点 · N 次"，
        // 每一下移动单独成为它的子行。读流程的人要的是"从哪儿到哪儿"，
        // 四十几行 (x,y) 只会把真正要看的步骤挤出视野。
        // 先折叠"按住再松开"：长按在文件里是按下 + 若干等待 + 松开三步，
        // 读流程的人只想要一行"长按 鼠标左键 1.7 秒"。
        (int Head, int Offset, int Count)[] runs = FoldHolds();
        (int Head, int Offset, int Count)[] moves = FoldMoves();
        for (int i = 0; i < runs.Length; i++)
        {
            // 长按内部不参与移动折叠（它已经是子行）；长按之外的移动组照旧。
            if (runs[i].Head < 0 && moves[i].Head >= 0) runs[i] = moves[i];
        }

        // 编号按**看得见的顺序**走：折叠组占一个号，组内子行接在它下面。
        // 用底层下标编会出现 1、2、7 这种跳号——用户看的是这一屏，不是数组。
        int display = 0;
        int[] displayNo = new int[Steps.Count];
        for (int i = 0; i < Steps.Count; i++)
        {
            if (runs[i].Head >= 0 && runs[i].Head != i)
            {
                displayNo[i] = displayNo[runs[i].Head];
                continue;
            }
            displayNo[i] = ++display;
        }

        for (int i = 0; i < Steps.Count; i++)
        {
            StepVm top = Steps[i];
            top.Number = displayNo[i].ToString();
            top.Group = null;
            top.RunCount = 0;
            top.SummaryOverride = null;

            if (runs[i].Head == i && runs[i].Count > 0)
            {
                top.Depth = 0;
                top.RunCount = runs[i].Count;
                top.ChildTotal = runs[i].Count - 1;   // 头一行自己就是第一下移动
                top.SummaryOverride = top.Step.Type is "mousedown" or "keydown"
                    ? HoldSummary(i, runs[i].Count)
                    : MoveRunSummary(i, runs[i].Count);
                Rows.Add(top);
                continue;
            }

            if (runs[i].Head >= 0)
            {
                // 折叠组里的后续步骤：保留顶层身份（增删移仍按 Steps 走），只改显示层。
                // 收起时它们不出现在 Rows 里。
                top.Depth = 1;
                top.Number = $"{displayNo[runs[i].Head]}.{runs[i].Offset}";
                StepVm head = Steps[runs[i].Head];
                // 折叠后的子步骤属于这一行：运行结果要能沿 Group 找到它并累加，
                // 否则结果只会写进日志（截图里"740 次"却只累加到 2 步）。
                top.Group = head;
                if (head.IsExpanded || head.ChildTotal <= 1) Rows.Add(top);   // 只有一个子项的直接摊开
                continue;
            }

            top.Depth = 0;
            Rows.Add(top);

            if (top.Step.Type == "call" && top.Step.Flow is { Length: > 0 } flow)
            {
                // 被调用流程的步骤以**只读预览**接在 call 行下面：读流程的人不必来回切文件。
                // 只展开一层——嵌套调用的完整树会把账本变成翻页器。
                List<FlowStep>? preview = Preview(flow);
                top.PreviewCount = preview?.Count ?? 0;
                top.ChildTotal = top.PreviewCount;
                bool showPreview = top.IsExpanded || top.ChildTotal <= 1;
                for (int j = 0; j < top.PreviewCount && showPreview; j++)
                {
                    StepVm child = PreviewVm(preview![j], Path.GetFileName(flow));
                    child.Number = $"{displayNo[i]}.{j + 1}";
                    child.Depth = 1;
                    child.Group = top;
                    child.FirstInGroup = j == 0;
                    Rows.Add(child);
                }
            }

            if (top.Step.Steps is not { Count: > 0 } children) continue;
            top.ChildTotal = children.Count;
            bool showChildren = top.IsExpanded || top.ChildTotal <= 1;   // 只有一个子项的直接摊开
            for (int j = 0; j < children.Count && showChildren; j++)
            {
                StepVm child = VmFor(children[j]);
                child.Number = $"{displayNo[i]}.{j + 1}";
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
    /// 编辑一步之后，把文档里指向旧节点的那条引用换成新节点。
    ///
    /// <see cref="StepVm.Write"/> 是"按契约写回 JSON 再重建对象"，所以每一步编辑都会产生一个新的
    /// FlowStep。顶层步骤的引用就在 VM 上（BuildDocument 直接用它），**但子步骤的引用挂在父分组的
    /// Steps 列表里**——不换过去，子步骤怎么改都进不了文件（实测：改 2.1 的文字，跑起来仍是旧文字）。
    /// </summary>
    internal void Rebind(FlowStep oldNode, FlowStep newNode, StepVm vm)
    {
        _byNode.Remove(oldNode);
        _byNode[newNode] = vm;   // 身份不变：同一个 VM，只换了底下那个节点

        foreach (StepVm top in Steps)
        {
            if (ReferenceEquals(top.Step, oldNode)) return;   // 顶层：VM 自己就是权威引用
            if (top.Step.Steps is not { } children) continue;
            int index = children.IndexOf(oldNode);
            if (index < 0) continue;
            children[index] = newNode;
            return;
        }
    }

    /// <summary>点一下父行：展开或收起它的子项。只有一个子项时不响应（它本来就是展开的）。</summary>
    public StepVm Toggle(StepVm head)
    {
        if (head.ChildTotal <= 1) return head;
        head.IsExpanded = !head.IsExpanded;
        Renumber();
        return head;
    }

    /// <summary>
    /// 找出"按住 + 等待 + 松开"的组：mousedown…mouseup / keydown…keyup。
    /// 中间只允许 sleep / move（手会抖，视频里也常见），别的步骤出现就说明这不是一次长按。
    /// </summary>
    private (int Head, int Offset, int Count)[] FoldHolds()
    {
        var map = new (int Head, int Offset, int Count)[Steps.Count];
        for (int i = 0; i < map.Length; i++) map[i] = (-1, 0, 0);

        int k = 0;
        while (k < Steps.Count)
        {
            string open = Steps[k].Step.Type;
            if (open is not ("mousedown" or "keydown"))
            {
                k++;
                continue;
            }

            string close = open == "mousedown" ? "mouseup" : "keyup";
            int end = k + 1;
            while (end < Steps.Count && Steps[end].Step.Type is "sleep" or "move") end++;
            if (end >= Steps.Count || Steps[end].Step.Type != close)
            {
                k++;
                continue;
            }

            int count = end - k + 1;
            for (int m = k; m <= end; m++) map[m] = (k, m - k, count);
            k = end + 1;
        }

        return map;
    }

    /// <summary>长按那一行的摘要：键名 + 时长（时长优先取时间线，退回落差）。</summary>
    private string HoldSummary(int head, int count)
    {
        StepVm open = Steps[head];
        StepVm close = Steps[head + count - 1];
        string type = open.Step.Type == "mousedown" ? "鼠标" : "键盘";
        string name = open.Step.Type == "mousedown"
            ? (open.Step.Button ?? "left") switch
            {
                "right" => "鼠标右键",
                "middle" => "鼠标中键",
                "x1" => "鼠标侧键 1",
                "x2" => "鼠标侧键 2",
                _ => "未知键",
            }
            : $"键盘 {open.Step.Combo ?? open.Step.Text}";

        int ms = 0;
        if (open.Step.AtMs is int from && close.Step.AtMs is int to && to >= from) ms = to - from;
        for (int i = head + 1; i < head + count; i++)
        {
            if (Steps[i].Step.Type == "sleep" && Steps[i].Step.Ms is int sleepMs) ms = Math.Max(ms, sleepMs);
        }

        string duration = ms >= 1000 ? $"{ms / 1000.0:0.0} 秒" : $"{ms} ms";
        return $"长按 {name} · {duration}";
    }

    /// <summary>
    /// 找出连续 mouse move 的组（至少 3 次）。返回每个顶层下标所属的组：
    /// Head = 组头下标（组内每个成员都指向同一个头），Offset = 组内第几个，Count = 组大小。
    /// 不是组员的项 Head = -1。
    /// </summary>
    private (int Head, int Offset, int Count)[] FoldMoves()
    {
        var map = new (int Head, int Offset, int Count)[Steps.Count];
        for (int i = 0; i < map.Length; i++) map[i] = (-1, 0, 0);

        int k = 0;
        while (k < Steps.Count)
        {
            if (Steps[k].Step.Type != "move")
            {
                k++;
                continue;
            }

            int end = k;
            while (end + 1 < Steps.Count && Steps[end + 1].Step.Type == "move") end++;
            int count = end - k + 1;

            if (count >= 3)
            {
                for (int m = k; m <= end; m++) map[m] = (k, m - k, count);
            }
            k = end + 1;
        }

        return map;
    }

    /// <summary>折叠行的摘要：起点 → 终点。两个端点都来自真实步骤，不猜。</summary>
    private string MoveRunSummary(int head, int count)
    {
        string Point(StepVm vm) => vm.Step.At is { } at ? $"{at.X},{at.Y}" : "?";
        StepVm first = Steps[head];
        StepVm last = Steps[head + count - 1];
        return $"{Point(first)} → {Point(last)}";
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

    /// <summary>
    /// Reads a flow file. Throws <see cref="CommandFailure"/> with the loader's own message.
    ///
    /// <paramref name="keepPath"/> = false 用于**录制**：录到的内容进编辑区，但文件还是临时的，
    /// 不该被当成用户的文档路径（否则一按 Ctrl+S 就写回临时文件，人还以为存好了）。
    /// </summary>
    public void Load(string path, bool keepPath = true)
    {
        FlowDocument document = FlowDocument.Load(path);
        Steps.Clear();
        _byNode.Clear();
        foreach (var step in document.Steps) Steps.Add(new StepVm(step, this));
        _documentPath = keepPath ? path : null;
        // 录制时写进文件头的总时长：界面上要能看到，否则只能去翻 JSON（实测反馈）。
        _durationMs = document.DurationMs;
        _savedJson = Serialize();
        _dirty = !keepPath;
        Renumber();
        Notify();
    }

    /// <summary>
    /// 把每一步按契约检查一遍，返回有问题的字段总数。保存与运行之前先跑它：错误必须落到
    /// 具体字段上（检查器就地显示），而不是等加载器回一句"第 N 步需要某某"。
    /// </summary>
    internal int Validate() => Rows.Where(row => !row.IsPreview).Sum(step => step.Validate());
    /// <summary>
    /// 把当前状态当成"已保存"。打开文件后要调用：选中第一行会触发检查器把默认值写进模型，
    /// 那是渲染副作用，不是用户的改动——不抹掉的话，刚打开的文件立刻就是"有未保存的改动"，
    /// 每次播放、录制、打开都要先过一道确认框（实测）。
    /// </summary>
    internal void MarkClean()
    {
        _savedJson = Serialize();
        _dirty = false;
        Notify();
    }

    /// <summary>有没有未保存的改动。工具在丢掉别人的东西之前应该先问一句。</summary>

    /// <summary>新建：清空步骤与文件路径，回到空状态。</summary>
    internal void Reset()
    {
        _durationMs = null;
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
        string path = KeyMouse.Scratch.PathFor("keymouse-editor-run.json");
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
            // 加完必须让这个组开着：只有一个子项时它是"默认摊开"的，加到第二个就会被收起，
            // 新加的行立刻消失、之后的"添加"也不再落在这个组里（实测反馈）。
            anchor.Group.IsExpanded = true;
        }
        else if (anchor.IsGroup)
        {
            // 选中分组行再点一个动作 = 往这个组里加第一步。空组也能因此被填上。
            anchor.Step.Steps ??= [];
            anchor.Step.Steps.Add(fresh.Step);
            anchor.IsExpanded = true;
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
