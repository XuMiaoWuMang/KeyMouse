using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyMouse.Runner;

/// <summary>
/// The wire format between a client (the editor, a test, anything that speaks JSON lines) and the
/// resident Runner.
///
/// It is deliberately boring: one JSON object per line, UTF-8, over a named pipe. No ports, no
/// serialization framework, nothing that a script cannot speak in ten lines - and a human can watch
/// the traffic by reading it. Streaming is the reason it is a request/event pair rather than plain
/// RPC: an execution is long, and the point of the service is that the UI sees each step as it
/// happens instead of waiting for a process to exit.
/// </summary>
internal static class RunnerProtocol
{
    /// <summary>Per-user so two accounts on one machine cannot talk to each other's runner.</summary>
    internal static string PipeName => $"keymouse-runner-{Environment.UserName}";

    /// <summary>Tests use their own pipe so they can never talk to a runner the user has open.</summary>
    internal static string TestPipeName(string tag) => $"keymouse-runner-test-{Environment.UserName}-{tag}";

    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}

/// <summary>One request from a client. <c>id</c> is chosen by the client and echoed on every event.</summary>
internal sealed class RunnerRequest
{
    public int Id { get; set; }
    public string Method { get; set; } = "";
    public RunnerParameters? Params { get; set; }
}

/// <summary>
/// The parameters of every method in one shape. A flat bag keeps the protocol readable and lets a new
/// method appear without a new type; each method documents which fields it reads.
/// </summary>
internal sealed class RunnerParameters
{
    public string? Flow { get; set; }
    public string? Out { get; set; }
    public bool? DryRun { get; set; }
    public bool? Echo { get; set; }
    public bool? KeepGoing { get; set; }
    public int? DelayMs { get; set; }
    public int? Retry { get; set; }
    public int? DurationMs { get; set; }
    public bool? Shots { get; set; }
    public string? Process { get; set; }
    public string? Class { get; set; }
    public int[]? Region { get; set; }
    public int? Target { get; set; }
    public string? Client { get; set; }
    public string? Version { get; set; }
}

/// <summary>One line the Runner sends back. <c>kind</c> is the discriminator.</summary>
internal sealed class RunnerEvent
{
    public string Kind { get; set; } = "";
    public int Id { get; set; }
    public int? Job { get; set; }
    public string? Line { get; set; }
    public int? Index { get; set; }
    public int? Total { get; set; }
    public string? State { get; set; }
    public string? Type { get; set; }
    public int? ExitCode { get; set; }
    public long? DurationMs { get; set; }
    public int? Code { get; set; }
    public string? Message { get; set; }
    public JsonElement? Value { get; set; }

    internal static RunnerEvent Log(int id, int job, string line) =>
        new() { Kind = "log", Id = id, Job = job, Line = line };

    internal static RunnerEvent Step(int id, int job, int index, int total, string state, string type, int? exitCode = null, long? durationMs = null) =>
        new()
        {
            Kind = "step", Id = id, Job = job, Index = index, Total = total,
            State = state, Type = type, ExitCode = exitCode, DurationMs = durationMs,
        };

    internal static RunnerEvent Finished(int id, int job, int exitCode, long durationMs) =>
        new() { Kind = "finished", Id = id, Job = job, ExitCode = exitCode, DurationMs = durationMs };

    internal static RunnerEvent Error(int id, int code, string message) =>
        new() { Kind = "error", Id = id, Code = code, Message = message };

    internal static RunnerEvent Result(int id, JsonElement value) =>
        new() { Kind = "result", Id = id, Value = value };

    internal static RunnerEvent Hello(string version) =>
        new() { Kind = "hello", Id = 0, Message = version };
}

/// <summary>What a job is doing right now, as reported by `status` / `list`.</summary>
internal sealed class RunnerJobInfo
{
    public int Job { get; set; }
    public int Id { get; set; }
    public string Method { get; set; } = "";
    public string State { get; set; } = "queued";
    public string? Detail { get; set; }
    public long StartedAt { get; set; }
    public long? FinishedAt { get; set; }
    public int? ExitCode { get; set; }
}
