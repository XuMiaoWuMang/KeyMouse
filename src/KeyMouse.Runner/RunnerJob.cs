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

    public void StepFinished(int index, int exitCode, long durationMs) =>
        Emit(RunnerEvent.Step(RequestId, Job, index, Total, "finished", "", exitCode, durationMs));

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
        State = State,
        Detail = Detail,
        StartedAt = StartedAt,
        FinishedAt = FinishedAt,
        ExitCode = ExitCode,
    };
}
