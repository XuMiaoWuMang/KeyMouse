using System.IO.Pipes;
using System.Reflection;
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

    /// <summary>谁在用这个连接（编辑器进程、测试进程……）。服务端把它记在作业上，排查时知道是谁让它跑的。</summary>
    internal static string ClientName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";

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

        // 每个请求都自报家门与协议版本：契约里的 client / version 就是给这件事用的，
        // 服务端据此把作业记到某个人头上，并在版本不合时当场拒绝，而不是跑出看不懂的结果。
        parameters ??= new RunnerParameters();
        parameters.Client ??= ClientName;
        parameters.Version ??= RunnerProtocol.ContractVersion;

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

        // 起子进程时**必须**把它的输出读干：只重定向不读，缓冲区写满会把 serve 卡死
        // （它连命名管道都建不出来，客户端于是永远连不上）；完全不重定向，又会在失败时
        // 丢掉它的遗言——用户看到的就是那句没法行动的"连不上"。两个都要。
        var startInfo = new System.Diagnostics.ProcessStartInfo(toolPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            // 在 CLI 自己的目录里启动：继承编辑器的 bin 目录时，serve 可能找不到它需要的东西
            // （数据目录、语言模型等）而立刻退出——那正是"启动了 serve 但连不上"的另一种成因。
            WorkingDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(toolPath)) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("serve");

        var words = new List<string>();
        void Remember(object _, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data is not { Length: > 0 } line) return;
            lock (words) { if (words.Count < 6) words.Add(line.Trim()); }
        }

        System.Diagnostics.Process? child;
        try
        {
            child = System.Diagnostics.Process.Start(startInfo);
            if (child is null) { LastStartFailure = "启动 serve 失败：系统没有返回进程"; return null; }
            child.OutputDataReceived += Remember;
            child.ErrorDataReceived += Remember;
            child.BeginOutputReadLine();
            child.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            LastStartFailure = $"启动 serve 失败：{ex.Message}";
            return null;
        }

        var deadline = Environment.TickCount64 + waitMs;
        while (Environment.TickCount64 < deadline)
        {
            await Task.Delay(150);
            if (await ConnectAsync(300) is { } client)
            {
                LastStartFailure = null;
                return client;
            }

            if (child.HasExited)
            {
                await Task.Delay(150);   // 让最后几行输出落进 words
                lock (words)
                {
                    LastStartFailure = $"serve 起来就退了（退出码 {child.ExitCode}）"
                        + (words.Count > 0 ? "：" + string.Join(" / ", words) : "，而且没有留下任何输出");
                }
                return null;
            }
        }

        lock (words)
        {
            LastStartFailure = $"serve 在 {waitMs} ms 内没有让管道可连"
                + (words.Count > 0 ? "，它说：" + string.Join(" / ", words) : "，也没有留下任何输出");
        }
        return null;
    }

    /// <summary>上一次启动 serve 失败的原因（编辑器显示给用户；成功时清空）。</summary>
    internal static string? LastStartFailure { get; private set; }
}
