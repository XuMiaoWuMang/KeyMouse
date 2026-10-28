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
        Title = $"KeyMouse 流程编辑器 {Commands.ProductVersion}（构建 {built} · {Elevation()}）";
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
            // 「先构建 dist\KeyMouse.exe」这句话曾经挂在界面上很多年，而 dist\ 早就不存在了：
            // 提示必须说出**当前形态**该去哪儿找，否则它只会把人引到更远的地方。
            string why = _tool is not null
                ? "启动 serve 之后没能连上命名管道。"
                : ProductLayout.Environment == ProductEnvironment.Development
                    ? "找不到 KeyMouse.exe。开发树里它由 tools\\build\\build.ps1（或 km d）产出到 artifacts\\cli\\。"
                    : "找不到 KeyMouse.exe。安装版里它应当在 <安装目录>\\cli\\ 下，重装一次可以修好。";
            Show("连不上 Runner",
                why + " 各候选路径与选中结果：命令行跑一次 KeyMouse doctor。" +
                (RunnerClient.LastStartFailure is { Length: > 0 } detail ? " 它说：" + detail : ""),
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
    private bool _recording;      // 录制期间试运行/播放一律不可用，见 UpdateStates
    private string[]? _paletteTypes;
    private int _paletteShown = -1;
    private Surfaces? _surfaces;

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateStates();

    private void UpdateStates()
    {
        bool any = Model.Rows.Count > 0;
        bool selected = SelectedStep is not null;
        PickButton.IsEnabled = SelectedStep is not null;
        // 录制期间这两条都关上：录制自己占着鼠标和全局钩子，同时跑流程只会互相打岔。
        DryRunButton.IsEnabled = any && _run is null && !_recording;
        PlayButton.IsEnabled = any && _run is null && !_recording;
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
