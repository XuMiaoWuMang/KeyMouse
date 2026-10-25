using System.Diagnostics;
using System.Text.Json;
using KeyMouse.Runner;

namespace KeyMouse.FlowEditor;

/// <summary>What the Runner's `pick-region` reports, reduced to what the editor needs.</summary>
internal sealed record PickedRegion(
    string Space, int X, int Y, int Width, int Height, string? Process, string? Class, string? Title, string? Note)
{
    /// <summary>同一个点的屏幕坐标。步骤没有目标窗口时（"就发到这个屏幕位置"）只有它有意义。</summary>
    internal int ScreenX { get; init; }
    internal int ScreenY { get; init; }
}

/// <summary>
/// The editor's link to the product's other half: not a pile of one-shot child processes, but one
/// connection to the resident Runner. Everything real - the overlay, the execution, the exit codes -
/// happens inside that process, which runs the same dispatch the CLI runs; the editor only edits and
/// watches. If no Runner is listening, one is started (`serve`) and the client waits for it.
/// </summary>
internal static class KeyMouseBridge
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Finds KeyMouse.exe: next to the editor first, then in the `cli` folder beside it (that is how
    /// the installer lays them out), then up the tree for a development checkout, then PATH.
    /// It is what `serve` is started from.
    ///
    /// 安装程序把命令行放在 `<安装目录>\cli\`，而编辑器只会在自己目录里找——
    /// 结果装完之后编辑器找不到引擎，播放和试运行直接失败（实测反馈）。这条候选不能少。
    /// </summary>
    internal static string? ResolveTool()
    {
        // 顺序：**身边这份**优先，注册表里的安装版最后。
        // 理由很实际：开发时 bin\…\KeyMouse.FlowEditor.exe 旁边就是刚编的 dist\KeyMouse.exe，
        // 而注册表指向安装目录里的旧 CLI——旧 CLI 不认识新参数（--parent 就是这么炸的：
        // 新编辑器拉起旧 serve，退出码 2）。安装版自己的 CLI 在 <安装目录>\cli\ 里，仍然找得到。
        var candidates = new List<string>();
        string here = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(here, "KeyMouse.exe"));
        candidates.Add(Path.Combine(here, "cli", "KeyMouse.exe"));

        var directory = new DirectoryInfo(here);
        for (int i = 0; i < 10 && directory is not null; i++, directory = directory.Parent)
        {
            candidates.Add(Path.Combine(directory.FullName, "dist", "KeyMouse.exe"));
        }
        candidates.Add(Path.Combine(Environment.CurrentDirectory, "dist", "KeyMouse.exe"));
        candidates.Add(Path.Combine(Environment.CurrentDirectory, "KeyMouse.exe"));

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }

        if (ProductRegistry.Read(ProductRegistry.CliValue) is { } registered) return registered;

        string? path = Environment.GetEnvironmentVariable("PATH");
        foreach (string folder in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim(), "KeyMouse.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // a malformed PATH entry is not worth failing over
            }
        }
        return null;
    }

    /// <summary>Connects to the Runner, starting one if necessary.</summary>
    internal static async Task<RunnerClient?> ConnectAsync(string? tool)
    {
        if (await RunnerClient.ConnectAsync(400) is { } already) return already;
        if (tool is null) return null;
        return await RunnerClient.ConnectOrStartAsync(tool);
    }

    /// <summary>
    /// Asks the Runner to run the region picker (the same overlay the CLI uses, on an STA thread
    /// inside the service) and returns what the human drew.
    /// </summary>
    internal static Task<PickedRegion?> PickRegionAsync(RunnerClient client) => PickAsync(client, point: false);

    /// <summary>只要一个点：浮层里点一下就结束（点击类动作不需要框）。</summary>
    internal static Task<PickedRegion?> PickPointAsync(RunnerClient client) => PickAsync(client, point: true);

    private static async Task<PickedRegion?> PickAsync(RunnerClient client, bool point)
    {
        RunnerEvent result = await client.SendAsync("pick-region", point ? new RunnerParameters { Mode = "point" } : null);
        if (result.Kind != "result" || result.Value is not { } value) return null;

        if (!value.TryGetProperty("region", out var region) || region.ValueKind != JsonValueKind.Array) return null;
        int[] rect = region.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        if (rect.Length != 4) return null;

        string space = value.TryGetProperty("space", out var spaceElement) && spaceElement.ValueKind == JsonValueKind.String
            ? spaceElement.GetString() ?? "client"
            : "client";

        string? processName = null, className = null, title = null;
        if (value.TryGetProperty("window", out var window) && window.ValueKind == JsonValueKind.Object)
        {
            className = window.TryGetProperty("class", out var c) ? c.GetString() : null;
            title = window.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (window.TryGetProperty("pid", out var pidElement) && pidElement.TryGetInt32(out int pid))
            {
                try { processName = Process.GetProcessById(pid).ProcessName; }
                catch (ArgumentException) { /* the window died while we were asking */ }
            }
        }

        string? note = value.TryGetProperty("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String
            ? noteElement.GetString()
            : null;
        int screenX = rect[0], screenY = rect[1];
        if (value.TryGetProperty("screen", out var screen) && screen.ValueKind == JsonValueKind.Object &&
            screen.TryGetProperty("rect", out var screenRect) && screenRect.ValueKind == JsonValueKind.Array)
        {
            int[] absolute = screenRect.EnumerateArray().Select(element => element.GetInt32()).ToArray();
            if (absolute.Length == 4) { screenX = absolute[0]; screenY = absolute[1]; }
        }

        return new PickedRegion(space, rect[0], rect[1], rect[2], rect[3], processName, className, title, note)
        {
            ScreenX = screenX,
            ScreenY = screenY,
        };
    }

    /// <summary>
    /// Hands a flow to the Runner and streams everything it reports: log lines, per-step start and
    /// finish, and the terminal event. Returns the exit code (7 = cancelled by the client).
    /// </summary>
    internal static async Task<int> RunFlowAsync(
        RunnerClient client, string flowPath, bool dryRun, string? evidenceFor,
        Action<RunnerEvent> onEvent, CancellationToken cancellation)
    {
        RunnerEvent finished = await client.SendAsync("run", new RunnerParameters
        {
            Flow = flowPath,
            EvidenceFor = evidenceFor,
            DryRun = dryRun,
            Shots = !dryRun,   // 试运行不发输入、不改屏幕，没有可留的证据；真实运行才截图
            Echo = true,
        }, onEvent, cancellation);

        return finished.Kind switch
        {
            "finished" => finished.ExitCode ?? 0,
            "error" => finished.Code ?? 1,
            _ => 1,
        };
    }

    /// <summary>
    /// 录一段操作，写成流程文件。结束靠录制进程的全局热键 Ctrl+Alt+Q（它才在监听键鼠），
    /// 客户端能做的只是"不再等"——所以取消只影响界面，不会丢已经录到的内容。
    /// </summary>
    internal static async Task<int> RecordAsync(
        RunnerClient client, string outPath, Action<RunnerEvent> onEvent, CancellationToken cancellation)
    {
        RunnerEvent finished = await client.SendAsync("record", new RunnerParameters
        {
            Out = outPath,
            Echo = true,
            Shots = true,   // 录制时逐步留图：回放时能看见"当时屏幕上是什么"
        }, onEvent, cancellation);

        return finished.Kind switch
        {
            "finished" => finished.ExitCode ?? 0,
            "error" => finished.Code ?? 1,
            _ => 1,
        };
    }

    internal static Task<RunnerEvent> CancelAsync(RunnerClient client, int job) =>
        client.SendAsync("cancel", new RunnerParameters { Target = job });

    internal static Task<RunnerEvent> PauseAsync(RunnerClient client, int job) =>
        client.SendAsync("pause", new RunnerParameters { Target = job });

    internal static Task<RunnerEvent> ResumeAsync(RunnerClient client, int job) =>
        client.SendAsync("resume", new RunnerParameters { Target = job });
}
