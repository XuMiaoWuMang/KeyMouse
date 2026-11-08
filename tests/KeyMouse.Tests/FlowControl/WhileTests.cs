using KeyMouse;
using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// `while` and `do-while`: the condition is re-read every round, `break`/`continue` behave like C,
/// and every conditional loop has a stated ceiling. A flow must never hang the machine it automates.
/// </summary>
internal static class WhileTests
{
    public static void Run()
    {
        Harness.Group("while: the condition decides");

        FlowRun.Result counted = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"while","cond":"n < 3","steps":[{"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 3"}]
            """,
            """{"n":"0"}"""));
        Harness.Equal("a while runs until its condition is false", 0, counted.ExitCode);
        Harness.Equal("...three rounds plus the assert", 4, counted.Report?.Succeeded);

        FlowRun.Result skipped = FlowRun.Run(FlowRun.Flow(
            """[{"type":"while","cond":"n > 3","steps":[{"type":"set","name":"n","value":"n + 1"}]},{"type":"assert","cond":"n == 0"}]""",
            """{"n":"0"}"""));
        Harness.Equal("a false condition runs the body zero times", 0, skipped.ExitCode);

        Harness.Group("while: break and continue");

        FlowRun.Result broken = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"while","cond":"n < 10","steps":[
                {"type":"if","cond":"n == 2","steps":[{"type":"break"}]},
                {"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 2"}]
            """,
            """{"n":"0"}"""));
        Harness.Equal("break leaves the loop immediately", 0, broken.ExitCode);

        FlowRun.Result continued = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"while","cond":"n < 5","steps":[
                {"type":"set","name":"n","value":"n + 1"},
                {"type":"if","cond":"n < 3","steps":[{"type":"continue"}]},
                {"type":"set","name":"hits","value":"hits + 1"}]},
             {"type":"assert","cond":"hits == 3"}]
            """,
            """{"n":"0","hits":"0"}"""));
        Harness.Equal("continue skips the rest of this round only", 0, continued.ExitCode);

        Harness.Group("do-while: once before the condition");

        FlowRun.Result once = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"do","cond":"n < 0","steps":[{"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 1"}]
            """,
            """{"n":"0"}"""));
        Harness.Equal("do-while runs once even when the condition is already false", 0, once.ExitCode);

        FlowRun.Result doCounted = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"do","cond":"n < 3","steps":[{"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 3"}]
            """,
            """{"n":"0"}"""));
        Harness.Equal("...and keeps going while it stays true", 0, doCounted.ExitCode);

        Harness.Group("while: ceilings");

        FlowRun.Result capped = FlowRun.Run(FlowRun.Flow(
            """[{"type":"while","cond":"true","maxRounds":3,"steps":[{"type":"sleep","ms":0}]}]"""));
        Harness.Equal("a loop over its maxRounds exits 3", 3, capped.ExitCode);
        Harness.Check("...and says which loop and why", capped.Output.Contains("maxRounds"), capped.Output);
        Harness.Equal("...after exactly the allowed number of steps", 3, capped.Report?.Succeeded);

        FlowRun.Result budget = FlowRun.Run(FlowRun.Flow(
            """[{"type":"while","cond":"true","maxRounds":100,"steps":[{"type":"sleep","ms":0}]}]"""),
            "--max-steps", "2");
        Harness.Equal("--max-steps stops a nested/runaway flow", 3, budget.ExitCode);
        Harness.Check("...naming the budget", budget.Output.Contains("max-steps"), budget.Output);
        Harness.Equal("...after two executed steps", 2, budget.Report?.Succeeded);

        Harness.Group("while: dry run walks the body once");

        FlowRun.Result dry = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"while","cond":"n < 99","steps":[{"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 1"}]
            """,
            """{"n":"0"}"""),
            "--dry-run");
        Harness.Equal("--dry-run does not spin a conditional loop", 0, dry.ExitCode);

        Harness.Group("while: what the loader refuses");

        LoadThrows("break outside a loop is refused",
            """[{"type":"break"}]""", "不在循环里");
        LoadThrows("continue outside a loop is refused",
            """[{"type":"continue"}]""", "不在循环里");
        LoadThrows("a while without cond is refused",
            """[{"type":"while","steps":[{"type":"sleep","ms":0}]}]""", "cond");
        LoadThrows("a while without steps is refused",
            """[{"type":"while","cond":"true"}]""", "没有步骤");
        LoadThrows("maxRounds is bounded",
            """[{"type":"while","cond":"true","maxRounds":10001,"steps":[{"type":"sleep","ms":0}]}]""", "maxRounds");
        LoadThrows("a loop condition cannot see a name first defined inside the body",
            """[{"type":"while","cond":"fresh == 1","steps":[{"type":"set","name":"fresh","value":"1"}]}]""", "fresh");
    }

    private static void LoadThrows(string what, string steps, string contains)
    {
        string path = Path.Combine(Path.GetTempPath(), $"keymouse-while-test-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, FlowRun.Flow(steps));
        try
        {
            FlowDocument.Load(path);
            Harness.Check(what, false, "没有抛异常");
        }
        catch (CommandFailure ex)
        {
            Harness.Check(what, ex.Message.Contains(contains), ex.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
