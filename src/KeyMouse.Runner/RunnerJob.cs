namespace KeyMouse.Runner;

using KeyMouse; // CommandFailure: the capability layer's "abort with this exit code"

/// <summary>
/// One execution inside the resident Runner. It is both the bookkeeping (state, exit code, timing)
/// and the <see cref="IExecutionControl"/> Core sees while the job runs: that is how a client can
/// pause, resume or cancel work that is sitting inside the capability layer, and how each step
/// reaches the UI as it starts and finishes.
/// </summary>
internal sealed class RunnerJob : IExecutionControl
{
    private readonly ManualResetEventSlim _resume = new(true);
    private volatile bool _cancelled;

    internal RunnerJob(int job, int requestId, string method, Action<RunnerEvent> emit)
    {
        Job = job;
        RequestId = requestId;
        Method = method;
        Emit = emit;
        StartedAt = Environment.TickCount64;
    }

    internal int Job { get; }
    internal int RequestId { get; }
    internal string Method { get; }

    /// <summary>提出这个作业的客户端（请求里的 client），只作展示与排查用。</summary>
    internal string? Client { get; init; }

    /// <summary>这条流程的文件路径（截图证据要落在它旁边）。</summary>
    internal string? FlowPath { get; set; }

    /// <summary>证据目录以哪个文件为准（用户那份流程）。</summary>
    internal string? EvidenceFor { get; set; }

    /// <summary>这次运行要不要逐步留截图。</summary>
    internal bool Shots { get; set; }

    /// <summary>恢复最小化窗口是否被允许（与执行器用的是同一个判断）。</summary>
    internal bool AllowRestore { get; set; }

    /// <summary>按执行序号排列的步骤，用来给每一步找到它的目标区域。</summary>
    internal IReadOnlyList<FlowStep> Steps { get; set; } = [];

    /// <summary>queued | running | paused | finished | failed | cancelled.</summary>
    internal string State { get; set; } = "queued";

    internal string? Detail { get; set; }
    internal long StartedAt { get; }
    internal long? FinishedAt { get; set; }
    internal int? ExitCode { get; set; }

    /// <summary>How many steps the flow has, so a UI can draw progress without knowing the format.</summary>
    internal int Total { get; set; }

    internal Action<RunnerEvent> Emit { get; }

    // ------------------------------------------------------------------ IExecutionControl

    public bool Cancelled => _cancelled;

    public void BetweenSteps(int index)
    {
        // Thrown as a CommandFailure on purpose: that is how the capability layer carries "stop now,
        // with this exit code" through its own top-level handler, so a cancel cannot be mistaken for
        // a generic failure (measured: an OperationCanceledException came back as exit code 1).
        if (_cancelled) throw new CommandFailure(CancelledExitCode, CancelledMessage);
        _resume.Wait();
        if (_cancelled) throw new CommandFailure(CancelledExitCode, CancelledMessage);
    }

    internal const int CancelledExitCode = 7;
    internal const string CancelledMessage = "已被客户端取消：取消点之后没有再发出任何输入";

    public void StepStarted(int index, string type) =>
        Emit(RunnerEvent.Step(RequestId, Job, index, Total, "started", type));

    /// <summary>
    /// 一步跑完。顺手留一张证据：**先执行、后截图**，所以行上看到的是这一步做完之后的屏幕，
    /// 而不是它动手之前的。
    /// </summary>
    public void StepFinished(int index, int exitCode, long durationMs) =>
        Emit(RunnerEvent.Step(RequestId, Job, index, Total, "finished", "", exitCode, durationMs, ShotFor(index)));

    private string? ShotFor(int index)
    {
        if (!Shots || FlowPath is null) return null;
        if (index - 1 < 0 || index - 1 >= Steps.Count) return null;
        return RunShots.Capture(Steps[index - 1], FlowPath, EvidenceFor, index, AllowRestore);
    }

    // ------------------------------------------------------------------ control

    internal void Pause()
    {
        if (_cancelled || State != "running") return;
        State = "paused";
        _resume.Reset();
    }

    internal void Resume()
    {
        if (_cancelled || State != "paused") return;
        State = "running";
        _resume.Set();
    }

    internal void Cancel()
    {
        _cancelled = true;
        _resume.Set(); // a paused job has to wake up to notice
        State = "cancelled";
    }

    internal RunnerJobInfo Info() => new()
    {
        Job = Job,
        Id = RequestId,
        Method = Method,
        Client = Client,
        State = State,
        Detail = Detail,
        StartedAt = StartedAt,
        FinishedAt = FinishedAt,
        ExitCode = ExitCode,
    };
}
