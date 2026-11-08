using KeyMouse;
using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// `for (init; cond; step)`: init runs once, the condition is re-read every round, the step runs
/// after each body (including after `continue`), and the init names are scoped to the loop - C's
/// `for (int i = …)`, not a way to quietly overwrite an outer variable.
/// </summary>
internal static class ForSugarTests
{
    public static void Run()
    {
        Harness.Group("for: counting");

        FlowRun.Result counted = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"i < 3","step":{"i":"i + 1"},
              "steps":[{"type":"set","name":"sum","value":"sum + i"}]},
             {"type":"assert","cond":"sum == 3"}]
            """,
            """{"sum":"0"}"""));
        Harness.Equal("init/cond/step run the body three times", 0, counted.ExitCode);

        FlowRun.Result never = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"9"},"cond":"i < 3","step":{"i":"i + 1"},
              "steps":[{"type":"set","name":"sum","value":"sum + i"}]},
             {"type":"assert","cond":"sum == 0"}]
            """,
            """{"sum":"0"}"""));
        Harness.Equal("a false condition runs the body zero times", 0, never.ExitCode);

        Harness.Group("for: continue still runs the step");

        FlowRun.Result continued = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"i < 5","step":{"i":"i + 1"},"steps":[
                {"type":"if","cond":"i == 2","steps":[{"type":"continue"}]},
                {"type":"set","name":"sum","value":"sum + i"}]},
             {"type":"assert","cond":"sum == 8"}]
            """,
            """{"sum":"0"}"""));
        Harness.Equal("continue skips the body but not i++", 0, continued.ExitCode);

        FlowRun.Result broken = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"i < 10","step":{"i":"i + 1"},"steps":[
                {"type":"if","cond":"i == 3","steps":[{"type":"break"}]},
                {"type":"set","name":"sum","value":"sum + i"}]},
             {"type":"assert","cond":"sum == 3"}]
            """,
            """{"sum":"0"}"""));
        Harness.Equal("break leaves a for loop before its step", 0, broken.ExitCode);

        Harness.Group("for: scope is the loop's");

        FlowRun.Result shadow = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"i < 2","step":{"i":"i + 1"},
              "steps":[{"type":"sleep","ms":0}]},
             {"type":"assert","cond":"i == 99"}]
            """,
            """{"i":"99"}"""));
        Harness.Equal("an outer i is not overwritten by the loop's i", 0, shadow.ExitCode);

        LoadThrows("the loop's i does not exist after the loop",
            """[{"type":"for","init":{"i":"0"},"cond":"i < 1","step":{"i":"i + 1"},"steps":[{"type":"sleep","ms":0}]},{"type":"assert","cond":"i == 0"}]""", "i");
        LoadThrows("a name first assigned in step is not in scope in the body",
            """[{"type":"for","init":{"i":"0"},"cond":"true","step":{"j":"1"},"steps":[{"type":"set","name":"sum","value":"j"}]}]""",
            "j");
        LoadThrows("a for without cond is refused",
            """[{"type":"for","init":{"i":"0"},"step":{"i":"i + 1"},"steps":[{"type":"sleep","ms":0}]}]""", "cond");
        LoadThrows("init must be an expression, not a {{placeholder}}",
            """[{"type":"for","init":{"i":"{{never}}"},"cond":"i < 1","step":{"i":"i + 1"},"steps":[{"type":"sleep","ms":0}]}]""",
            "不要用");

        Harness.Group("for: ceilings and dry-run");

        FlowRun.Result capped = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"true","maxRounds":3,"step":{"i":"i + 1"},
              "steps":[{"type":"sleep","ms":0}]}]
            """));
        Harness.Equal("a for over its maxRounds exits 3", 3, capped.ExitCode);
        Harness.Check("...and names the guard", capped.Output.Contains("maxRounds"), capped.Output);

        FlowRun.Result dry = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"for","init":{"i":"0"},"cond":"i < 3","step":{"i":"i + 1"},
              "steps":[{"type":"set","name":"sum","value":"sum + i"}]},
             {"type":"assert","cond":"sum == 0"}]
            """,
            """{"sum":"0"}"""),
            "--dry-run");
        Harness.Equal("--dry-run runs the for body (and its step) once", 0, dry.ExitCode);
    }

    private static void LoadThrows(string what, string steps, string contains)
    {
        string path = Path.Combine(Path.GetTempPath(), $"keymouse-for-test-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
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
