using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using KeyMouse.Runner;

namespace KeyMouse.Tests;

internal static class RunnerV2Tests
{
    internal static void Run()
    {
        static string? V(string? max, string? min) => RunnerProtocol.Negotiate(new RunnerRequest { ProtocolVersion = max, MinCompatibleVersion = min });
        Harness.Equal("legacy request selects v1", "1", V(null, null));
        Harness.Equal("highest common dialect", "2.0", V("3.0", "1.5"));
        Harness.Equal("only v1 in range", "1", V("1.9", "1.0"));
        Harness.Check("reversed, malformed and disjoint ranges rejected", V("1", "2") == null && V("2.x", "1") == null && V("3", "2.1") == null);
        var d = new ShotDelta();
        NativeCapture.Frame Frame(int width, params byte[] pixels) => new(width, 2, pixels);
        var bytes = new byte[32];
        var full = d.Next(1, Frame(4, bytes));
        Harness.Check("diff starts with a full BGRA frame", full is { BaseFrame: null, W: 4, H: 2 } && full.Bgra.Length == 32);
        Harness.Check("identical pixels omitted", d.Next(2, Frame(4, bytes)) == null);
        byte[] changed = (byte[])bytes.Clone(); changed[24] = 127;
        var patch = d.Next(3, Frame(4, changed));
        Harness.Check("only changed rectangle sent; base is last transmitted frame", patch is { BaseFrame: 1, X: 2, Y: 1, W: 1, H: 1 } && patch.Bgra.SequenceEqual(changed[24..28]));
        Harness.Check("resize starts a new full frame", d.Next(4, Frame(2, new byte[16])) is { BaseFrame: null, W: 2, H: 2 });
        var ring = new ProgressSession();
        for (int i = 0; i <= ProgressSession.Capacity; i++) ring.Publish(RunnerEvent.Step(1, 1, i, 1000, "started", "sleep"));
        var stale = new List<RunnerEvent>();
        ring.Follow(3, 0, stale.Add, CancellationToken.None).GetAwaiter().GetResult();
        Harness.Check("expired cursor cannot silently miss events", stale.Single().Code == 3);
        Wire().GetAwaiter().GetResult();
        ServerCrash().GetAwaiter().GetResult();
    }

    private static async Task ServerCrash()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var nonce = Guid.NewGuid();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, FlowRun.Flow("""[{"type":"sleep","ms":0},{"type":"sleep","ms":0}]"""));
            using var child = await RunnerFixture.Start(nonce, deadline.Token);
            try
            {
                using var client = await RunnerClient.ConnectAsync(5000, RunnerProtocol.TestPipeName(nonce.ToString("N"))) ?? throw new IOException("no test runner");
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = client.SendAsync("run", new RunnerParameters { Flow = path, DryRun = true, DelayMs = 10000 }, e =>
                {
                    if (e.Kind == "step") started.TrySetResult();
                }, deadline.Token);
                await started.Task.WaitAsync(deadline.Token);
                child.Kill();
                await child.WaitForExitAsync(deadline.Token);
                var interrupted = await pending.WaitAsync(deadline.Token);
                Harness.Check("Runner death terminates pending run with a disconnect diagnostic, not success or timeout",
                    interrupted is { Kind: "error", Code: 1, Message: "Runner 已停止" });
                var later = await client.SendAsync("status", cancellation: deadline.Token);
                Harness.Check("dead connection rejects subsequent work without restarting or retrying it", later is { Kind: "error", Code: 1 });
            }
            finally { if (!child.HasExited) child.Kill(); await child.WaitForExitAsync(); }
        }
        finally { File.Delete(path); }
    }

    private static async Task Wire()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string old = RunnerProtocol.TestPipeName(Guid.NewGuid().ToString("N"));
        using (var server = RunnerPipe.Listen(old))
        {
            var answering = Task.Run(async () =>
            {
                await server.WaitForConnectionAsync(deadline.Token);
                using var r = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                using var w = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                for (int i = 0; i < 2; i++)
                {
                    using var doc = JsonDocument.Parse((await r.ReadLineAsync(deadline.Token))!);
                    var root = doc.RootElement;
                    if (i == 1 && (root.TryGetProperty("protocolVersion", out _) || root.GetProperty("method").GetString() != "status")) throw new InvalidDataException("Not a v1 request");
                    await w.WriteLineAsync(JsonSerializer.Serialize(new { id = root.GetProperty("id").GetInt32(), kind = "result", value = new { protocol = "1" } }));
                }
            });
            using var client = await RunnerClient.ConnectAsync(2000, old) ?? throw new IOException("v1 handshake failed");
            Harness.Check("v2 client falls back before sending work to old server", client.ProtocolVersion == "1" && (await client.SendAsync("status", cancellation: deadline.Token)).Kind == "result");
            await answering;
        }
        using var stop = new CancellationTokenSource();
        string name = RunnerProtocol.TestPipeName(Guid.NewGuid().ToString("N"));
        var host = new RunnerHost();
        var running = new PipeServer(host, stop, name).RunAsync();
        string file = Path.Combine(Path.GetTempPath(), $"km-v2-{Guid.NewGuid():N}.json");
        try
        {
            // Old client, new server: no additional fields, no negotiation required.
            using (var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(deadline.Token);
                using var r = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                using var w = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                await r.ReadLineAsync(deadline.Token);
                await w.WriteLineAsync("""{"id":77,"method":"hello","params":{"version":"1"}}""");
                using var response = JsonDocument.Parse((await r.ReadLineAsync(deadline.Token))!);
                var value = response.RootElement.GetProperty("value");
                Harness.Check("old client receives unchanged hello", value.GetProperty("protocol").GetString() == "1" && !value.TryGetProperty("capabilities", out _));
            }
            File.WriteAllText(file, FlowRun.Flow("[" + string.Join(',', Enumerable.Repeat("""{"type":"sleep","ms":0}""", 8)) + "]"));
            string token;
            using (var first = await RunnerClient.ConnectAsync(2000, name) ?? throw new IOException("v2 handshake failed"))
            {
                Harness.Check("client negotiates implemented v2 capabilities", first.Capabilities.SequenceEqual(RunnerProtocol.Capabilities));
                var opened = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = first.SendAsync("run", new RunnerParameters { Flow = file, DryRun = true, DelayMs = 30 }, ev =>
                {
                    if (ev.Kind == "status-stream") opened.TrySetResult(ev.Value!.Value.GetProperty("session").GetString()!);
                }, deadline.Token);
                token = await opened.Task.WaitAsync(deadline.Token);
            }
            using var second = await RunnerClient.ConnectAsync(2000, name) ?? throw new IOException("reconnect failed");
            var seen = new List<long>(); var states = new List<string>();
            var result = await second.SendAsync("watch", new RunnerParameters { Session = token, After = 0 }, ev =>
            {
                if (ev.Kind != "status-stream") return;
                foreach (var item in ev.Value!.Value.GetProperty("steps").EnumerateArray())
                {
                    seen.Add(item.GetProperty("cursor").GetInt64()); states.Add(item.GetProperty("kind").GetString()!);
                }
            }, deadline.Token);
            Harness.Check("disconnect + replay + live tail does not re-run or skip a step", result.Kind == "result" && host.Jobs.Count == 1 && states.Count(s => s == "step") == 16 && states[^1] == "finished" && seen.SequenceEqual(Enumerable.Range(1, seen.Count).Select(i => (long)i)));
            Harness.Check("foreign session token refused", (await second.SendAsync("watch", new RunnerParameters { Session = "unknown" }, cancellation: deadline.Token)).Code == 3);
        }
        finally { stop.Cancel(); await running; File.Delete(file); }
    }
}
