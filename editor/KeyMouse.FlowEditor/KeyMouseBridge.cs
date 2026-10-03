using System.Diagnostics;
using System.Text.Json;
using KeyMouse.Runner;

namespace KeyMouse.FlowEditor;

/// <summary>What the Runner's `pick-region` reports, reduced to what the editor needs.</summary>
internal sealed record PickedRegion(
    string Space, int X, int Y, int Width, int Height, string? Process, string? Class, string? Title, string? Note);

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
    /// Finds KeyMouse.exe: next to the editor first (that is how a release ships them), then up the
    /// tree for a development checkout, then PATH. It is what `serve` is started from.
    /// </summary>
    internal static string? ResolveTool()
    {
        var candidates = new List<string>();
        string here = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(here, "KeyMouse.exe"));

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
    internal static async Task<PickedRegion?> PickRegionAsync(RunnerClient client)
    {
        RunnerEvent result = await client.SendAsync("pick-region");
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
        return new PickedRegion(space, rect[0], rect[1], rect[2], rect[3], processName, className, title, note);
    }

    /// <summary>
    /// Hands a flow to the Runner and streams everything it reports: log lines, per-step start and
    /// finish, and the terminal event. Returns the exit code (7 = cancelled by the client).
    /// </summary>
    internal static async Task<int> RunFlowAsync(
        RunnerClient client, string flowPath, bool dryRun,
        Action<RunnerEvent> onEvent, CancellationToken cancellation)
    {
        RunnerEvent finished = await client.SendAsync("run", new RunnerParameters
        {
            Flow = flowPath,
            DryRun = dryRun,
            Echo = true,
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
