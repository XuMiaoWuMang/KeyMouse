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

/// <summary>录制：开始/结束、隐藏与还原窗口、悬浮停止键、把录制事件翻译成界面状态。</summary>
public sealed partial class MainWindow
{
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

        string path = Scratch.PathFor("keymouse-record.json");
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }

        // 悬浮按钮是**辅助**，坏掉也绝不能挡住录制。
        // 它只在 runner 报 recording 之后才显示（见下面的 SetVisible(true)），录制没真的开始就不出现；
        // 原生窗口在第二次创建时会硬崩（进程直接消失、崩溃日志无记录，说明是原生层问题），
        // 所以这里整个进程只创建一个，之后只做显示/隐藏。
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
        // 从按下去的那一刻就算在录制：之后任何一次 UpdateStates 都不会把这两条放出来。
        _recording = true;
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
            _recording = false;
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

}
