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

/// <summary>试运行与播放：提交作业、按帧刷新输出、把 Runner 的执行序映射回界面行、中止与暂停。</summary>
public sealed partial class MainWindow
{
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

}
