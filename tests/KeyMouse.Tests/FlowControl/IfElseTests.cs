using KeyMouse;
using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// `if` / `else`: which branch runs, nesting (that is how `else if` is written), block scope,
/// and the errors that must happen when the file loads instead of half-way through a run.
/// </summary>
internal static class IfElseTests
{
    public static void Run()
    {
        Harness.Group("if: branches");

        FlowRun.Result then = Run(1, "then");
        Harness.Equal("a true condition runs the then branch", 0, then.ExitCode);
        Harness.Equal("...and reports the branch's step plus the assert", 2, then.Report?.Succeeded);

        FlowRun.Result otherwise = Run(0, "else");
        Harness.Equal("a false condition runs else", 0, otherwise.ExitCode);
        Harness.Check("the else branch keeps its own origin (1.e1)",
            otherwise.Report?.Commands.Any(c => c.Origin == "1.e1") == true,
            string.Join(",", otherwise.Report?.Commands.Select(c => c.Origin ?? "?") ?? []));

        FlowRun.Result noElse = FlowRun.Run(FlowRun.Flow(
            """[{"type":"if","cond":"n > 0","steps":[{"type":"set","name":"hit","value":"\"then\""}]},{"type":"assert","cond":"hit == \"\""}]""",
            """{"n":"0","hit":""}"""));
        Harness.Equal("an if without else runs nothing when false", 0, noElse.ExitCode);

        Harness.Group("if: else-if is an if in else");

        FlowRun.Result zero = Nested(0, "zero");
        Harness.Equal("n == 0 falls through to the final else", 0, zero.ExitCode);
        Harness.Equal("negative", 0, Nested(-2, "negative").ExitCode);
        Harness.Equal("positive", 0, Nested(3, "positive").ExitCode);

        Harness.Group("if: what the loader refuses");

        LoadThrows("an if without cond is refused",
            """[{"type":"if","steps":[{"type":"sleep","ms":0}]}]""", "cond");
        LoadThrows("an if without steps is refused",
            """[{"type":"if","cond":"true"}]""", "没有步骤");
        LoadThrows("an empty else is refused",
            """[{"type":"if","cond":"true","steps":[{"type":"sleep","ms":0}],"else":[]}]""", "else 里没有步骤");
        LoadThrows("an if cannot carry a when",
            """
            [{"type":"if","cond":"true","when":{"text":"x","region":{"space":"client","x":0,"y":0,"width":1,"height":1},"target":{"process":"p"}},
              "steps":[{"type":"sleep","ms":0}]}]
            """, "不能带 when");
        LoadThrows("text() needs a probe",
            """[{"type":"if","cond":"text(\"保存\")","steps":[{"type":"sleep","ms":0}]}]""", "probe");
        LoadThrows("a name introduced inside a branch is not in scope afterwards",
            """[{"type":"if","cond":"true","steps":[{"type":"set","name":"fresh","value":"1"}]},{"type":"assert","cond":"fresh == 1"}]""", "fresh");
        LoadThrows("cond uses a name that does not exist yet",
            """[{"type":"if","cond":"typo == 1","steps":[{"type":"sleep","ms":0}]}]""", "typo");

        Harness.Group("if: runtime type error is a usage error, not a silent false");

        FlowRun.Result textCond = FlowRun.Run(FlowRun.Flow(
            """[{"type":"if","cond":"n","steps":[{"type":"sleep","ms":0}]}]""",
            """{"n":"1"}"""));
        Harness.Equal("a text condition is not truthy", 2, textCond.ExitCode);
        Harness.Check("...and says what was expected",
            textCond.Output.Contains("true/false"), textCond.Output);

        Harness.Group("assert: a false assertion is exit 3 and stops");

        FlowRun.Result assertion = FlowRun.Run(FlowRun.Flow(
            """[{"type":"assert","cond":"n == 2","message":"n 不是 2"}]""",
            """{"n":"1"}"""));
        Harness.Equal("a false assertion exits 3", 3, assertion.ExitCode);
        Harness.Check("...carrying its message", assertion.Output.Contains("n 不是 2"), assertion.Output);
    }

    private static FlowRun.Result Run(int n, string expected) => FlowRun.Run(FlowRun.Flow(
        $$"""
        [{"type":"if","cond":"n > 0",
          "steps":[{"type":"set","name":"hit","value":"\"then\""}],
          "else":[{"type":"set","name":"hit","value":"\"else\""}]},
         {"type":"assert","cond":"hit == \"{{expected}}\""}]
        """,
        $$"""{"n":"{{n}}","hit":""}"""));

    private static FlowRun.Result Nested(int n, string expected)
    {
        string steps = """
            [{"type":"if","cond":"n > 0",
              "steps":[{"type":"set","name":"hit","value":"\"positive\""}],
              "else":[{"type":"if","cond":"n < 0",
                       "steps":[{"type":"set","name":"hit","value":"\"negative\""}],
                       "else":[{"type":"set","name":"hit","value":"\"zero\""}]}]},
             {"type":"assert","cond":"hit == \"EXPECTED\""}]
            """.Replace("EXPECTED", expected);
        return FlowRun.Run(FlowRun.Flow(steps, $$"""{"n":"{{n}}","hit":""}"""));
    }

    private static void LoadThrows(string what, string steps, string contains)
    {
        string path = Path.Combine(Path.GetTempPath(), $"keymouse-if-test-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, FlowRun.Flow(steps, """{"n":"1"}"""));
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
