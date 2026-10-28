using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using KeyMouse;
using KeyMouse.Runner;

namespace KeyMouse.Tests;

/// <summary>
/// The resident Runner, tested without a real desktop and without the user's own runner: the host is
/// driven in process for the request/response behaviour, and over a private named pipe for the parts
/// that only exist on the wire (framing, streaming, cancel while a job is running).
/// </summary>
internal static class RunnerTests
{
    public static void Run()
    {
        Harness.Group("runner: requests and events");
        var host = new RunnerHost();

        var status = Run(host, new RunnerRequest { Id = 1, Method = "status" });
        Harness.Equal("status is answered with a result", "result", status[^1].Kind);
        Harness.Check("...carrying the version", status[^1].Value?.GetProperty("version").GetString() == Commands.Version);
        Harness.Check("...and the pipe name a client should use",
            status[^1].Value?.GetProperty("pipeline").GetString()?.StartsWith("keymouse-runner-") == true);

        var hello = Run(host, new RunnerRequest { Id = 2, Method = "hello" });
        Harness.Equal("hello answers with a result", "result", hello[^1].Kind);
        Harness.Equal("...echoing the request id", 2, hello[^1].Id);
        Harness.Check("...carrying the runner version and the protocol version",
            hello[^1].Value?.GetProperty("version").GetString() == Commands.Version &&
            hello[^1].Value?.GetProperty("protocol").GetString() == RunnerProtocol.ContractVersion);

        var unknown = Run(host, new RunnerRequest { Id = 3, Method = "teleport" });
        Harness.Equal("an unknown method is an error, not a crash", "error", unknown[^1].Kind);
        Harness.Equal("...with a usage exit code", 2, unknown[^1].Code);

        var missing = Run(host, new RunnerRequest
        {
            Id = 4,
            Method = "run",
            Params = new RunnerParameters { Flow = Path.Combine(Path.GetTempPath(), "no-such-flow.json") },
        });
        Harness.Check("running a flow that does not exist reports the error, then ends the job",
            missing.Any(e => e.Kind == "error" && e.Code == 2) &&
            missing[^1].Kind == "finished" && missing[^1].ExitCode == 2,
            string.Join(" / ", missing.Select(e => $"{e.Kind}:{e.Code}-{e.ExitCode}")));
        Harness.Check("...and the job does not stay queued for good",
            host.Jobs.All(j => j.State != "queued"),
            string.Join(" / ", host.Jobs.Select(j => $"{j.Job}:{j.State}")));

        Harness.Group("runner: a flow that will not load still ends its job");

        string unloadable = Path.Combine(Path.GetTempPath(), $"keymouse-unloadable-{Environment.ProcessId}.json");
        string healthy = Path.Combine(Path.GetTempPath(), $"keymouse-healthy-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(unloadable, """{"format":"keymouse-flow","version":1,"steps":[{"type":"teleport"}]}""");
            List<RunnerEvent> broken = Run(host, new RunnerRequest
            {
                Id = 90,
                Method = "run",
                Params = new RunnerParameters { Flow = unloadable },
            });
            Harness.Equal("the client is told it failed", "finished", broken[^1].Kind);
            Harness.Equal("...with the loader's exit code", 2, broken[^1].ExitCode);
            Harness.Check("...and the failure was explained", broken.Any(e => e.Kind == "error"), "no error event");
            Harness.Check("...and the job is not left queued (measured once: two jobs stuck that way, and a client that could never end its run)",
                host.Jobs.All(j => j.State != "queued"), string.Join(", ", host.Jobs.Select(j => $"{j.Job}:{j.State}")));

            File.WriteAllText(healthy, """{"format":"keymouse-flow","version":1,"steps":[{"type":"sleep","ms":30}]}""");
            List<RunnerEvent> after = Run(host, new RunnerRequest
            {
                Id = 91,
                Method = "run",
                Params = new RunnerParameters { Flow = healthy, Echo = false },
            });
            Harness.Equal("...so the next run still works", "finished", after[^1].Kind);
            Harness.Equal("...with its own exit code", 0, after[^1].ExitCode);
        }
        finally
        {
            foreach (string path in new[] { unloadable, healthy })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        Harness.Group("runner: validate, run and cancel");

        string flow = Path.Combine(Path.GetTempPath(), $"keymouse-runner-test-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(flow, """
                {"format":"keymouse-flow","version":1,"steps":[
                  {"type":"sleep","ms":120},{"type":"sleep","ms":120},{"type":"sleep","ms":120}]}
                """);

            var valid = Run(host, new RunnerRequest { Id = 5, Method = "validate", Params = new RunnerParameters { Flow = flow } });
            Harness.Equal("validate reports a result", "result", valid[^1].Kind);
            Harness.Equal("...with the step count", 3, valid[^1].Value?.GetProperty("steps").GetInt32());

            var dry = Run(host, new RunnerRequest
            {
                Id = 6,
                Method = "run",
                Params = new RunnerParameters { Flow = flow, DryRun = true },
            });
            Harness.Equal("a dry run finishes", "finished", dry[^1].Kind);
            Harness.Equal("...with exit code 0", 0, dry[^1].ExitCode);
            Harness.Check("...and reports every step as it starts",
                dry.Count(e => e.Kind == "step" && e.State == "started") == 3);
            Harness.Check("...and as it finishes",
                dry.Count(e => e.Kind == "step" && e.State == "finished") == 3);
            Harness.Equal("...with the total step count on each event", 3, dry.First(e => e.Kind == "step").Total);
            Harness.Check("...and streams the log lines the capability layer prints",
                dry.Any(e => e.Kind == "log" && (e.Line ?? "").Contains("流程结束")));
        }
        finally
        {
            if (File.Exists(flow)) File.Delete(flow);
        }

        // A real (not dry) run, cancelled half way: the seam has to turn that into exit code 7.
        string slow = Path.Combine(Path.GetTempPath(), $"keymouse-runner-cancel-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(slow, """
                {"format":"keymouse-flow","version":1,"steps":[
                  {"type":"sleep","ms":400},{"type":"sleep","ms":400},{"type":"sleep","ms":400}]}
                """);
            var events = new List<RunnerEvent>();
            var gate = new object();
            void Emit(RunnerEvent e) { lock (gate) events.Add(e); }

            Task running = host.HandleAsync(new RunnerRequest
            {
                Id = 7,
                Method = "run",
                Params = new RunnerParameters { Flow = slow },
            }, Emit);
            Thread.Sleep(200);
            RunnerJob? job = host.Jobs.LastOrDefault();
            Harness.Check("a running job is visible to the client", job is { State: "running" });
            job?.Cancel();
            running.Wait(TimeSpan.FromSeconds(10));

            RunnerEvent finished = events.Last(e => e.Kind == "finished");
            Harness.Equal("cancelling a job ends it with exit code 7", 7, finished.ExitCode);
            Harness.Check("...and no later step is reported as started",
                events.Count(e => e.Kind == "step" && e.State == "started") < 3);
        }
        finally
        {
            if (File.Exists(slow)) File.Delete(slow);
        }

        Harness.Group("runner: the pipe carries the same conversation");
        string pipeName = RunnerProtocol.TestPipeName(Environment.ProcessId.ToString());
        var stop = new CancellationTokenSource();
        var server = new PipeServer(host, stop, pipeName);
        Task serving = server.RunAsync();
        try
        {
            var client = Connect(pipeName).GetAwaiter().GetResult();
            Harness.Check("a client can connect and is greeted", client is not null);
            if (client is not null)
            {
                using (client)
                {
                    RunnerEvent reply = client.SendAsync("status").GetAwaiter().GetResult();
                    Harness.Equal("status over the pipe is a result", "result", reply.Kind);
                    Harness.Check("...with the same version the CLI reports",
                        reply.Value?.GetProperty("version").GetString() == Commands.Version);

                    var seen = new List<string>();
                    RunnerEvent run = client.SendAsync("run", new RunnerParameters
                    {
                        Flow = WriteTemporarySteps(2),
                        DryRun = true,
                    }, e => { lock (seen) seen.Add(e.Kind); }).GetAwaiter().GetResult();
                    Harness.Equal("a dry run over the pipe finishes with 0", 0, run.ExitCode);
                    Harness.Check("...after streaming step events", seen.Count(k => k == "step") == 4);
                    Harness.Check("...and log events", seen.Contains("log"));

                    RunnerEvent bad = client.SendAsync("nonsense").GetAwaiter().GetResult();
                    Harness.Equal("an unknown method comes back as an error", "error", bad.Kind);
                }
            }

            // 问候（contract 里的 greeting）是契约里的一条：连上就先收到 hello(id=0)，不用先问什么。
            using (var raw = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                raw.Connect(2000);
                using var reader = new StreamReader(raw, new UTF8Encoding(false));
                var read = Task.Run(() => reader.ReadLine());
                string? first = read.Wait(TimeSpan.FromSeconds(5)) ? read.Result : null;
                RunnerEvent? greeting = first is null ? null : JsonSerializer.Deserialize<RunnerEvent>(first, RunnerProtocol.Json);
                Harness.Check("a fresh connection is greeted before it asks anything",
                    greeting is { Kind: "hello", Id: 0 } && greeting.Message == Commands.Version,
                    $"{greeting?.Kind}#{greeting?.Id} {greeting?.Message}");
            }
        }
        finally
        {
            stop.Cancel();
            try { serving.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { /* shutting down */ }
            stop.Dispose();
        }
    }

    private static async Task<RunnerClient?> Connect(string pipeName)
    {
        for (int i = 0; i < 20; i++)
        {
            var client = await RunnerClient.ConnectAsync(500, pipeName);
            if (client is not null) return client;
            await Task.Delay(100);
        }
        return null;
    }

    private static string WriteTemporarySteps(int count)
    {
        string path = Path.Combine(Path.GetTempPath(), $"keymouse-runner-pipe-{Environment.ProcessId}-{count}.json");
        string steps = string.Join(',', Enumerable.Range(0, count).Select(_ => """{"type":"sleep","ms":10}"""));
        File.WriteAllText(path, $$"""{"format":"keymouse-flow","version":1,"steps":[{{steps}}]}""");
        return path;
    }

    private static List<RunnerEvent> Run(RunnerHost host, RunnerRequest request)
    {
        var events = new List<RunnerEvent>();
        host.HandleAsync(request, e => { lock (events) events.Add(e); }).GetAwaiter().GetResult();
        lock (events) return [.. events];
    }
}
