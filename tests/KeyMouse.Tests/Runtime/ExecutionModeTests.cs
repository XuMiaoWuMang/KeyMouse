using System.Text.Json;

namespace KeyMouse.Tests.Runtime;

internal static class ExecutionModeTests
{
    internal static void Run()
    {
        string directory = Directory.CreateTempSubdirectory("keymouse-mode-").FullName;
        try
        {
            string script = Path.Combine(directory, "input.txt"), flow = Path.Combine(directory, "input.json");
            File.WriteAllText(script, "key press a");
            File.WriteAllText(flow, """{"format":"keymouse-flow","version":1,"steps":[{"type":"key","combo":"a"}]}""");
            foreach (string path in new[] { script, flow })
            {
                bool json = path == flow;
                using var overlap = new Barrier(2);
                (int Exit, ScriptReport? Report, bool ModeOk) RunOne(bool dry)
                {
                    // The live dispatcher is a stub. Never send input to the user's desktop in a test.
                    using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
                    string report = Path.Combine(directory, $"{dry}.json");
                    string[] args = dry ? [path, "--dry-run", "--report", report] : [path, "--report", report];
                    bool modeOk = false;
                    int Dispatch(string[] _)
                    {
                        if (!overlap.SignalAndWait(TimeSpan.FromSeconds(5))) return 1;
                        modeOk = ExecutionMode.IsRunning && ExecutionMode.DryRun == dry;
                        if (!modeOk) return 1;
                        CommandOutput.Out.Write(dry ? "dry-only" : "live-only");
                        if (dry) NativeInput.MoveBy(0, 0); // suppressed at Send
                        else ExecutionMode.NoteInjected(3);
                        return 0;
                    }
                    int code = json ? FlowRunner.Run(args, new(), (_, _) => Dispatch([])) : ScriptRunner.Run(args, new(), Dispatch);
                    return (code, File.Exists(report)
                        ? JsonSerializer.Deserialize<ScriptReport>(File.ReadAllText(report), FlowDocument.Json) : null,
                        modeOk && !ExecutionMode.IsRunning);
                }
                var dry = Task.Run(() => RunOne(true));
                var live = Task.Run(() => RunOne(false));
                Task.WaitAll(dry, live);
                var d = dry.Result; var l = live.Result;
                Harness.Check($"{Path.GetExtension(path)} simultaneous runs see only their own mode and leave the scope", d.Exit == 0 && l.Exit == 0 && d.ModeOk && l.ModeOk);
                Harness.Check("dry counters and output are per run", d.Report is { InjectedEvents: 0, SuppressedEvents: 1 } && d.Report.Commands.Single().Output == "dry-only");
                Harness.Check("live counters and output are per run", l.Report is { InjectedEvents: 3, SuppressedEvents: 0 } && l.Report.Commands.Single() is { InjectedEvents: 3, Output: "live-only" });
            }

            using var silent = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
            string realReport = Path.Combine(directory, "dispatch.json");
            // Guard the real dispatcher at the test boundary too: a policy regression fails the test
            // without ever letting a live key reach Windows.
            int real = FlowRunner.Run([flow, "--dry-run", "--report", realReport], new(),
                (step, restore) => ExecutionMode.DryRun ? FlowCommandExecutor.Execute(step, restore) : 1);
            var result = JsonSerializer.Deserialize<ScriptReport>(File.ReadAllText(realReport), FlowDocument.Json);
            Harness.Check("JSON dry-run suppresses the real key dispatcher", real == 0 && result is { InjectedEvents: 0, SuppressedEvents: 2 });
            Harness.Throws<InvalidOperationException>("a thrown step unwinds the execution scope", () =>
                FlowRunner.Run([flow, "--dry-run"], new(), (_, _) => throw new InvalidOperationException("test")));
            Harness.Check("exception did not leave the caller in dry-run", !ExecutionMode.IsRunning && !ExecutionMode.DryRun);
            using var guard = ExecutionMode.Begin(true);
            Harness.Equal("nested text run refused", 2, ScriptRunner.Run([script], new(), _ => 0));
            Harness.Equal("nested JSON run refused", 2, FlowRunner.Run([flow], new(), (_, _) => 0));
            NativeInput.MoveTo(0, 0);
            Harness.Equal("dry absolute move suppressed once, without retries", 1L, ExecutionMode.SuppressedEvents);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
