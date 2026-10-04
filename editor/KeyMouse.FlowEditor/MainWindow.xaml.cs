using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage.Pickers;
using KeyMouse;
using KeyMouse.Runner;

namespace KeyMouse.FlowEditor;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs Everything = new(string.Empty);
    private readonly string? _tool = KeyMouseBridge.ResolveTool();
    private RunnerClient? _client;
    private int? _job;
    private bool _paused;
    private CancellationTokenSource? _run;

    public MainWindow()
    {
        InitializeComponent();

        // 工具栏左边那排"加一步"的按钮，按契约里的摘要做提示。
        FillPalette();
        VerticalSplitter.Target = InspectorColumn;
        HorizontalSplitter.Target = LogRow;

        Title = "KeyMouse 流程编辑器";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch (Exception)
        {
            // Mica needs Windows 11; without it the window is simply opaque.
        }

        Model.Steps.CollectionChanged += OnStepsChanged;
        SizeAndCenter(1320, 880);

        _settings = EditorSettings.Load();
        _surfaces = new Surfaces(this);
        StepList.KeyboardAccelerators[0].Invoked += (_, e) =>
        {
            DeleteSelected();
            e.Handled = true;
        };
        AppWindow.Closing += OnClosing;

        // 最小尺寸必须在**任何**缩放路径上都成立：PreferredMinimum* 只约束用户拖动，
        // 程序化 SetWindowPos 会绕过它（实测：我把窗口设成 1290 宽，它照做了）。
        AppWindow.Changed += (_, args) =>
        {
            if (!args.DidSizeChange) return;
            EnforceMinimumSize();
        };

        // 最小窗口尺寸：两位对手的最低标准是"任何尺寸下不出现半个字、动作一屏可达"。
        // 窗口按物理像素算，这里按 150% 缩放留出 900×640 逻辑的余地。
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 1360;
            presenter.PreferredMinimumHeight = 960;
        }

        // 日志最多占窗口高度的三成：它再长也不许把参数区压到看不见。
        Root.SizeChanged += (_, e) => CapLogHeight(e.NewSize.Height);

        // 启动时焦点给主操作（空状态的新建流程），而不是随机停在一个按钮上。
        // 焦点是有意指定的：没文档时给主操作（新建流程），有文档时给步骤列表。
        // 不指定的话 WinUI 会把焦点环停在工具栏第一个按钮上，看起来像"它被选中了"。
        Root.Loaded += (_, _) =>
        {
            if (_hasDocument) StepList.Focus(FocusState.Programmatic);
            else StartNewButton.Focus(FocusState.Programmatic);
        };

        UpdateStates();
        // 启动不弹提示：内部基础设施的状态（用哪个 exe、连没连上）不该打扰用户；真出问题会在下面报错。
    }


    /// <summary>
    /// Connects to the resident Runner, starting one if it is not running yet. One connection serves
    /// the whole session: picking, replaying, cancelling are all requests on it.
    /// </summary>
    private async Task<RunnerClient?> EnsureClientAsync()
    {
        if (_client is not null) return _client;

        _client = await KeyMouseBridge.ConnectAsync(_tool);
        if (_client is null)
        {
            Show("连不上 Runner",
                (_tool is null
                    ? "找不到 KeyMouse.exe。先构建 dist\\KeyMouse.exe，或把编辑器与它放在一起。"
                    : "启动 serve 之后没能连上命名管道。")
                + (RunnerClient.LastStartFailure is { Length: > 0 } why ? " 它说：" + why : ""),
                InfoBarSeverity.Error);
            return null;
        }

        // 连接成功不弹提示：它是背景设施。失败在上面的分支里已经说过。
        UpdateStates();
        return _client;
    }

    public EditorModel Model { get; } = new();

    /// <summary>选中的那一步有没有本次运行的证据（决定检查器里那张卡显不显示）。</summary>
    public bool HasShot => SelectedStep?.ShotPath is { Length: > 0 };

    private void NotifyShot() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasShot)));

    public event PropertyChangedEventHandler? PropertyChanged;

    public StepVm? SelectedStep { get; private set; }

    public Visibility HasSelection => Ui.Show(SelectedStep is not null);

    public Visibility NoSelection => Ui.Show(SelectedStep is null);

    // ------------------------------------------------------------------ window plumbing

    private void SizeAndCenter(int width, int height)
    {
        AppWindow.Resize(new SizeInt32(width, height));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (area is null) return;
        int x = area.WorkArea.X + (area.WorkArea.Width - width) / 2;
        int y = area.WorkArea.Y + (area.WorkArea.Height - height) / 2;
        AppWindow.Move(new PointInt32(Math.Max(area.WorkArea.X, x), Math.Max(area.WorkArea.Y, y)));
    }

    private EditorSettings _settings = new();
    private double _inspectorWidth = 540;
    private bool _hasDocument;
    private string[]? _paletteTypes;
    private int _paletteShown = -1;
    private Surfaces? _surfaces;

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateStates();

    private void UpdateStates()
    {
        bool any = Model.Steps.Count > 0;
        bool selected = SelectedStep is not null;
        PickButton.IsEnabled = SelectedStep is not null;
        DryRunButton.IsEnabled = any && _run is null;
        PlayButton.IsEnabled = any && _run is null;
        // 没有文档的时候，工作区整块留给"从哪儿开始"：右侧检查器此时没有任何东西可检查，
        // 留着它只会把空状态挤成左边一条。
        bool start = !_hasDocument;
        if (start && InspectorColumn.ActualWidth > 0) _inspectorWidth = InspectorColumn.ActualWidth;
        InspectorColumn.Width = start ? new GridLength(0) : new GridLength(1, GridUnitType.Star);   // 按比例，不钉像素
        VerticalSplitter.Visibility = Ui.Show(!start);

        // 收起起始面时要连**外层的滚动面**一起收：它盖在步骤列表上，留着就会吃掉所有点击与按键。
        StartScroll.Visibility = Ui.Show(start);
        EmptyState.Visibility = Ui.Show(start);
        EmptyDocHint.Visibility = Ui.Show(!start && !any);   // 空白文档：告诉人怎么加第一步
        RefreshStart();

        PropertyChanged?.Invoke(this, Everything);
    }

    private void OnStepSelected(object sender, SelectionChangedEventArgs e)
    {
        SelectedStep = StepList.SelectedItem as StepVm;
        foreach (StepVm row in Model.Steps) row.SetSelected(ReferenceEquals(row, SelectedStep));
        BuildInspector();
        UpdateStates();
    }

    /// <summary>
    /// 消息一律写进版面里（运行输出面板），不做浮动卡片。
    ///
    /// 理由是这个界面里**没有一处浮层可以不压东西**：右上压参数区、右下压日志、左下压步骤列表。
    /// 而"状态写在发生的位置"本来就是这套界面的原则——基础设施的状态写在输出面板顶部，
    /// 校验错误写在出错字段下方，运行结果写在结论条里，谁也不需要浮在别人脸上。
    /// </summary>
    private void Show(string title, string message, InfoBarSeverity severity)
    {
        string mark = severity switch
        {
            InfoBarSeverity.Error => "错误",
            InfoBarSeverity.Warning => "注意",
            _ => "提示",
        };
        Log($"# {mark}  {title}");
        if (message is { Length: > 0 }) Log($"         {message}");
        _logShown = true;
        LogRow.Height = new GridLength(Math.Min(200, Root.ActualHeight * 0.3));
        NotifyOutput();
    }


    private void Log(string line)
    {
        LogBox.Text += line + Environment.NewLine;
        LogBox.SelectionStart = LogBox.Text.Length;
        if (_logShown) return;

        // 第一行输出到了才让输出面板出现并让出高度：空面板不占地方。
        _logShown = true;
        LogRow.Height = new GridLength(Math.Min(200, Root.ActualHeight * 0.3));   // 有输出才占地方，且不超过窗口三成
        NotifyOutput();
    }


    private void NotifyOutput()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(OutputVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LogSplitterVisibility)));
    }

    // ------------------------------------------------------------------ files

    public void OpenFile(string path)
    {
        try
        {
            Model.Load(path);
        _hasDocument = true;
        _settings.Remember(path);
        RefreshStart();
            StepList.SelectedIndex = Model.Steps.Count > 0 ? 0 : -1;
            LogBox.Text = "";
            // 打开这件事走提示条，不占"运行输出"——那个面板只留给运行。
            // 打开成功不弹提示：文件路径已经在标题栏上，弹一次只是噪音。
        }
        catch (CommandFailure ex)
        {
            Show($"读不了这个流程（退出码 {ex.Code}）", ex.Message, InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            Show("读不了这个流程", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".json");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file is not null) OpenFile(file.Path);
    }

    /// <summary>
    /// 保存或运行之前先就地校验：错误的落点应该在**具体字段下方**，而不是等加载器回一句"第 N 步需要某某"。
    /// 校验读的是同一份契约，所以两边不会说两套话。
    /// </summary>
    private bool ReadyForUse()
    {
        int issues = Model.Validate();
        if (issues == 0) return true;

        BuildInspector();
        int firstBad = Model.Steps.ToList().FindIndex(s => s.HasIssues) + 1;
        Show("还差几项没填", $"第 {firstBad} 步有 {issues} 处必填项没填，已在对应字段下方标出。", InfoBarSeverity.Warning);
        return false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!ReadyForUse()) return;
        if (Model.DocumentPath is null)
        {
            OnSaveAs(sender, e);
            return;
        }
        try
        {
            Model.Save(Model.DocumentPath);
            Show("已保存", Model.DocumentPath, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("保存失败", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    private async void OnSaveAs(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("KeyMouse 流程", [".json"]);
        picker.SuggestedFileName = Model.DocumentPath is { Length: > 0 } existing
            ? Path.GetFileNameWithoutExtension(existing)
            : "flow";
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        try
        {
            Model.Save(file.Path);
            Show("已保存", file.Path, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("保存失败", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    // ------------------------------------------------------------------ editing

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int offset)
    {
        if (SelectedStep is not { } step) return;
        int target = Model.Steps.IndexOf(step) + offset;
        Model.Move(step, offset);
        StepList.SelectedIndex = target;
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Steps.IndexOf(step);
        Model.Duplicate(step);
        StepList.SelectedIndex = index + 1;
    }

    private void OnDelete(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Steps.IndexOf(step);
        Model.Remove(step);
        StepList.SelectedIndex = Model.Steps.Count == 0 ? -1 : Math.Min(index, Model.Steps.Count - 1);
    }

    /// <summary>
    /// Asks the console tool for a region. The picked rectangle and the window it belongs to land in
    /// whichever field the selected step actually uses, which is the whole point of having the picker
    /// inside the editor.
    /// </summary>
    /// <summary>
    /// 检查器完全由契约渲染（见 InspectorBuilder）：契约说这一步有哪些参数、哪些必须、取值与范围
    /// 是什么，界面就照做。这里只负责"选择变了就重建一次"——编辑时不重建，否则每敲一个字符都会
    /// 抢走输入焦点。
    /// </summary>
    // ---------------------------------------------------------------- 运行输出：有内容才占地方

    private bool _logShown;

    /// <summary>没输出时整块收起——一个空的输出面板占着半屏，是最没道理的浪费。</summary>
    public Visibility OutputVisibility => Ui.Show(_logShown);

    public Visibility LogSplitterVisibility => Ui.Show(_logShown);

    private void OnClearLog(object sender, RoutedEventArgs e)
    {
        LogBox.Text = "";
        _logShown = false;
        LogRow.Height = GridLength.Auto;
        RunSummary.Visibility = Visibility.Collapsed;
        NotifyOutput();
    }

    /// <summary>
    /// 工具栏左边是"往流程里加一步"——这是这个编辑器最常做的事，所以常用类型直接是一排按钮，
    /// 其余在"更多"里。打开/保存这类文件操作不常做，收进了"文件"菜单。
    /// </summary>
    private static readonly string[] CommonSteps = ["focus", "click", "type", "wait-text", "click-text"];

    private void FillPalette()
    {
        PaletteHost.Children.Clear();
        _paletteTypes = CommonSteps;
        int take = _paletteShown < 0 ? CommonSteps.Length : Math.Max(1, _paletteShown);
        foreach (string type in CommonSteps.Take(take))
        {
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["PaletteButton"],

                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = StepCatalog.GlyphOf(type), FontSize = 14 },
                        new TextBlock { Text = StepCatalog.NameOf(type), VerticalAlignment = VerticalAlignment.Center },
                    },
                },
            };
            ToolTipService.SetToolTip(button, DescribeStep(type));
            button.Click += (_, _) => AddStep(type);
            PaletteHost.Children.Add(button);
        }
    }

    /// <summary>按钮上的一句话提示来自契约（每种类型自己的摘要），不在这里另写一份。</summary>
    private static string DescribeStep(string type) => FlowSchema.TryGetStep(type, out FlowStepContract contract)
        ? $"{contract.SummaryZh}（{type}）"
        : type;

    private void AddStep(string type)
    {
        int index = SelectedStep is null ? Model.Steps.Count : Model.Steps.IndexOf(SelectedStep) + 1;
        Model.Add(type, index);
        StepList.SelectedIndex = index;
    }

    /// <summary>系统是否允许动画。关掉时一切过渡都免了。</summary>
    private static bool AnimationsEnabled =>
        new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

    private static async Task FadeInAsync(UIElement element)
    {
        element.Opacity = 0;
        for (int step = 1; step <= 6; step++)
        {
            element.Opacity = step / 6.0;
            await Task.Delay(20);
        }
    }

    // ---------------------------------------------------------------- 三个入口：新建、打开路径、更多步骤

    /// <summary>新建：清空当前流程（未保存时会先问一句），然后停在空状态等下一步。</summary>
    private async void OnNewFlow(object sender, RoutedEventArgs e)
    {
        if (Model.Steps.Count > 0 && !await ConfirmDiscardAsync("新建流程"))
        {
            return;
        }
        Model.Reset();
        _hasDocument = true;   // 新建之后是"空白文档"，不是"还没打开文件"
        StepList.SelectedIndex = -1;
        UpdateStates();
    }

    /// <summary>空状态里的"最近打开"用它；也顺手记住这次打开。</summary>
    internal void OpenPath(string path)
    {
        if (File.Exists(path)) OpenFile(path);
        else
        {
            _settings.Recent.Remove(path);
            RefreshStart();
            Show("文件不在了", path, InfoBarSeverity.Warning);
        }
    }

    /// <summary>async void 里抛出的异常会直接吞掉（界面上什么都看不到）。这里显式接住并说出来。</summary>
    private async void OnMoreSteps(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_surfaces is null) _surfaces = new Surfaces(this);
            await _surfaces.PickStepAsync(Root.XamlRoot, AddStep);
        }
        catch (Exception ex)
        {
            Show("打不开步骤选择面板", ex.Message, InfoBarSeverity.Error);
        }
    }
    private void RefreshStart() =>
        _surfaces?.FillRecent(RecentList, EmptyState, _settings);

    /// <summary>有未保存改动时问一句。工具不该在没提示的情况下丢掉别人的东西。</summary>
    private async Task<bool> ConfirmDiscardAsync(string action)
    {
        if (!Model.IsDirty) return true;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = $"{action}前先保存吗？",
            Content = "当前流程有未保存的改动。",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        // 不能在这里同步等待：ShowAsync 需要 UI 线程，同步等待会把 UI 线程锁死（点了没反应就是这个）。
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return false;
        if (result == ContentDialogResult.Primary) OnSave(this, new RoutedEventArgs());
        return true;
    }

    /// <summary>Delete 键删掉选中的那一步（右键菜单里也有同一条）。</summary>
    private void OnStepListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Delete) return;
        OnDelete(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    /// <summary>点检查器里的证据图 = 用系统看图工具打开原图。</summary>
    private void OnInspectorShotTapped(object sender, TappedRoutedEventArgs e)
    {
        if (SelectedStep?.ShotPath is not { Length: > 0 } path || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Show("打不开截图", ex.Message, InfoBarSeverity.Error); }
    }

    /// <summary>
    /// 常用步骤按可用宽度收放：窗口窄了就少放几个（其余仍在「更多」里），
    /// 绝不让某个按钮被裁成半个——那是最像"没做完"的一种残缺。
    /// </summary>
    private void OnPaletteSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_paletteTypes is null) return;
        double available = e.NewSize.Width;
        if (available <= 0) return;

        int fit = 0;
        double used = 0;
        foreach (string type in _paletteTypes)
        {
            double need = StepCatalog.NameOf(type).Length * 13 + 72;   // 图标 + 边距的粗略估算
            if (used + need > available && fit > 0) break;
            used += need;
            fit++;
        }
        if (fit == _paletteShown) return;
        _paletteShown = fit;
        FillPalette();
    }

    /// <summary>日志区高度上限：200px 与窗口高度三成取小。</summary>
    private void CapLogHeight(double windowHeight)
    {
        if (!_logShown || windowHeight <= 0) return;
        LogRow.Height = new GridLength(Math.Min(200, windowHeight * 0.3));
    }

    /// <summary>夹紧到最小尺寸。两位对手的最低标准：任何尺寸下不出现半个字、动作一屏可达。</summary>
    private void EnforceMinimumSize()
    {
        const int minWidth = 1360;    // 物理像素（150% 缩放下约合 907 逻辑像素）
        const int minHeight = 960;
        var size = AppWindow.Size;
        if (size.Width >= minWidth && size.Height >= minHeight) return;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Max(size.Width, minWidth), Math.Max(size.Height, minHeight)));
    }

    private void BuildInspector()
    {
        InspectorHost.Children.Clear();
        if (SelectedStep is not { } step) return;
        new InspectorBuilder(InspectorHost, step, path => PickInto(step, path, isRegion: false), path => PickInto(step, path, isRegion: true)).Build();
    }

    /// <summary>取一次区域或点，写进契约给的那条路径（`region` / `at` / `when.region` 都可能）。</summary>
    private async void PickInto(StepVm step, string path, bool isRegion)
    {
        try
        {
            RunnerClient? client = await EnsureClientAsync();
            if (client is null) return;

            Show("取区域", "用鼠标框一块区域：拖拽框选，单击选整个客户区，ESC 取消。", InfoBarSeverity.Informational);
            PickedRegion? picked = await KeyMouseBridge.PickRegionAsync(client);
            BringToFront();
            if (picked is null)
            {
                Show("取区域", "取消了，没有改动。", InfoBarSeverity.Warning);
                return;
            }

            if (isRegion)
            {
                step.SetRegion(path, new FlowRegion
                {
                    Space = FlowSpace.Client, X = picked.X, Y = picked.Y, Width = picked.Width, Height = picked.Height,
                });
            }
            else
            {
                step.SetPoint(path, new FlowPoint
                {
                    Space = picked.Space == "screen" ? FlowSpace.Screen : FlowSpace.Client, X = picked.X, Y = picked.Y,
                });
            }

            string prefix = path.Contains('.') ? path[..path.LastIndexOf('.')] : "target";
            if (picked.Process is { Length: > 0 }) step.SetText($"{prefix}.process", picked.Process);
            if (picked.Class is { Length: > 0 }) step.SetText($"{prefix}.class", picked.Class);
            if (picked.Title is { Length: > 0 }) step.SetText($"{prefix}.title", picked.Title);

            Show(isRegion ? "取区域" : "取点",
                $"记下 {picked.Width}x{picked.Height} @ {picked.X},{picked.Y}，窗口 {picked.Process ?? picked.Class ?? "（无）"}",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("取区域失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnPickRegion(object sender, RoutedEventArgs e)
    {
        if (SelectedStep is not { } step) return;
        try
        {
            RunnerClient? client = await EnsureClientAsync();
            if (client is null) return;

            Show("取区域", "用鼠标框一块区域：拖拽框选，单击选整个客户区，ESC 取消。", InfoBarSeverity.Informational);
            PickedRegion? picked = await KeyMouseBridge.PickRegionAsync(client);
            if (picked is null)
            {
                Show("取区域", "取消了，没有改动。", InfoBarSeverity.Warning);
                return;
            }

            // 写进契约里的参数名：这一步用 region 就写 region，否则写 at（点）。不按类型写死字段，
            // 而是问契约这一步有没有这个参数。
            bool wantsRegion = step.Fields.Any(f => f.Name == "region" && f.Kind == "region");
            if (wantsRegion)
            {
                step.SetRegion("region", new FlowRegion
                {
                    Space = FlowSpace.Client, X = picked.X, Y = picked.Y, Width = picked.Width, Height = picked.Height,
                });
            }
            else
            {
                step.SetPoint("at", new FlowPoint
                {
                    Space = picked.Space == "screen" ? FlowSpace.Screen : FlowSpace.Client,
                    X = picked.X, Y = picked.Y,
                });
            }
            step.SetText("target.process", picked.Process);
            step.SetText("target.class", picked.Class ?? "");
            step.SetText("target.title", picked.Title);

            Show("取区域", $"记下 {picked.Width}x{picked.Height} @ {picked.X},{picked.Y}，" +
                           $"窗口 {picked.Process ?? picked.Class ?? "（无）"}" +
                           (picked.Note is { Length: > 0 } note ? $"；{note}" : ""),
                InfoBarSeverity.Success);
            BringToFront();
        }
        catch (Exception ex)
        {
            Show("取区域失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Brings this window back to the front after the overlay took the foreground.</summary>
    private void BringToFront()
    {
        try { Activate(); }
        catch (Exception) { /* the window may already be closing */ }
    }

    // ------------------------------------------------------------------ running

    private async void OnDryRun(object sender, RoutedEventArgs e) => await RunAsync(dryRun: true);

    private async void OnPlay(object sender, RoutedEventArgs e) => await RunAsync(dryRun: false);

    private async void OnStopRun(object sender, RoutedEventArgs e)
    {
        if (_client is null || _job is not int job) return;
        CancellationTokenSource? run = _run;
        StopButton.IsEnabled = false;
        try
        {
            try
            {
                await KeyMouseBridge.CancelAsync(_client, job);
            }
            catch (Exception ex)
            {
                Show("取消请求没送到", ex.Message, InfoBarSeverity.Warning);
            }

            // 服务端要是不回话，界面不能就这么挂着：等 4 秒，然后自己撒手（RunAsync 的 finally
            // 会把按钮恢复）。之前正是这里缺了兜底——作业卡在 queued 时，"停止"没有任何出路。
            for (int i = 0; i < 40 && ReferenceEquals(_run, run) && run is not null; i++)
            {
                await Task.Delay(100);
            }
            if (ReferenceEquals(_run, run) && run is not null)
            {
                run.Cancel();
                Show("已经撒手", "常驻 Runner 没有回应停止请求，界面不再等它（那边如果还在跑，重启 Runner 即可收拾）。",
                    InfoBarSeverity.Warning);
            }
        }
        finally
        {
            StopButton.IsEnabled = true;
        }
    }

    /// <summary>Pause/resume a running flow: the Runner holds the execution between steps.</summary>
    private async void OnPauseRun(object sender, RoutedEventArgs e)
    {
        if (_client is null || _job is not int job) return;
        if (_paused)
        {
            await KeyMouseBridge.ResumeAsync(_client, job);
            _paused = false;
            PauseButton.Content = PauseLabel("暂停");
            Show("继续", "流程继续跑。", InfoBarSeverity.Informational);
        }
        else
        {
            await KeyMouseBridge.PauseAsync(_client, job);
            _paused = true;
            PauseButton.Content = PauseLabel("继续");
            Show("已暂停", "跑完当前这一步就会停住，等你说继续。", InfoBarSeverity.Warning);
        }
    }

    private static StackPanel PauseLabel(string text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            new FontIcon { Glyph = "\uE769", FontSize = 14 },
            new TextBlock { Text = text },
        },
    };

    private async Task RunAsync(bool dryRun)
    {
        if (_run is not null) return;

        RunnerClient? client = await EnsureClientAsync();
        if (client is null) return;

        string path;
        try
        {
            path = Model.SaveForRun();
        }
        catch (Exception ex)
        {
            Show("先存一份再跑", ex.Message, InfoBarSeverity.Error);
            return;
        }

        LogBox.Text = "";
        // 输出面板自己会出现（见 Log），不需要展开任何东西
        Log($"# KeyMouse serve 连接已建立；请求 run {path}{(dryRun ? "（--dry-run）" : "")}");
        Show(dryRun ? "试运行" : "播放", "正在跑…", InfoBarSeverity.Informational);

        _run = new CancellationTokenSource();
        _job = null;
        _paused = false;
        StopButton.Visibility = Visibility.Visible;
        PauseButton.Visibility = Visibility.Visible;
        PauseButton.Content = PauseLabel("暂停");
        UpdateStates();

        int exitCode;
        try
        {
            // 运行事件是从管道读取线程直接回调过来的，而 UI 只能在自己的线程上改：
// 不切线程的话，绑定更新会抛异常，而泵那边的 catch 会把它静静吃掉（实测：行内的运行结果一直不显示）。
// 所以这里统一排队回 UI 线程再处理。
exitCode = await KeyMouseBridge.RunFlowAsync(
    client, path, dryRun, Model.DocumentPath,
    e => DispatcherQueue.TryEnqueue(() => OnRunnerEvent(e)),
    _run.Token);
        }
        catch (OperationCanceledException)
        {
            Show("已停止", "界面不等了；流程在 Runner 那边最多跑完当前这一步。", InfoBarSeverity.Informational);
            return;
        }
        catch (Exception ex)
        {
            Show("跑不起来", ex.Message, InfoBarSeverity.Error);
            return;
        }
        finally
        {
            _run.Dispose();
            _run = null;
            _job = null;
            _paused = false;
            StopButton.Visibility = Visibility.Collapsed;
            PauseButton.Visibility = Visibility.Collapsed;
            UpdateStates();
            BringToFront();
        }

        ShowRunSummary(exitCode);
    }

    /// <summary>
    /// 运行结论写进"运行输出"面板顶部：几步成功、几步失败、卡在哪一步、退出码是什么意思。
    /// 不用一闪而过的提示打发人，也不弹模态挡住流程；结论一直留在那儿，直到下一次运行或清空。
    /// </summary>
    private void ShowRunSummary(int exitCode)
    {
        StepVm? failed = Model.Steps.FirstOrDefault(s => s.RunDetail.StartsWith("失败"));
        int ok = Model.Steps.Count(s => s.RunDetail.EndsWith("ms") && !s.RunDetail.StartsWith("失败"));
        long totalMs = Model.Steps.Sum(s => long.TryParse(s.RunDetail.Replace("失败 · ", "").Replace(" ms", ""), out long ms) ? ms : 0);

        bool success = exitCode == 0;
        RunSummaryIcon.Glyph = success ? "\uE73E" : "\uEA39";
        RunSummaryText.Text = success
            ? $"{ok} 步全部成功，共 {totalMs} ms。{Explain(0)}"
            : $"第 {Model.Steps.IndexOf(failed!) + 1} 步「{StepCatalog.NameOf(failed!.Step.Type)}」失败。"
              + (ok > 0 ? $"前面 {ok} 步成功。" : "") + Explain(exitCode);
        RunSummaryJump.Visibility = Ui.Show(!success);
        RunSummary.Visibility = Visibility.Visible;
        NotifyOutput();
    }

    /// <summary>点行上的缩略图 = 打开当时那张截图。用系统看图工具，不在应用里再造一个查看器。</summary>
    private void OnShotTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StepVm row } || row.ShotPath is not { Length: > 0 } path) return;
        if (!File.Exists(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Show("打不开截图", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>失败时一键跳到那一步，省得在大流程里自己找。</summary>
    private void OnJumpToFailure(object sender, RoutedEventArgs e)
    {
        StepVm? failed = Model.Steps.FirstOrDefault(s => s.RunDetail.StartsWith("失败"));
        if (failed is null) return;
        StepList.SelectedItem = failed;
        StepList.ScrollIntoView(failed);
    }

    /// <summary>
    /// Everything the Runner reports about the running job: its log lines, each step as it starts and
    /// finishes (which is what highlights the row), and the state the job is in.
    /// </summary>
    private void OnRunnerEvent(RunnerEvent e)
    {
        if (e.Job is int job) _job ??= job;
        switch (e.Kind)
        {
            case "log":
                Log(e.Line ?? "");
                break;

            case "step" when e.Index is int index:
                // 状态显示在**那一行上**，不为每一步弹一次提示：跑二十步弹二十次，那不叫反馈，叫噪音。
                if (index - 1 >= 0 && index - 1 < Model.Steps.Count)
                {
                    StepVm row = Model.Steps[index - 1];
                    if (e.State == "started")
                    {
                        row.MarkRunning();
                        StepList.SelectedIndex = index - 1;
                        Log($"  [{index}/{e.Total ?? Model.Steps.Count}] 开始 {StepCatalog.NameOf(e.Type ?? "")}");
                    }
                    else
                    {
                        row.MarkResult(e.ExitCode ?? 0, e.DurationMs ?? 0);
                        if (e.Shot is { Length: > 0 } shot && File.Exists(shot)) { row.SetShot(shot); NotifyShot(); }
                        Log($"  [{index}] {(e.ExitCode == 0 ? "完成" : $"失败（退出码 {e.ExitCode}）")}，{e.DurationMs} ms");
                    }
                }
                break;
        }
    }

    /// <summary>The exit codes mean the same thing here as on the command line - say so in the UI.</summary>
    private static string Explain(int code) => code switch
    {
        0 => "退出码 0：整条流程都成功了。",
        1 => "退出码 1：运行时出错（比如引擎起不来）。",
        2 => "退出码 2：用法/文件有问题（这个流程本身不合法）。",
        3 => "退出码 3：没等到、没匹配上，或者选区被取消。没有任何输入发出去，可以直接重试。",
        4 => "退出码 4：目标窗口现在不可用（最小化、被遮盖或拒绝绘制）。没有发输入。",
        5 => "退出码 5：焦点没验证通过，不敢往下按。没有发输入。",
        6 => "退出码 6：没读到内容（区域不对、引擎看不懂）。没有发输入。",
        7 => "退出码 7：被你（或客户端）取消了。取消点之后没有发出任何输入。",
        _ => $"退出码 {code}。",
    };

    // ------------------------------------------------------------------ closing

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!Model.IsDirty) return;
        args.Cancel = true;

        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "有未保存的改动",
            Content = "关掉之前要保存吗？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            OnSave(this, new RoutedEventArgs());
            if (Model.IsDirty) return; // saving failed or was cancelled: stay open
        }
        else if (result == ContentDialogResult.None)
        {
            return;
        }

        AppWindow.Closing -= OnClosing;
        Close();
    }
}
