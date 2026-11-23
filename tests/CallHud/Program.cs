using System.Text.Json;
using KeyMouse;
using KeyMouse.Flow;
using KeyMouse.Runtime;

const string Prefix = "@keymouse-hud/v1 ";
string directory = Path.Combine(Path.GetTempPath(), "keymouse-call-hud-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string Flow(string steps) => "{\"format\":\"keymouse-flow\",\"version\":1,\"steps\":" + steps + "}";
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
JsonElement[] Events(string text) => text.Split('\n').Where(l => l.StartsWith(Prefix))
    .Select(l => JsonDocument.Parse(l[Prefix.Length..].Trim()).RootElement.Clone()).ToArray();
string Text(JsonElement e, string key) => e.GetProperty(key).GetString()!;
string WriteFlow(string name, string steps)
{
    string path = Path.Combine(directory, name);
    File.WriteAllText(path, Flow(steps));
    return path;
}
StringWriter? activeOutput = null;
(string Output, JsonElement[] Events, int Code) Run(string path, ExecuteFlowCommand? execute = null, params string[] options)
{
    var output = new StringWriter();
    var stdout = Console.Out;
    try
    {
        Console.SetOut(output);
        activeOutput = output;
        int code = FlowRunner.Run([path, .. options], new CommandLine.GlobalOptions(), execute ?? ((_, _) => 0));
        return (output.ToString(), Events(output.ToString()), code);
    }
    finally { Console.SetOut(stdout); }
}
void CheckLifecycle(JsonElement[] events)
{
    Check(events.Select(e => e.GetProperty("seq").GetInt64()).SequenceEqual(Enumerable.Range(1, events.Length).Select(i => (long)i)), "sequence is not monotonic");
    foreach (var group in events.GroupBy(e => Text(e, "id")))
    {
        var pair = group.ToArray();
        Check(pair.Length == 2, $"{group.Key}: expected exactly one start and one terminal event");
        Check(Text(pair[0], "status") == "current" && Text(pair[1], "status") != "current", $"{group.Key}: terminal event missing");
        foreach (string field in new[] { "scopeId", "kind", "origin", "index", "command" })
            Check(pair[0].GetProperty(field).ToString() == pair[1].GetProperty(field).ToString(), $"{group.Key}: {field} changed");
        string scope = Text(pair[0], "scopeId");
        if (scope != "root")
        {
            var parent = events.Where(e => Text(e, "id") == scope).ToArray();
            Check(parent.Length == 2 && Text(parent[0], "kind") == "call", "child has no call parent");
            Check(parent[0].GetProperty("seq").GetInt64() < pair[0].GetProperty("seq").GetInt64() && parent[1].GetProperty("seq").GetInt64() > pair[1].GetProperty("seq").GetInt64(), "child escaped parent lifecycle");
        }
    }
}
var previous = Environment.GetEnvironmentVariable("KEYMOUSE_HUD");
try
{
    Environment.SetEnvironmentVariable("KEYMOUSE_HUD", "1");
    WriteFlow("leaf.json", "[{\"type\":\"sleep\",\"ms\":0},{\"type\":\"return\"}]");
    WriteFlow("child.json", "[{\"type\":\"repeat\",\"times\":5,\"steps\":[{\"type\":\"sleep\",\"ms\":0}]},{\"type\":\"call\",\"flow\":\"leaf.json\"}]");
    string root = WriteFlow("root.json", "[{\"type\":\"repeat\",\"times\":2,\"steps\":[{\"type\":\"call\",\"flow\":\"child.json\"}]},{\"type\":\"sleep\",\"ms\":0}]");
    var nested = Run(root);
    Check(nested.Code == 0, "nested run failed");
    CheckLifecycle(nested.Events);
    var calls = nested.Events.Where(e => Text(e, "kind") == "call").ToArray();
    Check(calls.Length == 8 && calls.Select(e => Text(e, "id")).Distinct().Count() == 4, "loop calls reused ids");
    Check(calls.Count(e => Text(e, "status") == "done") == 4, "return did not complete calls");
    Check(nested.Events.All(e => e.GetProperty("total").ValueKind == JsonValueKind.Null), "dynamic total guessed");
    foreach (var scope in nested.Events.Where(e => Text(e, "status") == "current").GroupBy(e => Text(e, "scopeId")))
        Check(scope.Select(e => e.GetProperty("index").GetInt32()).SequenceEqual(Enumerable.Range(1, scope.Count())), "scope counter leaked across invocations");
    Check(nested.Output.Contains("流程结束："), "text compatibility lost");
    Console.WriteLine($"PASS nested/loop/return/dynamic/monotonic lifecycle: {nested.Events.Length} events");

    // Observe the real stdout writer during dispatch: neither child nor parent can already be done.
    WriteFlow("live-child.json", "[{\"type\":\"key\",\"combo\":\"A\"}]");
    string live = WriteFlow("live.json", "[{\"type\":\"call\",\"flow\":\"live-child.json\"}]");
    bool dispatched = false;
    var running = Run(live, (_, _) =>
    {
        dispatched = true;
        var events = Events(activeOutput!.ToString());
        Check(events.Length == 2 && events.All(e => Text(e, "status") == "current"), "call is not visible while child is executing");
        Check(Text(events[1], "scopeId") == Text(events[0], "id"), "live child detached from call");
        return 0;
    });
    Check(dispatched && running.Code == 0, "live dispatch was not exercised");
    CheckLifecycle(running.Events);
    Console.WriteLine("PASS call and child visible during execution");

    foreach (int failure in new[] { 3, 7 })
    {
        var result = Run(live, (_, _) => failure);
        Check(result.Code == failure, "failure exit code lost");
        CheckLifecycle(result.Events);
        string expected = failure == 7 ? "cancelled" : "failed";
        Check(result.Events.Where(e => Text(e, "status") != "current").All(e => Text(e, "status") == expected && e.GetProperty("exitCode").GetInt32() == failure), $"exit {failure}: wrong child/call terminal state");
    }
    Console.WriteLine("PASS failed and cancelled command/call terminal states");

    var cancelledOutput = new StringWriter();
    var originalOutput = Console.Out;
    try
    {
        Console.SetOut(cancelledOutput);
        try
        {
            FlowRunner.Run([live], new CommandLine.GlobalOptions(), (_, _) => throw new OperationCanceledException());
            throw new Exception("cancellation exception was swallowed");
        }
        catch (OperationCanceledException) { }
    }
    finally { Console.SetOut(originalOutput); }
    var cancelled = Events(cancelledOutput.ToString());
    CheckLifecycle(cancelled);
    Check(cancelled.Where(e => Text(e, "status") != "current").All(e => Text(e, "status") == "cancelled" && e.GetProperty("exitCode").GetInt32() == 7), "cancellation exception misclassified");
    Console.WriteLine("PASS cancellation exception closes child and call");

    WriteFlow("error.json", "[{\"type\":\"assert\",\"cond\":\"1 + true == 0\"}]");
    string error = WriteFlow("error-root.json", "[{\"type\":\"call\",\"flow\":\"error.json\"}]");
    var thrown = Run(error);
    Check(thrown.Code != 0, "expression type error unexpectedly succeeded");
    CheckLifecycle(thrown.Events);
    Check(thrown.Events.Where(e => Text(e, "status") != "current").All(e => Text(e, "status") == "failed"), "thrown error left running entries");
    Console.WriteLine("PASS expression exception closes child and call");

    WriteFlow("keep.json", "[{\"type\":\"key\",\"combo\":\"A\"},{\"type\":\"sleep\",\"ms\":0}]");
    string keep = WriteFlow("keep-root.json", "[{\"type\":\"call\",\"flow\":\"keep.json\"},{\"type\":\"call\",\"flow\":\"leaf.json\"}]");
    var continued = Run(keep, (_, _) => 3, "--keep-going");
    CheckLifecycle(continued.Events);
    var ends = continued.Events.Where(e => Text(e, "scopeId") == "root" && Text(e, "status") != "current").ToArray();
    Check(continued.Code == 3 && ends.Length == 2 && Text(ends[0], "status") == "failed" && Text(ends[1], "status") == "done", "keep-going failure contaminated later call");
    Console.WriteLine("PASS keep-going failure stays scoped to invocation");

    Environment.SetEnvironmentVariable("KEYMOUSE_HUD", null);
    Check(Run(root).Events.Length == 0, "CLI without opt-in leaked protocol events");
    Console.WriteLine("PASS CLI opt-in compatibility");
    if (args.Length > 0) File.WriteAllText(Path.GetFullPath(args[0]), nested.Output);
    Console.WriteLine("PASS all real FlowRunner HUD assertions");
}
finally
{
    Environment.SetEnvironmentVariable("KEYMOUSE_HUD", previous);
    Directory.Delete(directory, true);
}
