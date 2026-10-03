using System.Collections.Specialized;
using System.ComponentModel;
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

        BuildAddMenu();
        StepList.KeyboardAccelerators[0].Invoked += (_, e) =>
        {
            DeleteSelected();
            e.Handled = true;
        };
        AppWindow.Closing += OnClosing;

        UpdateStates();
        Show("ready", _tool is null
            ? "没找到 KeyMouse.exe：把它放在编辑器旁边，或者先构建 dist\\KeyMouse.exe。「取区域/试运行/播放」需要它来启动常驻 Runner。"
            : $"{_tool} — 取区域与回放都通过常驻 Runner（命名管道），不再是一个动作一个子进程",
            InfoBarSeverity.Informational);
    }

    /// <summary>
    /// Connects to the resident Runner, starting one if it is not running yet. One connection serves
    /// the whole session: picking, replaying, cancelling are all requests on it.
    /// </summary>
    private async Task<RunnerClient?> EnsureClientAsync()
    {
        if (_client is not null) return _client;
        Show("连接 Runner", "正在连接常驻 Runner（没有就启动一个）…", InfoBarSeverity.Informational);
        _client = await KeyMouseBridge.ConnectAsync(_tool);
        if (_client is null)
        {
            Show("连不上 Runner", _tool is null
                ? "找不到 KeyMouse.exe。先构建 dist\\KeyMouse.exe，或把编辑器与它放在一起。"
                : "启动了 KeyMouse serve 但连不上命名管道；在命令行跑一次 KeyMouse serve 看看它报什么。",
                InfoBarSeverity.Error);
            return null;
        }
        Show("Runner 已连接", "取区域、试运行、播放都通过这条连接。", InfoBarSeverity.Success);
        UpdateStates();
        return _client;
    }

    public EditorModel Model { get; } = new();

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

    private void BuildAddMenu()
    {
        foreach (var kind in StepCatalog.All)
        {
            var item = new MenuFlyoutItem { Text = kind.Name, Icon = new FontIcon { Glyph = kind.Glyph } };
            ToolTipService.SetToolTip(item, kind.Hint);
            string type = kind.Type;
            item.Click += (_, _) =>
            {
                int index = SelectedStep is null ? Model.Steps.Count : Model.Steps.IndexOf(SelectedStep) + 1;
                Model.Add(type, index);
                StepList.SelectedIndex = index;
            };
            AddMenu.Items.Add(item);
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateStates();

    private void UpdateStates()
    {
        bool any = Model.Steps.Count > 0;
        bool selected = SelectedStep is not null;
        UpButton.IsEnabled = selected;
        DownButton.IsEnabled = selected;
        DuplicateButton.IsEnabled = selected;
        DeleteButton.IsEnabled = selected;
        PickButton.IsEnabled = SelectedStep is not null;
        DryRunButton.IsEnabled = any && _run is null;
        PlayButton.IsEnabled = any && _run is null;
        EmptyState.Visibility = Ui.Show(!any);

        PropertyChanged?.Invoke(this, Everything);
    }

    private void OnStepSelected(object sender, SelectionChangedEventArgs e)
    {
        SelectedStep = StepList.SelectedItem as StepVm;
        UpdateStates();
    }

    private void Show(string title, string message, InfoBarSeverity severity)
    {
        Status.Title = title;
        Status.Message = message;
        Status.Severity = severity;
        Status.IsOpen = true;
    }

    private void Log(string line)
    {
        LogBox.Text += line + Environment.NewLine;
        LogBox.SelectionStart = LogBox.Text.Length;
    }

    // ------------------------------------------------------------------ files

    public void OpenFile(string path)
    {
        try
        {
            Model.Load(path);
            StepList.SelectedIndex = Model.Steps.Count > 0 ? 0 : -1;
            LogBox.Text = "";
            Log($"打开 {path}（{Model.Steps.Count} 步）");
            Show("已打开", $"{path}（{Model.Steps.Count} 步）", InfoBarSeverity.Success);
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

    private void OnSave(object sender, RoutedEventArgs e)
    {
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

            if (step.Step.Type == "wait-text")
            {
                step.RegionX = picked.X;
                step.RegionY = picked.Y;
                step.RegionWidth = picked.Width;
                step.RegionHeight = picked.Height;
            }
            else
            {
                step.SpaceIndex = picked.Space == "screen" ? 1 : 0;
                step.X = picked.X;
                step.Y = picked.Y;
            }
            if (picked.Process is { Length: > 0 }) step.TargetProcess = picked.Process;
            if (picked.Class is { Length: > 0 }) step.TargetClass = picked.Class;
            if (picked.Title is { Length: > 0 }) step.TargetTitle = picked.Title;

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
        if (_client is not null && _job is int job) await KeyMouseBridge.CancelAsync(_client, job);
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
        LogExpander.IsExpanded = true;
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
            exitCode = await KeyMouseBridge.RunFlowAsync(client, path, dryRun, OnRunnerEvent, _run.Token);
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

        Show(exitCode == 0 ? "成功" : $"失败（退出码 {exitCode}）", Explain(exitCode),
            exitCode == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Error);
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
                if (e.State == "started")
                {
                    if (index - 1 >= 0 && index - 1 < Model.Steps.Count) StepList.SelectedIndex = index - 1;
                    Show("正在执行", $"[{index}/{e.Total ?? Model.Steps.Count}] " +
                                     $"{StepCatalog.NameOf(e.Type ?? "")}（这一行已被选中）", InfoBarSeverity.Informational);
                }
                else
                {
                    Log($"  [{index}] 完成，退出码 {e.ExitCode}，{e.DurationMs} ms");
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
        3 => "退出码 3：没等到、没匹配上，或者选区被取消——没有任何输入发出去，可以直接重试。",
        4 => "退出码 4：目标窗口现在不可用（最小化、被遮盖或拒绝绘制）——没有发输入。",
        5 => "退出码 5：焦点没验证通过，不敢往下按——没有发输入。",
        6 => "退出码 6：没读到内容（区域不对、引擎看不懂）——没有发输入。",
        7 => "退出码 7：被你（或客户端）取消了——取消点之后没有发出任何输入。",
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
