using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KeyMouse.FlowEditor;

/// <summary>What `region pick --json` reports, reduced to what the editor needs.</summary>
internal sealed record PickedRegion(
    string Space, int X, int Y, int Width, int Height, string? Process, string? Class, string? Title, string? Note);

/// <summary>
/// The editor does not re-implement picking, reading or replaying: it asks the console tool, which
/// already owns those behaviours (and their measurements). That also means the editor's "取区域"
/// is the same overlay, and "播放" is the same dispatcher with the same gates and exit codes.
/// </summary>
internal static class KeyMouseBridge
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Finds KeyMouse.exe: next to the editor first (that is how a release would ship them), then up
    /// the tree for a development checkout, then PATH.
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

    /// <summary>Runs `region pick --json`, i.e. the real overlay, and returns what the human drew.</summary>
    internal static async Task<PickedRegion?> PickRegionAsync(string tool)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("region");
        startInfo.ArgumentList.Add("pick");
        startInfo.ArgumentList.Add("--json");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("启动不了 KeyMouse.exe");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 || stdout.Length == 0) return null;

        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        if (!root.TryGetProperty("region", out var region) || region.ValueKind != JsonValueKind.Array) return null;
        int[] rect = region.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        if (rect.Length != 4) return null;

        string space = root.TryGetProperty("space", out var spaceElement) && spaceElement.ValueKind == JsonValueKind.String
            ? spaceElement.GetString() ?? "client"
            : "client";
        string? processName = null, className = null, title = null;
        if (root.TryGetProperty("window", out var window) && window.ValueKind == JsonValueKind.Object)
        {
            className = window.TryGetProperty("class", out var c) ? c.GetString() : null;
            title = window.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (window.TryGetProperty("pid", out var pidElement) && pidElement.TryGetInt32(out int pid))
            {
                try { processName = Process.GetProcessById(pid).ProcessName; }
                catch (ArgumentException) { /* the window died while we were asking */ }
            }
        }

        string? note = root.TryGetProperty("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String
            ? noteElement.GetString()
            : null;
        return new PickedRegion(space, rect[0], rect[1], rect[2], rect[3], processName, className, title, note);
    }

    /// <summary>
    /// Replays a flow through the console tool and streams its output line by line. No shell, no
    /// quoting: every argument is passed as an argument, so a process name with spaces is safe.
    /// </summary>
    internal static async Task<int> RunFlowAsync(
        string tool, string flowPath, bool dryRun, IEnumerable<string> extraArguments,
        Action<string> onLine, CancellationToken cancellation)
    {
        var startInfo = new ProcessStartInfo(tool)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add(flowPath);
        if (dryRun) startInfo.ArgumentList.Add("--dry-run");
        foreach (string argument in extraArguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("启动不了 KeyMouse.exe");
        using var registration = cancellation.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already gone */ }
        });

        var reading = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) is not null) onLine(line);
            while ((line = await process.StandardError.ReadLineAsync()) is not null) onLine(line);
        });
        await process.WaitForExitAsync(cancellation);
        await reading;
        return process.ExitCode;
    }
}
