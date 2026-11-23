namespace KeyMouse.Runtime;

/// <summary>Input policy and counters belong to one run, including work it dispatches to another task.</summary>
internal sealed class ExecutionMode : IDisposable
{
    private static readonly AsyncLocal<ExecutionMode?> Current = new();
    private readonly bool _dryRun;
    private long _injected, _suppressed;

    private ExecutionMode(bool dryRun) => _dryRun = dryRun;

    internal static ExecutionMode Begin(bool dryRun)
    {
        if (IsRunning) throw new InvalidOperationException("Nested runs are not supported.");
        return Current.Value = new ExecutionMode(dryRun);
    }

    internal static bool IsRunning => Current.Value is not null;
    internal static bool DryRun => Current.Value?._dryRun ?? false;
    internal static long InjectedEvents => Current.Value is { } run ? Interlocked.Read(ref run._injected) : 0;
    internal static long SuppressedEvents => Current.Value is { } run ? Interlocked.Read(ref run._suppressed) : 0;
    internal static void NoteInjected(int count) { if (Current.Value is { } run) Interlocked.Add(ref run._injected, count); }
    internal static void NoteSuppressed(int count) { if (Current.Value is { } run) Interlocked.Add(ref run._suppressed, count); }

    public void Dispose() => Current.Value = null;
}
