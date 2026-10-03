using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace KeyMouse.Runner;

/// <summary>
/// The client side of the pipe, used by the editor (and by tests). One connection, a background pump
/// that routes every event to the request that asked for it, and one awaitable per request: a `run`
/// completes when its `finished` event arrives, a `pick-region` when its `result` does.
/// </summary>
internal sealed class RunnerClient : IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<int, Action<RunnerEvent>> _listeners = [];
    private readonly Dictionary<int, TaskCompletionSource<RunnerEvent>> _waiting = [];
    private readonly object _gate = new();
    private readonly Task _pump;
    private int _nextId;

    private RunnerClient(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
        _reader = new StreamReader(pipe, new UTF8Encoding(false));
        _writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
        _pump = Task.Run(PumpAsync);
    }

    /// <summary>Connects to a running Runner, or returns null when there is none.</summary>
    internal static async Task<RunnerClient?> ConnectAsync(int timeoutMs = 400, string? pipeName = null)
    {
        var pipe = new NamedPipeClientStream(".", pipeName ?? RunnerProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var timeout = new CancellationTokenSource(timeoutMs);
            await pipe.ConnectAsync(timeout.Token);
            return new RunnerClient(pipe);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or TimeoutException)
        {
            pipe.Dispose();
            return null;
        }
    }

    internal int NextId() => Interlocked.Increment(ref _nextId);

    /// <summary>
    /// Sends one request and waits for its terminal event (`finished`, `result` or `error`).
    /// <paramref name="onEvent"/> sees every event of that request as it arrives, which is how the
    /// editor highlights the step being executed and streams the log.
    /// </summary>
    internal async Task<RunnerEvent> SendAsync(
        string method, RunnerParameters? parameters = null, Action<RunnerEvent>? onEvent = null,
        CancellationToken cancellation = default)
    {
        int id = NextId();
        var completion = new TaskCompletionSource<RunnerEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (onEvent is not null) _listeners[id] = onEvent;
            _waiting[id] = completion;
        }

        string line = JsonSerializer.Serialize(new RunnerRequest { Id = id, Method = method, Params = parameters }, RunnerProtocol.Json);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellation);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            Complete(id, RunnerEvent.Error(id, 1, $"Runner 连接断了：{ex.Message}"));
        }

        using var registration = cancellation.Register(() => Complete(id, RunnerEvent.Error(id, 7, "客户端取消了等待")));
        return await completion.Task;
    }

    private async Task PumpAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                string? line = await _reader.ReadLineAsync(_stop.Token);
                if (line is null) break;
                if (line.Trim().Length == 0) continue;

                RunnerEvent? e;
                try
                {
                    e = JsonSerializer.Deserialize<RunnerEvent>(line, RunnerProtocol.Json);
                }
                catch (JsonException)
                {
                    continue; // a line we cannot read is not worth killing the connection over
                }
                if (e is null) continue;

                lock (_gate)
                {
                    if (_listeners.TryGetValue(e.Id, out var listener))
                    {
                        try { listener(e); }
                        catch (Exception) { /* a UI callback must not kill the pump */ }
                    }
                }

                if (e.Kind is "finished" or "result" or "error") Complete(e.Id, e);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // the runner stopped or the pipe broke; pending requests are failed below
        }
        finally
        {
            lock (_gate)
            {
                foreach (int id in _waiting.Keys.ToArray())
                {
                    Complete(id, RunnerEvent.Error(id, 1, "Runner 已停止"));
                }
            }
        }
    }

    private void Complete(int id, RunnerEvent e)
    {
        TaskCompletionSource<RunnerEvent>? completion;
        lock (_gate)
        {
            if (!_waiting.Remove(id, out completion)) return;
            _listeners.Remove(id);
        }
        completion.TrySetResult(e);
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _pipe.Dispose(); } catch (ObjectDisposedException) { /* already gone */ }
        _stop.Dispose();
    }

    /// <summary>Starts a Runner if none is listening, and returns a connected client.</summary>
    internal static async Task<RunnerClient?> ConnectOrStartAsync(string toolPath, int waitMs = 4000)
    {
        if (await ConnectAsync() is { } existing) return existing;

        var startInfo = new System.Diagnostics.ProcessStartInfo(toolPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("serve");
        try
        {
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception)
        {
            return null;
        }

        var deadline = Environment.TickCount64 + waitMs;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(150);
            if (await ConnectAsync(300) is { } client) return client;
        }
        return null;
    }
}
