namespace KeyMouse;

/// <summary>
/// The seam the resident Runner plugs into.
///
/// Core never references the Runner: it only knows that *something* may be watching an execution and
/// may want to pause it, cancel it, or hear about each step. With no control installed (the CLI path,
/// which is a plain blocking call) every call here is a no-op, so the same code path serves both
/// entry points - which is the whole point of the layering.
/// </summary>
internal interface IExecutionControl
{
    /// <summary>True once the client asked for the execution to stop.</summary>
    bool Cancelled { get; }

    /// <summary>Blocks while the client has it paused; throws <see cref="OperationCanceledException"/> on cancel.</summary>
    void BetweenSteps(int index);

    void StepStarted(int index, string type);

    void StepFinished(int index, int exitCode, long durationMs);

    /// <summary>
    /// 录制会话的状态变化：`recording`（钩子已装好、真的开始录了）、`stopping`（收到停止键，正在收尾）。
    /// 界面靠它把"正在初始化"换成"停止录制"，而不是点下去就先假装开始了。
    /// </summary>
    void RecordingState(string state);
}

internal static class Execution
{
    /// <summary>Set by the Runner for the duration of one job; null when the CLI drives directly.</summary>
    internal static IExecutionControl? Control { get; set; }

    internal static void BetweenSteps(int index) => Control?.BetweenSteps(index);

    internal static void StepStarted(int index, string type) => Control?.StepStarted(index, type);

    internal static void StepFinished(int index, int exitCode, long durationMs) =>
        Control?.StepFinished(index, exitCode, durationMs);

    internal static void RecordingState(string state) => Control?.RecordingState(state);

    /// <summary>Stop conditions (a recording session, a wait loop) ask this instead of a Runner type.</summary>
    internal static bool Cancelled => Control?.Cancelled ?? false;
}
