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
}

internal static class Execution
{
    /// <summary>Set by the Runner for the duration of one job; null when the CLI drives directly.</summary>
    internal static IExecutionControl? Control { get; set; }

    internal static void BetweenSteps(int index) => Control?.BetweenSteps(index);

    internal static void StepStarted(int index, string type) => Control?.StepStarted(index, type);

    internal static void StepFinished(int index, int exitCode, long durationMs) =>
        Control?.StepFinished(index, exitCode, durationMs);

    /// <summary>Stop conditions (a recording session, a wait loop) ask this instead of a Runner type.</summary>
    internal static bool Cancelled => Control?.Cancelled ?? false;
}
