using System.Text;
using System.Text.Json;
using KeyMouse; // the capability layer: the Runner drives exactly the dispatch the CLI drives

namespace KeyMouse.Runner;

/// <summary>
/// The resident engine: it accepts requests, runs them through the same
/// <see cref="Commands.Execute"/> the CLI uses, and streams what happens back to whoever asked.
///
/// Two decisions worth stating:
///
/// * **One job at a time.** Input is a global resource: two executions sending keys at once produce a
///   sequence neither of them asked for, and the console output that becomes the log stream is
///   process-wide anyway. Jobs are queued, not raced.
/// * **The log stream is the console.** A job redirects <c>Console.Out</c> into its event sink for
///   the duration of the run, so every line the capability layer already prints (progress, errors,
///   the exit-code report) reaches the client without Core knowing a socket exists.
/// </summary>
internal sealed class RunnerHost
{
    private readonly object _gate = new();
    private readonly List<RunnerJob> _jobs = [];
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);
    private int _nextJob;

    internal string Version => Commands.Version;

    internal IReadOnlyList<RunnerJob> Jobs
    {
        get { lock (_gate) return _jobs.ToArray(); }
    }

    /// <summary>Handles one request and writes whatever it produces through <paramref name="emit"/>.</summary>
    internal async Task HandleAsync(RunnerRequest request, Action<RunnerEvent> emit)
    {
        // 版本先对，再谈方法：一个说别的版本号的客户端，它的方法名和参数含义都不可信。
        if (!RunnerProtocol.AcceptsVersion(request.Params?.Version))
        {
            emit(RunnerEvent.Error(
                request.Id, 2,
                $"客户端说的是协议版本 {request.Params!.Version}，本 Runner 说的是 {RunnerProtocol.ContractVersion}；两边对齐版本后才能继续"));
            return;
        }

        switch (request.Method)
        {
            case "hello":
                // 与别的方法同一套规则：回显请求 id，以 result 终结。
                // 连接后那条 id=0 的问候是另一回事，见契约的 greeting。
                emit(RunnerEvent.Result(request.Id, Serialize(new
                {
                    version = Version,
                    protocol = RunnerProtocol.ContractVersion,
                })));
                return;

            case "list":
                emit(RunnerEvent.Result(request.Id, Serialize(Jobs.Select(j => j.Info()).ToArray())));
                return;

            case "status":
                emit(RunnerEvent.Result(request.Id, Serialize(new
                {
                    version = Version,
                    jobs = Jobs.Select(j => j.Info()).ToArray(),
                    pipeline = RunnerProtocol.PipeName,
                })));
                return;

            case "run":
            case "record":
                await ExecuteAsync(request, emit);
                return;

            case "validate":
                Validate(request, emit);
                return;

            case "pick-region":
                PickRegion(request, emit);
                return;

            case "ocr":
                Ocr(request, emit);
                return;

            case "cancel":
            case "pause":
            case "resume":
                Control(request, emit);
                return;

            case "shutdown":
                emit(RunnerEvent.Result(request.Id, Serialize(new { stopping = true })));
                _shutdown?.Invoke();
                return;

            default:
                // 方法清单只从 RunnerProtocol.Methods 来：改了协议而忘了同步，测试会先红。
                emit(RunnerEvent.Error(request.Id, 2, $"Runner 不认识 '{request.Method}'（可用：{string.Join(" | ", RunnerProtocol.Methods)}）"));
                return;
        }
    }

    private Action? _shutdown;

    internal void OnShutdown(Action action) => _shutdown = action;

    // ------------------------------------------------------------------ execution

    private async Task ExecuteAsync(RunnerRequest request, Action<RunnerEvent> emit)
    {
        RunnerParameters p = request.Params ?? new RunnerParameters();
        var job = new RunnerJob(Interlocked.Increment(ref _nextJob), request.Id, request.Method, emit) { Client = p.Client };
        lock (_gate) _jobs.Add(job);

        // The queue slot comes first, and everything that can fail sits inside this try: a flow that
        // will not load is the ordinary case, not an exotic one. It used to throw before the slot was
        // taken and outside every handler, which left the job sitting in "queued" for good - measured
        // on this machine: two jobs stuck that way and a client whose run could never end.
        await _oneAtATime.WaitAsync();
        TextWriter? previousOut = null, previousError = null;
        try
        {
            var arguments = new List<string> { request.Method };
            if (request.Method == "run")
            {
                if (string.IsNullOrWhiteSpace(p.Flow) || !File.Exists(p.Flow))
                    throw new CommandFailure(2, $"没有这个流程文件：{p.Flow}");

                // The plan, not the file: a `repeat`/`foreach` runs its body more than once, and
                // progress in the UI should count what will actually run.
                FlowDocument document = FlowDocument.Load(p.Flow, null);
                var plan = FlowPlan.Build(document, Path.GetFullPath(p.Flow!));
                job.Total = plan.StepCount;
                job.FlowPath = Path.GetFullPath(p.Flow!);
                job.AllowRestore = document.AllowRestore;
                job.Shots = p.Shots ?? false;
                job.EvidenceFor = p.EvidenceFor;
                job.Steps = plan.Items.Where(item => item.Step is not null).Select(item => item.Step!).ToList();
                arguments.Add(p.Flow!);
                if (p.DryRun == true) arguments.Add("--dry-run");
                if (p.Echo != false) arguments.Add("--echo"); // a client wants to see what happened
                if (p.KeepGoing == true) arguments.Add("--keep-going");
                if (p.DelayMs is int delay and > 0) arguments.AddRange(["--delay", delay.ToString()]);
                if (p.Retry is int retry and > 0) arguments.AddRange(["--retry", retry.ToString()]);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(p.Out)) arguments.AddRange(["--out", p.Out!]);
                if (p.DurationMs is int duration and > 0) arguments.AddRange(["--duration", duration.ToString()]);
                if (p.Shots == false) arguments.Add("--no-shots");
            }

            job.State = "running";
            Execution.Control = job;

            // Every line the capability layer prints becomes a log event (see the class comment).
            var sink = new EventTextWriter(emit, request.Id, job.Job);
            previousOut = Console.Out;
            previousError = Console.Error;
            Console.SetOut(sink);
            Console.SetError(sink);

            int exitCode;
            try
            {
                exitCode = await Task.Run(() => Dispatch(arguments.ToArray()));
            }
            catch (OperationCanceledException)
            {
                exitCode = 7; // cancelled by the client; nothing more was sent
            }

            job.ExitCode = exitCode;
            job.State = job.Cancelled ? "cancelled" : exitCode == 0 ? "finished" : "failed";
            emit(RunnerEvent.Finished(request.Id, job.Job, exitCode, Environment.TickCount64 - job.StartedAt));
        }
        catch (Exception ex)
        {
            // Whatever else goes wrong, the client hears about it and the job does not stay queued.
            int code = ex is CommandFailure failure ? failure.Code : 1;
            job.ExitCode = code;
            job.State = "failed";
            emit(RunnerEvent.Error(request.Id, code, ex.Message));
            emit(RunnerEvent.Finished(request.Id, job.Job, code, Environment.TickCount64 - job.StartedAt));
        }
        finally
        {
            if (previousOut is not null) Console.SetOut(previousOut);
            if (previousError is not null) Console.SetError(previousError);
            Execution.Control = null;
            job.FinishedAt = Environment.TickCount64;
            _oneAtATime.Release();
        }
    }

    /// <summary>The dispatch the CLI uses. A hook so tests can drive the host without a real console app.</summary>
    private int Dispatch(string[] arguments)
    {
        lock (_dispatchGate) return Commands.Execute(arguments);
    }

    private readonly object _dispatchGate = new();

    // ------------------------------------------------------------------ the small methods

    private static void Validate(RunnerRequest request, Action<RunnerEvent> emit)
    {
        string? path = request.Params?.Flow;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            emit(RunnerEvent.Error(request.Id, 2, $"没有这个流程文件：{path}"));
            return;
        }
        try
        {
            var document = FlowDocument.Load(path);
            emit(RunnerEvent.Result(request.Id, Serialize(new
            {
                ok = true,
                steps = document.Steps.Count,
                types = document.Steps.Select(s => s.Type).ToArray(),
                recordedAt = document.RecordedAt,
            })));
        }
        catch (CommandFailure ex)
        {
            emit(RunnerEvent.Error(request.Id, ex.Code, ex.Message));
        }
    }

    /// <summary>
    /// Region picking needs two things a pipe thread cannot give: an STA thread for the overlay, and
    /// the JSON the command prints. So it runs the same `region pick --json` the CLI runs, on a
    /// dedicated STA thread, and hands its output back verbatim.
    /// </summary>
    private void PickRegion(RunnerRequest request, Action<RunnerEvent> emit)
    {
        string json = "";
        int exitCode = 0;
        var thread = new Thread(() =>
        {
            var captured = new StringWriter();
            TextWriter previous = Console.Out;
            Console.SetOut(captured);
            try
            {
                exitCode = Commands.Execute(["region", "pick", "--json"]);
            }
            finally
            {
                Console.SetOut(previous);
            }
            json = captured.ToString().Trim();
        })
        {
            IsBackground = true,
            Name = "keymouse-pick-region",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exitCode != 0 || json.Length == 0)
        {
            emit(RunnerEvent.Error(request.Id, exitCode == 0 ? 3 : exitCode, "取区域被取消或失败"));
            return;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            emit(RunnerEvent.Result(request.Id, document.RootElement.Clone()));
        }
        catch (JsonException)
        {
            emit(RunnerEvent.Error(request.Id, 1, $"取区域返回了看不懂的内容：{json}"));
        }
    }

    private static void Ocr(RunnerRequest request, Action<RunnerEvent> emit)
    {
        RunnerParameters p = request.Params ?? new RunnerParameters();
        var arguments = new List<string> { "probe" };
        if (!string.IsNullOrWhiteSpace(p.Process)) arguments.AddRange(["--process", p.Process!]);
        if (!string.IsNullOrWhiteSpace(p.Class)) arguments.AddRange(["--class", p.Class!]);
        if (p.Region is { Length: 4 } region)
        {
            arguments.AddRange(["--region", string.Join(',', region)]);
        }
        arguments.Add("--json");

        var captured = new StringWriter();
        TextWriter previous = Console.Out;
        Console.SetOut(captured);
        int exitCode;
        try
        {
            exitCode = Commands.Execute(arguments.ToArray());
        }
        finally
        {
            Console.SetOut(previous);
        }

        string json = captured.ToString().Trim();
        if (json.Length > 0)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                emit(RunnerEvent.Result(request.Id, document.RootElement.Clone()));
                return;
            }
            catch (JsonException)
            {
                // fall through: report it as an error with the raw text
            }
        }
        emit(RunnerEvent.Error(request.Id, exitCode == 0 ? 6 : exitCode, $"OCR 没给出结果：{json}"));
    }

    private void Control(RunnerRequest request, Action<RunnerEvent> emit)
    {
        int target = request.Params?.Target ?? request.Id;
        RunnerJob? job = Jobs.FirstOrDefault(j => j.Job == target || j.RequestId == target);
        if (job is null)
        {
            emit(RunnerEvent.Error(request.Id, 3, $"没有这个作业：{target}"));
            return;
        }

        switch (request.Method)
        {
            case "cancel": job.Cancel(); break;
            case "pause": job.Pause(); break;
            case "resume": job.Resume(); break;
        }
        emit(RunnerEvent.Result(request.Id, Serialize(new { job = job.Job, state = job.State })));
    }

    // ------------------------------------------------------------------ serving

    /// <summary>`engine serve`: listen until cancelled or asked to shut down.</summary>
    internal static int Run(string[] args)
    {
        bool once = args.Contains("--once");
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    break;
                case "--once":
                    break;
                default:
                    return Commands.Fail(2, $"用法：KeyMouse serve [--once]（--once 只服务第一个连接，测试用）。收到：{args[i]}");
            }
        }

        var host = new RunnerHost();
        using var stop = new CancellationTokenSource();
        host.OnShutdown(stop.Cancel);

        Console.WriteLine($"KeyMouse {host.Version} Runner");
        Console.WriteLine($"  管道 {RunnerProtocol.PipeName}");
        Console.WriteLine("  客户端（编辑器）连上来就能用；Ctrl+C 或 shutdown 退出。");

        var server = new PipeServer(host, stop);
        try
        {
            server.RunAsync().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // asked to stop
        }
        Console.WriteLine("Runner 已停止。");
        return 0;
    }

    internal static JsonElement Serialize<T>(T value) =>
        JsonSerializer.SerializeToElement(value, RunnerProtocol.Json);
}

/// <summary>Turns every line the capability layer writes into a log event.</summary>
internal sealed class EventTextWriter : TextWriter
{
    private readonly Action<RunnerEvent> _emit;
    private readonly int _requestId;
    private readonly int _job;
    private readonly StringBuilder _pending = new();

    internal EventTextWriter(Action<RunnerEvent> emit, int requestId, int job)
    {
        _emit = emit;
        _requestId = requestId;
        _job = job;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\n') FlushLine();
        else if (value != '\r') _pending.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null) return;
        foreach (char c in value) Write(c);
    }

    public override void WriteLine(string? value)
    {
        Write(value);
        FlushLine();
    }

    private void FlushLine()
    {
        string line = _pending.ToString();
        _pending.Clear();
        _emit(RunnerEvent.Log(_requestId, _job, line));
    }
}
