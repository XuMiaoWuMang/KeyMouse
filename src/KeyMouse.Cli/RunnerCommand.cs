using KeyMouse.Runner;

namespace KeyMouse.Cli;

/// <summary>
/// `KeyMouse runner ...` - the CLI talking to the resident Runner instead of doing the work itself.
///
/// This is the second half of "two entry points": the same service the editor connects to can be
/// driven from a script (`runner run flow.json`), inspected (`runner status`) and controlled
/// (`runner cancel 3`) without a UI in sight. `serve` starts it; everything else here is a client.
/// </summary>
internal static class RunnerCommand
{
    internal static int Run(string[] args)
    {
        string action = args.Length > 0 ? args[0] : "status";
        switch (action)
        {
            case "status":
                return Status();
            case "list":
                return Status();
            case "run":
                return RunFlow(args[1..]);
            case "cancel":
            case "pause":
            case "resume":
                return Control(action, args[1..]);
            case "stop":
                return Stop();
            default:
                return Commands.Fail(2,
                    "用法：KeyMouse runner <status | list | run <流程.json> [--dry-run] | cancel <作业号> | " +
                    "pause <作业号> | resume <作业号> | stop>（先 KeyMouse serve 启动常驻 Runner）");
        }
    }

    private static RunnerClient? Connect(out int exitCode)
    {
        exitCode = 0;
        RunnerClient? client = RunnerClient.ConnectAsync(1500).GetAwaiter().GetResult();
        if (client is not null) return client;
        exitCode = Commands.Fail(4,
            $"没有正在运行的 Runner（管道 {RunnerProtocol.PipeName}）。先在一个窗口里跑：KeyMouse serve");
        return null;
    }

    private static int Status()
    {
        RunnerClient? client = Connect(out int code);
        if (client is null) return code;
        using (client)
        {
            RunnerEvent reply = client.SendAsync("status").GetAwaiter().GetResult();
            if (reply.Kind != "result" || reply.Value is not { } value)
                return Commands.Fail(reply.Code ?? 1, reply.Message ?? "Runner 没给出状态");

            string version = value.GetProperty("version").GetString() ?? "?";
            string pipeline = value.GetProperty("pipeline").GetString() ?? "?";
            Console.WriteLine($"Runner 在线  KeyMouse {version}  管道 {pipeline}");

            var jobs = value.GetProperty("jobs");
            if (jobs.GetArrayLength() == 0)
            {
                Console.WriteLine("  没有作业（还没有跑过东西）");
            }
            else
            {
                Console.WriteLine("  作业：");
                foreach (var job in jobs.EnumerateArray())
                {
                    int id = job.GetProperty("job").GetInt32();
                    string state = job.GetProperty("state").GetString() ?? "?";
                    string method = job.GetProperty("method").GetString() ?? "?";
                    string exit = job.TryGetProperty("exitCode", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? $" 退出码 {e.GetInt32()}"
                        : "";
                    Console.WriteLine($"    #{id} {method} {state}{exit}");
                }
            }
            return 0;
        }
    }

    private static int RunFlow(string[] args)
    {
        if (args.Length == 0) return Commands.Fail(2, "用法：KeyMouse runner run <流程.json> [--dry-run] [--keep-going]");
        string path = Path.GetFullPath(args[0]);
        if (!File.Exists(path)) return Commands.Fail(2, $"没有这个文件：{path}");
        bool dryRun = args.Contains("--dry-run");
        bool keepGoing = args.Contains("--keep-going");

        RunnerClient? client = Connect(out int code);
        if (client is null) return code;
        using (client)
        {
            Console.WriteLine($"通过常驻 Runner 执行 {path}{(dryRun ? "（--dry-run）" : "")}");
            var seen = 0;
            RunnerEvent finished = client.SendAsync("run", new RunnerParameters
            {
                Flow = path,
                DryRun = dryRun,
                KeepGoing = keepGoing,
                Echo = true,
            }, e =>
            {
                switch (e.Kind)
                {
                    case "log":
                        if (!string.IsNullOrEmpty(e.Line)) Console.WriteLine($"  {e.Line}");
                        break;
                    case "step" when e.State == "started":
                        seen++;
                        Console.WriteLine($"  [{e.Index}/{e.Total}] 开始 {e.Type}");
                        break;
                }
            }).GetAwaiter().GetResult();

            if (finished.Kind == "error")
                return Commands.Fail(finished.Code ?? 1, finished.Message ?? "Runner 拒绝了这次执行");
            int exitCode = finished.ExitCode ?? 0;
            Console.WriteLine($"  结束：退出码 {exitCode}，{finished.DurationMs} ms，{seen} 步");
            return exitCode;
        }
    }

    private static int Control(string action, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int job))
            return Commands.Fail(2, $"用法：KeyMouse runner {action} <作业号>（作业号见 runner status）");

        RunnerClient? client = Connect(out int code);
        if (client is null) return code;
        using (client)
        {
            RunnerEvent reply = client.SendAsync(action, new RunnerParameters { Target = job }).GetAwaiter().GetResult();
            if (reply.Kind == "error") return Commands.Fail(reply.Code ?? 1, reply.Message ?? "Runner 拒绝了这次控制");
            string state = reply.Value?.TryGetProperty("state", out var s) == true ? s.GetString() ?? "?" : "?";
            Console.WriteLine($"作业 #{job} 现在是 {state}");
            return 0;
        }
    }

    private static int Stop()
    {
        RunnerClient? client = Connect(out int code);
        if (client is null) return code;
        using (client)
        {
            client.SendAsync("shutdown").GetAwaiter().GetResult();
            Console.WriteLine("已请 Runner 退出。");
            return 0;
        }
    }
}
