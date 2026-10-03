using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace KeyMouse.Runner;

/// <summary>
/// The transport: a named pipe, one JSON object per line, in both directions.
///
/// Requests are dispatched to their own tasks instead of being handled inline, and that is essential
/// rather than tidy: a `run` can take minutes, and the client has to be able to send `pause`,
/// `resume` or `cancel` while it is still going. Writing is serialised because job events come from
/// the job's own thread while results come from the request thread.
/// </summary>
internal sealed class PipeServer
{
    private readonly RunnerHost _host;
    private readonly CancellationTokenSource _stop;
    private readonly string _pipeName;

    internal PipeServer(RunnerHost host, CancellationTokenSource stop, string? pipeName = null)
    {
        _host = host;
        _stop = stop;
        _pipeName = pipeName ?? RunnerProtocol.PipeName;
    }

    internal async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            _ = Task.Run(() => HandleAsync(pipe, _stop.Token));
        }
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        var writeGate = new object();
        using var reader = new StreamReader(pipe, new UTF8Encoding(false));
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false));

        void Emit(RunnerEvent e)
        {
            string line = JsonSerializer.Serialize(e, RunnerProtocol.Json);
            lock (writeGate)
            {
                try
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
                catch (IOException)
                {
                    // the client went away; the job keeps running and its events are dropped
                }
                catch (ObjectDisposedException)
                {
                    // same
                }
            }
        }

        try
        {
            Emit(RunnerEvent.Hello(_host.Version));
            while (!stop.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(stop);
                if (line is null) break;
                if (line.Trim().Length == 0) continue;

                RunnerRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<RunnerRequest>(line, RunnerProtocol.Json);
                }
                catch (JsonException ex)
                {
                    Emit(RunnerEvent.Error(0, 2, $"这一行不是合法请求：{ex.Message}"));
                    continue;
                }

                if (request is null || string.IsNullOrWhiteSpace(request.Method))
                {
                    Emit(RunnerEvent.Error(0, 2, "请求缺少 method"));
                    continue;
                }

                RunnerRequest captured = request;
                _ = Task.Run(() => _host.HandleAsync(captured, Emit), stop);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (IOException)
        {
            // the client disconnected mid-line
        }
        finally
        {
            try { pipe.Dispose(); } catch (ObjectDisposedException) { /* already gone */ }
        }
    }
}
