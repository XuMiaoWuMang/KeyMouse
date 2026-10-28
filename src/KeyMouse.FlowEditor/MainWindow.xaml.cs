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

    // 输出面板与列表更新都是**按帧合并**的：631 步的流程会瞬间产生 1200+ 条事件，
    // 一条一条改控件会把 UI 线程噎死（实测：窗口标题直接变「未响应」）。
    private readonly System.Text.StringBuilder _pending = new();
    private int _pendingLines;
    private readonly System.Text.StringBuilder _logText = new();
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _flush;
    private static string Elevation()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator)
                ? "管理员"
                : "普通权限";
        }
        catch (Exception)
        {
            return "权限未知";
        }
    }

    private StepVm? _runningRow;
    private int _runSteps;      // 本次运行执行了多少步（含折叠在行里的子步骤）
    private int _runFailures;   // 其中失败了几步
    private long _runTotalMs;   // 各步执行耗时之和（不含步骤之间的等待）
    private readonly System.Diagnostics.Stopwatch _runClock = new();   // 墙钟：整次运行到底花了多久
    private StepVm? _scrollTo;
    private const int MaxLogLines = 900;

    public MainWindow()
    {
        InitializeComponent();

        // 工具栏左边那排"加一步"的按钮，按契约里的摘要做提示。
        FillPalette();
        VerticalSplitter.Target = InspectorColumn;
        HorizontalSplitter.Target = LogRow;
        VerticalSplitter.Track(Root);
        HorizontalSplitter.Track(Root);
        HorizontalSplitter.Changed += () =>
        {
            // 拖动中不管：手指还在拖着，这时候收起来会跟手势打架。松手后再判断。
            if (HorizontalSplitter.IsDragging || !_logShown) return;
            if (LogRow.ActualHeight > 24) return;

            _logShown = false;
            LogRow.Height = GridLength.Auto;
            NotifyOutput();
        };
        Root.SizeChanged += (_, _) => ClampPanes();

        // 标题带构建时间：这一轮反复被"跑的是旧 exe"骗过，让人一眼能看出手上这份是不是刚编的。
        string built = "未知";
        try { built = File.GetLastWriteTime(typeof(MainWindow).Assembly.Location).ToString("MM-dd HH:mm"); }
        catch (Exception) { }
        // 权限写进标题：开发构建（dotnet build）**不会**嵌入 requireAdministrator，
        // 只有发布产物（dotnet publish）才有——同一份代码两种权限，不写出来就要靠猜（实测踩过）。
        // 版本从程序集取（根 Directory.Build.props 的 ProductVersion），标题里不再写死数字。
        Title = $"KeyMouse 流程编辑器 {KeyMouse.Commands.ProductVersion}（构建 {built} · {Elevation()}）";
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
        // 删除键由 OnStepListKeyDown 处理。这里曾经挂着 ListView 的 KeyboardAccelerator，
        // 但那会给列表生成一个"删除"提示气泡（WinUI 为加速器自动加 tooltip），已经去掉了。
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

        try
        {
            _client = await KeyMouseBridge.ConnectAsync(_tool);
        }
        catch (RunnerAccessDeniedException ex)
        {
            // 管道被更高权限的 Runner 占着：说得清楚一点，别让用户对着"未指定的错误"猜。
            Show("连不上 Runner", ex.Message, InfoBarSeverity.Error);
            return null;
        }

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

    // ------------------------------------------------------------------ 调试视图：默认收起

    private bool _jsonOpen;

    /// <summary>
    /// "这一步的 JSON"是调试视图，默认收起：它一展开就把检查器顶出视口，最后那个框会被窗口底边切掉一半。
    /// 收起不是省钱，是让默认状态下的面板以一条干净的线结束。
    /// </summary>
    public Visibility JsonVisibility => Ui.Show(_jsonOpen);

    public string JsonChevron => _jsonOpen ? "\uE70E" : "\uE70D";

    private void OnToggleJson(object sender, RoutedEventArgs e)
    {
        _jsonOpen = !_jsonOpen;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(JsonVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(JsonChevron)));
    }

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
        bool any = Model.Rows.Count > 0;
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

    /// <summary>
    /// 点父行 = 展开 / 收起它的子项。用 Tapped 而不是 SelectionChanged：
    /// 键盘上下键换行也会改选中，那不该顺带开合。
    /// </summary>
    /// <summary>
    /// 两栏各自的可用区间。右栏再宽也不能把账本挤到看不见内容——拖到头就停住，
    /// 而不是继续变宽、把左边的行裁掉（实测反馈）。
    /// </summary>
    private void ClampPanes()
    {
        const double LedgerMin = 420;    // 一行摘要 + 缩进还读得下去的最小宽度
        const double LedgerMinAbsolute = 320;
        const double InspectorMin = 380;

        double card = Math.Max(LedgerMin + InspectorMin, Root.ActualWidth - 32);

        // 右栏的天花板：留够账本；窗口很窄时退让到绝对下限，但绝不让它压没。
        VerticalSplitter.MinTarget = InspectorMin;
        VerticalSplitter.MaxTarget = Math.Max(InspectorMin, card - 8 - LedgerMin);

        // 已经是像素宽度时顺手夹一次：窗口变小后旧的宽度可能已经过分。
        if (InspectorColumn.Width.IsAbsolute && InspectorColumn.Width.Value > VerticalSplitter.MaxTarget)
        {
            InspectorColumn.Width = new GridLength(VerticalSplitter.MaxTarget);
        }

        HorizontalSplitter.MinTarget = 0;
        HorizontalSplitter.MaxTarget = Math.Max(160, Root.ActualHeight * 0.55);
        _ = LedgerMinAbsolute;
    }

    /// <summary>
    /// 把账本列按自身尺寸裁死。WinUI 的 Grid 不裁剪子元素，列表里任何落在右缘的绘制
    /// （展开的滚动条、悬停底、拖影）都会漏到分隔线那条缝里——实测在缝上看到过一块米色。
    /// </summary>
    private void OnLedgerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement host) return;
        host.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
        };
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StepVm row) return;
        if (row.ChildTotal <= 1) return;

        Model.Toggle(row);
        StepList.SelectedItem = row;   // 重建显示行之后把选中还给这一行
    }

    private void OnStepSelected(object sender, SelectionChangedEventArgs e)
    {
        SelectedStep = StepList.SelectedItem as StepVm;
        foreach (StepVm row in Model.Rows) row.SetSelected(ReferenceEquals(row, SelectedStep));
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
    }


    private void Log(string line)
    {
        // 只入队，不碰控件：真正的写入由 80ms 一次的合并刷新完成。
        _pending.Append(line).Append('\n');
        _pendingLines++;
        OpenLog();
        EnsureFlush();
    }

    private void EnsureFlush()
    {
        if (_flush is not null) return;

        _flush = DispatcherQueue.CreateTimer();
        _flush.Interval = TimeSpan.FromMilliseconds(80);
        _flush.IsRepeating = true;
        _flush.Tick += (_, _) => FlushUi();
        _flush.Start();
    }

    private void FlushUi()
    {
        if (_pending.Length > 0)
        {
            _logText.Append(_pending);
            _pending.Clear();
            _pendingLines = 0;

            // 只留最后 MaxLogLines 行：跑一小时的流程不该把整个文本堆在内存里。
            if (_logText.Length > 400_000)
            {
                string all = _logText.ToString();
                int cut = all.IndexOf('\n', Math.Max(0, all.Length - 200_000));
                _logText.Clear().Append(cut >= 0 ? all[(cut + 1)..] : all);
            }

            LogBox.Text = _logText.ToString();
            LogBox.SelectionStart = LogBox.Text.Length;
        }

        if (_scrollTo is { } row)
        {
            _scrollTo = null;
            StepList.SelectedItem = row;
            StepList.ScrollIntoView(row);
        }
    }

    /// <summary>
    /// 只在面板**从无到有**的时候给一个默认高度。
    ///
    /// 以前这里只要有输出就重置一次高度，于是把输出拉到最大之后，下一条日志一到面板就缩回默认大小
    /// ——看起来像"到顶就回档"（实测反馈）。用户调过的尺寸不该被日志行数决定。
    /// </summary>
    private void OpenLog()
    {
        if (_logShown) return;
        _logShown = true;
        LogRow.Height = new GridLength(Math.Clamp(Root.ActualHeight * 0.3, 140, 260));
        NotifyOutput();
    }


    private void NotifyOutput()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(OutputVisibility)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(OutputChipVisibility)));
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
            StepList.SelectedIndex = Model.Rows.Count > 0 ? 0 : -1;
            Model.MarkClean();   // 检查器渲染时写的默认值不算用户的改动
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
        string? path = await Picker.OpenFlowAsync(WinRT.Interop.WindowNative.GetWindowHandle(this));
        if (path is not null) OpenFile(path);
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
        int firstBad = Model.Rows.ToList().FindIndex(s => s.HasIssues) + 1;
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
        string suggested = Model.DocumentPath is { Length: > 0 } existing
            ? Path.GetFileNameWithoutExtension(existing)
            : "flow";
        string? path = await Picker.SaveFlowAsync(WinRT.Interop.WindowNative.GetWindowHandle(this), suggested);
        if (path is null) return;
        try
        {
            Model.Save(path);
            Show("已保存", path, InfoBarSeverity.Success);
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
        int target = Model.Rows.IndexOf(step) + offset;
        Model.Move(step, offset);
        StepList.SelectedIndex = target;
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Rows.IndexOf(step);
        Model.Duplicate(step);
        StepList.SelectedIndex = index + 1;
    }

    private void OnDelete(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Rows.IndexOf(step);
        Model.Remove(step);
        StepList.SelectedIndex = Model.Rows.Count == 0 ? -1 : Math.Min(index, Model.Rows.Count - 1);
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

    /// <summary>
    /// 这次运行的计划（循环展开、子流程内联后的线性序列）。它只用来回答一个界面问题：
    /// Runner 报的"第 N 个执行项"对应界面上哪一行。
    /// </summary>
    private FlowPlan? _plan;

    /// <summary>没输出时整块收起——一个空的输出面板占着半屏，是最没道理的浪费。</summary>
    public Visibility OutputVisibility => Ui.Show(_logShown);

    /// <summary>面板收起来时露出的那一小块「输出」。</summary>
    public Visibility OutputChipVisibility => Ui.Show(!_logShown);

    /// <summary>把输出面板叫回来（默认高度：窗口高度的三成，夹在 140–260 之间）。</summary>
    private void OnShowOutput(object sender, RoutedEventArgs e) => OpenLog();

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
    // 顶部快捷栏只放**最常用**的四个：切换窗口/长按这些留在「更多」里，不占位置。
    private static readonly string[] CommonSteps = ["click", "type", "wait-text", "click-text"];

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

    /// <summary>
    /// 加一步：加在选中那一步之后；选中的是组内子步骤，就加进那个组里（所见即所得）。
    /// 返回值是显示行里真实的那个 VM，选中它而不是"下一个位置"。
    /// </summary>
    private void AddStep(string type)
    {
        StepVm added = Model.Add(type, SelectedStep);
        StepList.SelectedItem = added;
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
        if (Model.Rows.Count > 0 && !await ConfirmDiscardAsync("新建流程"))
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

    private void OnMoreSteps(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_surfaces is null) _surfaces = new Surfaces(this);
            _surfaces.PickStep(MoreStepsButton, AddStep);
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

    /// <summary>
    /// 窗口变小以后，日志区如果还占着原来的高度就会把编辑区挤没——这时只**往下压到上限**，
    /// 不去"恢复默认高度"：用户自己拉出来的尺寸，只有在真的放不下时才动它。
    /// </summary>
    private void CapLogHeight(double windowHeight)
    {
        if (!_logShown || windowHeight <= 0) return;

        double cap = Math.Clamp(windowHeight * 0.55, 160, 640);
        if (!LogRow.Height.IsAbsolute || LogRow.Height.Value <= cap) return;
        LogRow.Height = new GridLength(cap);
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

        // 子流程预览行是只读的：这里不渲染契约字段（那会让人以为改了会生效），
        // 只说清楚它来自哪个文件、该去哪儿改。
        if (step.IsPreview)
        {
            InspectorHost.Children.Add(new TextBlock
            {
                Text = step.PreviewNote,
                Style = (Style)Application.Current.Resources["FieldCaption"],
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        new InspectorBuilder(InspectorHost, step, path => PickInto(step, path, isRegion: false), path => PickInto(step, path, isRegion: true)).Build();
    }

    /// <summary>取一次区域或点，写进契约给的那条路径（`region` / `at` / `when.region` 都可能）。</summary>
    private async void PickInto(StepVm step, string path, bool isRegion)
    {
        try
        {
            RunnerClient? client = await EnsureClientAsync();
            if (client is null) return;

            if (isRegion)
                Show("取区域", "用鼠标框一块区域：拖拽框选，单击选整个客户区，ESC 取消。", InfoBarSeverity.Informational);
            else
                Show("取点", "在屏幕上点一下要操作的位置，ESC 或右键取消。", InfoBarSeverity.Informational);

            PickedRegion? picked = isRegion
                ? await KeyMouseBridge.PickRegionAsync(client)
                : await KeyMouseBridge.PickPointAsync(client);
            BringToFront();
            if (picked is null)
            {
                Show(isRegion ? "取区域" : "取点", "取消了，没有改动。", InfoBarSeverity.Warning);
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
                // 没有指定窗口的步骤：存**屏幕坐标**，就到那个位置发事件，跟谁在前台无关。
                bool anchored = step.GetText("target.process") is { Length: > 0 }
                                || step.GetText("target.class") is { Length: > 0 };
                step.SetPoint(path, anchored
                    ? new FlowPoint { Space = picked.Space == "screen" ? FlowSpace.Screen : FlowSpace.Client, X = picked.X, Y = picked.Y }
                    : new FlowPoint { Space = FlowSpace.Screen, X = picked.ScreenX, Y = picked.ScreenY });
            }

            // 只有本来就有窗口的步骤才回填窗口；没窗口的步骤不该因为取了个点就被绑上窗口。
            bool hasWindow = step.GetText("target.process") is { Length: > 0 }
                             || step.GetText("target.class") is { Length: > 0 };
            string prefix = path.Contains('.') ? path[..path.LastIndexOf('.')] : "target";
            if (hasWindow)
            {
                if (picked.Process is { Length: > 0 }) step.SetText($"{prefix}.process", picked.Process);
                if (picked.Class is { Length: > 0 }) step.SetText($"{prefix}.class", picked.Class);
                if (picked.Title is { Length: > 0 }) step.SetText($"{prefix}.title", picked.Title);
            }

            // 数值是**程序写进去**的：输入框不会自己知道，必须重建面板，
            // 否则取了点以后 X/Y 还显示 0（实测）。
            BuildInspector();

            Show(isRegion ? "取区域" : "取点",
                isRegion
                    ? $"记下 {picked.Width}x{picked.Height} @ {picked.X},{picked.Y}，窗口 {picked.Process ?? picked.Class ?? "（无）"}"
                    : $"记下点 {picked.X},{picked.Y}（{(picked.Space == "screen" ? "屏幕" : "窗口内")}坐标），窗口 {picked.Process ?? picked.Class ?? "（无）"}",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show(isRegion ? "取区域失败" : "取点失败", ex.Message, InfoBarSeverity.Error);
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

    private CancellationTokenSource? _record;

    /// <summary>停止信号：引擎对 record 的停止请求不一定会回话（实测会永久不返回），
    /// 所以收尾不能只等它——停止后最多等几秒就自己收尾。</summary>
    private TaskCompletionSource? _recordStop;

    /// <summary>这一次录制请求的 id。停止时用它去 cancel，而不是用作业号——
    /// 作业号可能还是上一轮遗留的（recorded 事件没来得及到），cancel 就发给了已经结束的作业，
    /// 表现出来就是"点了停止没反应"（实测：停止后 5 秒才写盘）。</summary>
    private int? _recordRequest;
    private RunnerClient? _recordClient;
    private int? _recordJob;

    /// <summary>
    /// 录制：把一段屏幕操作变成流程，直接进编辑区（未命名，存哪儿由你决定）。
    ///
    /// 停止是录制进程的全局热键 Ctrl+Alt+Q——只有它在监听键鼠；界面这边的「结束录制」只是不再等它。
    /// </summary>
    private async void OnRecord(object sender, RoutedEventArgs e)
    {
        if (_record is not null)
        {
            // 真的让它停：Runner 的 cancel 会让录制进程收尾并写出文件（见 Recorder.ShouldStop）。
            // 光取消客户端的等待只是"不等了"，录制还在继续跑。
            RecordButton.IsEnabled = false;
            Show("录制", "正在结束录制…", InfoBarSeverity.Informational);
            _recordStop?.TrySetResult();
            if (_recordClient is { } running && _recordJob is int job) await KeyMouseBridge.CancelAsync(running, job);
            else _record.Cancel();
            return;
        }

        // 先把"正在初始化"摆到界面上，再做任何等待：确认框、拉起 serve 加起来可能 1 秒以上。
        // 实测：以前的顺序是点完 1.1 秒才出现初始化提示，用户完全不知道程序在干活。
        SetRecordVisual("正在初始化…", "\uE895", enabled: false);
        Show("录制", "正在初始化录制程序…", InfoBarSeverity.Informational);

        // 只说一次：热键 + （必要时）未保存提醒，合并进同一个对话框。
        // 以前这里连续弹两个 ContentDialog（先热键、再未保存确认），第二个没能显示就把 await 卡死，
        // 界面停在"正在初始化"、窗口也不隐藏（实测反馈）。
        if (!await ConfirmStartRecordingAsync(Model.IsDirty))
        {
            SetRecordVisual("录制", "\uE7C8", enabled: true);
            Show("录制", "已取消。", InfoBarSeverity.Informational);
            return;
        }

        RunnerClient? client = await EnsureClientAsync();
        if (client is null)
        {
            SetRecordVisual("录制", "\uE7C8", enabled: true);
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), "keymouse-record.json");
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }

        // 悬浮按钮是**辅助**，坏掉也绝不能挡住录制。
        // 目前它默认关闭：原生窗口在第二次录制时会硬崩（进程直接消失、崩溃日志无记录，
        // 说明是原生层问题）。定位之前不放进主流程；设 KEYMOUSE_ISLAND=1 可以打开做实验。
        string ignore = "";
        try
        {
            // **只创建一次**：原生窗口第二次创建会让进程硬崩（实测）。之后只做显示/隐藏。
            _island ??= new NativeIsland();
            if (!_island.IsCreated) _island.Start(() => DispatcherQueue.TryEnqueue(OnIslandStop));
            // 这里**不**显示：等 runner 报 recording（钩子真的装好）再显示。
            ignore = "0x" + _island.Handle.ToInt64().ToString("X");
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "准备悬浮条");
            _island = null;
        }

        LogBox.Text = "";
        Log("# 引擎 " + _tool);
        Log("# serve 已连接；请求 record -> " + path + (ignore.Length > 0 ? "（忽略悬浮按钮）" : ""));
        Show("录制", "正在录制。按 Ctrl+Alt+Q 结束（或点「结束录制」）。", InfoBarSeverity.Informational);

        _record = new CancellationTokenSource();
        _recordStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _recordRequest = null;
        _recordClient = client;
        _recordJob = null;

        // 先如实说"正在初始化"：录制进程要起进程、装全局钩子，这中间有 1-2 秒。
        // 以前这段直接显示"结束录制"，等于在还没开始时就宣称正在录（实测反馈）。
        SetRecordVisual("正在初始化…", "\uE895", enabled: false);
        Show("录制", "正在初始化录制程序…", InfoBarSeverity.Informational);
        DryRunButton.IsEnabled = false;
        PlayButton.IsEnabled = false;

        try
        {
            Task work = KeyMouseBridge.RecordAsync(client, path, ev => DispatcherQueue.TryEnqueue(() => OnRecordEvent(ev)), _record.Token, ignore);
            if (!work.IsCompleted && _recordStop is { } stopSignal)
            {
                await Task.WhenAny(work, stopSignal.Task);
                if (!work.IsCompleted)
                {
                    // 已经点了停止，等引擎 4 秒；它不吭声就先收尾（它自己会退出并写盘）。
                    await Task.WhenAny(work, Task.Delay(4000));
                }
            }
            if (work.IsCompleted) await work;
            else Log("# 引擎未在 4 秒内结束录制请求，已先收尾（它稍后会自己退出）。");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Show("录制出错", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _record.Dispose();
            _record = null;
            _recordClient = null;
            _recordJob = null;
            SetRecordVisual("录制", "\uE7C8", enabled: true);
            // 任何结束路径都保证窗口回到前台：录制期间它是隐藏的，绝不能留下看不见的进程。
            RestoreAfterRecording();
            // 不销毁：留着给下一次录制复用（销毁后再次创建会崩）。只隐藏。
            try { _island?.SetVisible(false); } catch (Exception) { }
            RecordButton.IsEnabled = true;
            RecordLabel.Text = "录制";
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(RecordButton, "录制");
            RecordIcon.Glyph = "\uE7C8";
            UpdateStates();
            BringToFront();
        }

        if (!File.Exists(path))
        {
            Show("录制", "没有产生流程文件（可能一步都没录到）。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            Model.Load(path, keepPath: false);
        }
        catch (Exception ex)
        {
            Show("录到的流程读不了", ex.Message, InfoBarSeverity.Error);
            return;
        }

        _hasDocument = true;
        SelectedStep = null;
        UpdateStates();
        Show("录制完成", $"录到 {Model.Steps.Count} 步：{path}。另存为你的流程文件即可。", InfoBarSeverity.Success);
    }

    /// <summary>录制进程的输出全部进「运行输出」，与运行时的日志同一个去处。</summary>
    /// <summary>
    /// 录制按钮的四种样子：录制 → 正在初始化 → 停止录制 → 正在停止。
    /// 每一步都由事实驱动（Runner 报 recording/stopping、或运行结束），不靠计时猜。
    /// </summary>
    private bool _hiddenForRecording;
    private NativeIsland? _island;

    /// <summary>开始录制前的说明：热键、窗口会隐藏、还有悬浮条可以点。返回 false = 用户取消。</summary>
    private async Task<bool> ConfirmStartRecordingAsync(bool dirty)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "开始录制",
            Content = "停止录制：按 Ctrl+Alt+Q，或点屏幕右侧中间的红色圆点。\n\n" +
                      "开始后编辑器窗口会隐藏，录制结束自动回来。" +
                      (dirty ? "\n\n注意：当前流程有未保存的改动，开始录制会丢弃它们。" : ""),
            PrimaryButtonText = "继续录制",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private async void OnIslandStop()
    {
        // **先恢复界面，再去取消**：引擎对停止请求不一定及时回话（实测会卡住不返回），
        // 而用户对"点了停止"的期待是立刻回到编辑器。录制文件由录制器自己收尾写盘。
        try { _island?.SetVisible(false); } catch (Exception) { }
        RestoreAfterRecording();

        try
        {
            _recordStop?.TrySetResult();
            if (_recordClient is { } running && RecordTarget() is int job)
            {
                SetRecordVisual("正在停止…", "\uE895", enabled: false);
                await KeyMouseBridge.CancelAsync(running, job);
            }
            else
            {
                _record?.Cancel();
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "悬浮按钮停止");
        }

        // 兜底：这里一旦卡住，窗口就再也回不来——用户看到的是"编辑器自己退出了"
        // （进程还在，只是永远隐藏，实测第 3 轮必现）。所以超时就强制收尾。
        _ = RestoreIfStuckAsync();
    }

    /// <summary>取消录制时用哪个编号：优先这一次请求的 id，退回作业号。</summary>
    private int? RecordTarget() => _recordRequest ?? _recordJob;

    /// <summary>停止流程卡住时的兜底：把窗口、按钮、悬浮球都恢复，绝不留一个看不见的进程。</summary>
    private async Task RestoreIfStuckAsync()
    {
        await Task.Delay(3000);
        if (!_hiddenForRecording) return;

        try { _island?.SetVisible(false); } catch (Exception) { }
        _record?.Cancel();
        RestoreAfterRecording();
        SetRecordVisual("录制", "\uE7C8", enabled: true);
        Show("录制", "已停止（引擎没有及时回话，已强制收尾）。", InfoBarSeverity.Warning);
        Log("# 停止超时：已强制恢复窗口。");
    }

    private void HideForRecording()
    {
        if (_hiddenForRecording) return;
        _hiddenForRecording = true;
        try { AppWindow.Hide(); } catch (Exception ex) { CrashLog.Write(ex, "隐藏窗口"); }
    }

    /// <summary>录制结束后把窗口拿回前台。任何结束路径都要走这里，绝不留下一个看不见的进程。</summary>
    private void RestoreAfterRecording()
    {
        if (!_hiddenForRecording) return;
        _hiddenForRecording = false;
        try
        {
            AppWindow.Show();
            Activate();
            SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "恢复窗口");
        }
    }

    private void SetRecordVisual(string label, string glyph, bool enabled)
    {
        RecordLabel.Text = label;
        RecordIcon.Glyph = glyph;
        RecordButton.IsEnabled = enabled;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(RecordButton, label);
    }

    private void OnRecordEvent(RunnerEvent ev)
    {
        if (ev.Kind == "state")
        {
            if (ev.State == "recording")
            {
                SetRecordVisual("停止录制", "\uE71A", enabled: true);

                // 只有悬浮按钮真的存在时才隐藏主窗口：否则用户连"停止录制"都点不到。
                // 悬浮按钮在时藏窗口，是为了不让那一下点击被录进流程（它会点回编辑器）。
                if (_island is not null)
                {
                    // 录制真的开始了：这时才亮出悬浮球并隐藏主窗口。
                    _island.SetVisible(true);
                    Show("录制中", "按 Ctrl+Alt+Q 结束，或点屏幕右侧的红点。停止后窗口自动回来。", InfoBarSeverity.Success);
                    HideForRecording();
                }
                else
                {
                    Show("录制中", "按 Ctrl+Alt+Q 结束，或点「停止录制」。", InfoBarSeverity.Success);
                }
                try
                {
                    // 原生窗口在 Start() 里已经显示并置顶，这里无事可做。
                }
                catch (Exception ex)
                {
                    CrashLog.Write(ex, "显示悬浮条");
                }
            }
            else if (ev.State == "stopping")
            {
                // 停止键一按下就把窗口拿回前台，并立刻切成"正在停止"。
                RestoreAfterRecording();
                SetRecordVisual("正在停止…", "\uE895", enabled: false);
                Show("录制", "正在停止录制并写出流程…", InfoBarSeverity.Informational);
            }
            return;
        }

        if (ev.Job is int job) _recordJob = job;   // 「结束录制」要用它发 cancel
        if (ev.Id > 0) _recordRequest = ev.Id;     // 更稳的取消目标（见 _recordRequest 的说明）

        if (ev.Kind == "log" && ev.Line is { Length: > 0 } line) Log(line);
        else if (ev.Kind == "finished") Log($"录制结束，退出码 {ev.ExitCode}");
        else if (ev.Kind == "error") Log("# 错误  " + (ev.Message ?? "录制失败"));
    }

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
        // 这里是 async void 处理器 await 的入口：任何漏出来的异常都会直接结束进程。
        // 所以整段兜住，把原因写进面板与日志——失败可以，蒸发不行。
        try
        {
            await RunCoreAsync(dryRun);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            Show("运行出错", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task RunCoreAsync(bool dryRun)
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

        // 执行序号 ≠ 行号：循环会展开、子流程会内联。先在本地算一份计划，事件来了才认得人。
        try
        {
            _plan = FlowPlan.Build(Model.BuildDocument(), Model.DocumentPath ?? "");
        }
        catch (Exception)
        {
            _plan = null;   // 计划建不起来不影响跑：Runner 自己会报格式错误
        }

        LogBox.Text = "";
        _runSteps = 0;
        _runFailures = 0;
        _runTotalMs = 0;
        _runClock.Restart();
        foreach (StepVm row in Model.Rows) row.ResetRun();
        // 输出面板自己会出现（见 Log），不需要展开任何东西
        Log($"# KeyMouse serve 连接已建立；请求 run {path}{(dryRun ? "（--dry-run）" : "")}");
        // 引擎路径写在运行输出里：版本不对时，第一眼就能看到它在用哪个 exe（实测踩过"注册表指向旧副本"）。
        Log($"# 引擎 {_tool ?? "（没找到 KeyMouse.exe）"}");
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
            foreach (StepVm row in Model.Rows) row.ClearRunning();
            _runningRow = null;
            _scrollTo = null;
            _run.Dispose();
            _run = null;
            _job = null;
            _plan = null;
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
        _runClock.Stop();
        // 摘要按**执行步数**统计，不按可见行数：折叠把 748 次移动并成一行，
        // 按行数算会得到"6 步全部成功"这种与事实不符的结论（实测反馈）。
        StepVm? failed = Model.Rows.FirstOrDefault(s => s.RunDetail.StartsWith("失败"));
        int ok = _runSteps - _runFailures;
        long totalMs = _runTotalMs;

        bool success = exitCode == 0;
        RunSummaryIcon.Glyph = success ? "\uE73E" : "\uEA39";
        // failed 可能是 null：格式错误、窗口不可用、Runner 拒跑都会让整次运行以非 0 结束，
        // 却没有"某一步失败"的事件。以前这里写 failed!.Number，等于用空引用把程序带走（实测崩过）。
        // 两个耗时都要说，且要说清是哪一种：墙钟含步骤之间的等待（录制时间线），
        // 各步合计不含——只写一个数会让人以为"录了 9 秒，为什么只跑 4.5 秒"（实测反馈）。
        string timing = $"用时 {_runClock.Elapsed.TotalSeconds:0.#} 秒（各步执行合计 {totalMs} ms）";
        RunSummaryText.Text = success
            ? $"{ok} 步全部成功，{timing}。{Explain(0)}"
            : failed is null
                ? $"这次没跑起来（退出码 {exitCode}）。{Explain(exitCode)}"
                : $"第 {failed.Number} 步「{StepCatalog.NameOf(failed.Step.Type)}」失败。"
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
        StepVm? failed = Model.Rows.FirstOrDefault(s => s.RunDetail.StartsWith("失败"));
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

            // 引擎一次只跑一个作业：前面那个还没收尾时，这条会把"为什么还没动静"说清楚。
            case "queued":
                Log("# " + (e.Message ?? "排队中"));
                Show("排队中", e.Message ?? "等上一个作业收尾就开跑。", InfoBarSeverity.Informational);
                break;

            // 运行已经结束（_run 为 null）之后还可能收到排队中的事件：
            // 这时再标"运行中"就永远清不掉了（终态清理已经跑过）。
            case "step" when _run is null:
                break;

            case "step" when e.Index is int index:
                // 状态显示在**那一行上**，不为每一步弹一次提示：跑二十步弹二十次，那不叫反馈，叫噪音。
                StepVm? row = RowForExecution(index);
                if (e.State == "started")
                {
                    // 只清上一行，不遍历全表：631 步 × 631 行的循环就是把 UI 卡死的那一半原因。
                    if (_runningRow is { } previous && !ReferenceEquals(previous, row)) previous.ClearRunning();
                    _runningRow = row;

                    if (row is not null)
                    {
                        row.MarkRunning();
                        _scrollTo = row;   // 滚动也按帧合并，别每步都滚一次
                    }

                    Log($"  [{index}/{e.Total ?? _plan?.StepCount ?? Model.Rows.Count}] 开始 {StepCatalog.NameOf(e.Type ?? "")}");
                }
                else
                {
                    // 结果只在"完成"事件上记一次。以前这段写在状态判断外面，
                    // started 与 finished 各调一次 MarkAggregate，而 MarkRunning 每次都清零，
                    // 于是折叠行永远显示"2 步"（截图里 13 次 → 2 步）。
                    _runSteps++;
                    _runTotalMs += e.DurationMs ?? 0;
                    if ((e.ExitCode ?? 0) != 0) _runFailures++;

                    if (row is not null)
                    {
                        // 折叠行累加子步骤，普通行直接写自己的结果
                        if (row.ChildTotal > 1) row.MarkAggregate(e.ExitCode ?? 0, e.DurationMs ?? 0);
                        else row.MarkResult(e.ExitCode ?? 0, e.DurationMs ?? 0);
                        if (e.Shot is { Length: > 0 } shot && File.Exists(shot)) { row.SetShot(shot); NotifyShot(); }
                        Log($"  [{index}] {(e.ExitCode == 0 ? "完成" : $"失败（退出码 {e.ExitCode}）")}，{e.DurationMs} ms");
                    }
                    else
                    {
                        // 内联进来的子流程步骤不属于这个文件：只在日志里如实报，不往任何一行上贴结果。
                        Log($"  [{index}] {(e.ExitCode == 0 ? "完成" : $"失败（退出码 {e.ExitCode}）")}，{e.DurationMs} ms（子流程内）");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 执行项序号（1 起）→ 界面行。循环展开后会多次指向同一行，那时**同一行反复刷新**是对的：
    /// 它确实又跑了一遍。
    /// </summary>
    private StepVm? RowForExecution(int index)
    {
        if (_plan is null) return index - 1 >= 0 && index - 1 < Model.Rows.Count ? Model.Rows[index - 1] : null;
        int i = index - 1;
        if (i < 0 || i >= _plan.Items.Count) return null;

        StepVm? vm = Model.VmForNode(_plan.Items[i].Step);
        if (vm is null) return null;
        if (Model.Rows.Contains(vm)) return vm;

        // 折叠时子步骤不在 Rows 里。以前这种情况被当成"内联子流程"、结果只写日志
        // （截图里 `[7] 完成，0 ms（子流程内）` 就是这么来的）。它其实属于这个文件，
        // 结果该落到它所属的那一折叠行上。
        for (StepVm? parent = vm.Group; parent is not null; parent = parent.Group)
        {
            if (Model.Rows.Contains(parent)) return parent;
        }
        return null;
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
