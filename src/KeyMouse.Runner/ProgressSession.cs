namespace KeyMouse.Runner;

/// <summary>Replayable v2 progress. Only the owner of the opaque session id can follow a run.
/// Bounded metadata only: never retain logs, image bytes or callbacks to disconnected clients.</summary>
internal sealed class ProgressSession
{
    internal const int Capacity = 256;
    internal string Token { get; } = Guid.NewGuid().ToString("N");
    private readonly Queue<RunnerEvent> _events = new();
    private TaskCompletionSource _changed = NewSignal();
    private long _cursor;
    private bool _done;
    internal bool Done { get { lock (_events) return _done; } }
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Publish(RunnerEvent item)
    {
        if (item.Kind is not ("step" or "state" or "queued" or "finished")) return;
        lock (_events)
        {
            if (_done) return;
            if (_events.Count == Capacity) _events.Dequeue();
            _events.Enqueue(item);
            item.Cursor = ++_cursor;
            _done = item.Kind == "finished";
            _changed.TrySetResult();
            _changed = NewSignal();
        }
    }

    private int _followers;
    internal async Task Follow(int id, long after, Action<RunnerEvent> emit, CancellationToken stop)
    {
        try
        {
            if (Interlocked.Increment(ref _followers) > 4) { emit(RunnerEvent.Error(id, 3, "会话订阅过多")); return; }
            await FollowCore(id, after, emit, stop);
        }
        finally { Interlocked.Decrement(ref _followers); }
    }

    private async Task FollowCore(int id, long after, Action<RunnerEvent> emit, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            Task changed;
            RunnerEvent[]? batch;
            bool done;
            lock (_events)
            {
                batch = after < _cursor - _events.Count || after > _cursor ? null :
                    _events.Skip((int)(after - (_cursor - _events.Count))).ToArray();
                changed = _changed.Task;
                done = _done;
            }
            if (batch == null) { emit(RunnerEvent.Error(id, 3, "进度游标过期或无效；请重新读取 status")); return; }
            if (batch.Length > 0)
            {
                after = batch[^1].Cursor!.Value;
                emit(new RunnerEvent
                {
                    Kind = "status-stream",
                    Id = id,
                    Cursor = after,
                    Value = RunnerHost.Serialize(new { steps = batch })
                });
            }
            if (done) { emit(RunnerEvent.Result(id, RunnerHost.Serialize(new { cursor = after }))); return; }
            try { await changed.WaitAsync(stop); } catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
        }
    }
}
