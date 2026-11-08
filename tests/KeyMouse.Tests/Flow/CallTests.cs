using KeyMouse;
using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// Subflows, without a desktop: a `call` runs its steps in a chained frame, the call's `vars` win
/// over the subflow's own defaults, and only what the subflow explicitly exports lands back in the
/// caller. The behavioral assertions use `set` instead of `read-text` so no window is needed -
/// the frame rules are the same either way.
/// </summary>
internal static class CallTests
{
    public static void Run()
    {
        Harness.Group("call: scope, vars and export");

        string directory = Path.Combine(Path.GetTempPath(), $"keymouse-call-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string Sub(string steps, string variables = """{"greeting":"你好"}""") =>
                $$"""{"format":"keymouse-flow","version":1,"variables":{{variables}},"steps":{{steps}}}""";

            File.WriteAllText(Path.Combine(directory, "sub.json"), Sub(
                """
                [{"type":"set","name":"seen","value":"\"屏幕上的字\""},
                 {"type":"set","name":"composed","value":"greeting + \"-\" + who"}]
                """));

            string Main(string steps, string variables = """{"who":"调用者"}""") =>
                $$"""{"format":"keymouse-flow","version":1,"variables":{{variables}},"steps":{{steps}}}""";

            string mainPath = Path.Combine(directory, "main.json");
            File.WriteAllText(mainPath, Main(
                """
                [{"type":"call","flow":"sub.json","vars":{"who":"{{who}}-改成"},"export":["seen","composed"]},
                 {"type":"assert","cond":"seen == \"屏幕上的字\""},
                 {"type":"assert","cond":"composed == \"你好-调用者-改成\""},
                 {"type":"assert","cond":"who == \"调用者\""}]
                """));

            FlowDocument loaded = FlowDocument.Load(mainPath);
            FlowPlan plan = FlowPlan.Build(loaded, mainPath);
            Harness.Equal("a call plans the subflow's steps plus the caller's", 5, plan.PlannedSteps);

            FlowRun.Result run = FlowRun.RunPath(mainPath);
            Harness.Equal("the subflow runs in the caller's execution", 0, run.ExitCode);
            Harness.Equal("...five executed steps", 5, run.Report?.Succeeded);
            Harness.Sequence("...subflow rows keep the call's origin",
                ["1.1", "1.2", "2", "3", "4"],
                run.Report?.Commands.Select(c => c.Origin ?? "") ?? []);

            Harness.Group("call: what stays inside and what is handed back");

            File.WriteAllText(Path.Combine(directory, "hidden-sub.json"), Sub(
                """[{"type":"set","name":"hidden","value":"\"只在里面\""}]"""));

            string hiddenPath = Path.Combine(directory, "hidden-main.json");
            File.WriteAllText(hiddenPath, Main(
                """
                [{"type":"call","flow":"hidden-sub.json"},
                 {"type":"assert","cond":"hidden == \"只在里面\""}]
                """));
            Harness.Throws<CommandFailure>("a name the subflow did not export is not in scope",
                () => FlowDocument.Load(hiddenPath));

            File.WriteAllText(hiddenPath, Main(
                """
                [{"type":"call","flow":"hidden-sub.json","export":["hidden"]},
                 {"type":"assert","cond":"hidden == \"只在里面\""}]
                """));
            FlowRun.Result exported = FlowRun.RunPath(hiddenPath);
            Harness.Equal("...and it is once the call exports it", 0, exported.ExitCode);

            Harness.Group("call: nesting");

            File.WriteAllText(Path.Combine(directory, "outer.json"), """
                {"format":"keymouse-flow","version":1,"variables":{"depth":"外层"},
                 "steps":[{"type":"call","flow":"inner.json","vars":{"extra":"{{depth}}+额外"},"export":["composed"]},
                          {"type":"assert","cond":"composed == \"外层/外层+额外\""}]}
                """);
            File.WriteAllText(Path.Combine(directory, "inner.json"), """
                {"format":"keymouse-flow","version":1,
                 "steps":[{"type":"set","name":"composed","value":"depth + \"/\" + extra"}]}
                """);
            FlowRun.Result nested = FlowRun.RunPath(Path.Combine(directory, "outer.json"));
            Harness.Equal("nested calls keep the outer variables visible", 0, nested.ExitCode);
            Harness.Equal("...and plan to two steps", 2, nested.Report?.Succeeded);

            Harness.Group("call: what the loader refuses");

            string broken = Path.Combine(directory, "broken.json");
            File.WriteAllText(broken, """{"format":"keymouse-flow","version":1,"steps":[{"type":"call"}]}""");
            Harness.Throws<CommandFailure>("a call without flow is refused",
                () => FlowDocument.Load(broken));

            File.WriteAllText(broken, """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"nope.json"}]}""");
            Harness.Throws<CommandFailure>("...and one pointing at a file that is not there",
                () => FlowDocument.Load(broken));

            File.WriteAllText(Path.Combine(directory, "a.json"),
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"b.json"}]}""");
            File.WriteAllText(Path.Combine(directory, "b.json"),
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"a.json"}]}""");
            FlowDocument cycle = FlowDocument.Load(Path.Combine(directory, "a.json"));
            Harness.Throws<CommandFailure>("a cycle is caught when the plan is built",
                () => FlowPlan.Build(cycle, Path.Combine(directory, "a.json")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
