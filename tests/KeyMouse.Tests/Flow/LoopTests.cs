using KeyMouse;
using KeyMouse.Flow;

namespace KeyMouse.Tests;

/// <summary>
/// Loops and variables, without a desktop: a `repeat`/`foreach` becomes the steps it would run,
/// each in its own round frame (so `{{index}}`/`{{item}}` resolve at the last moment), and the
/// loader refuses a file whose placeholders cannot resolve or whose loops have no bounds.
///
/// The execution assertions read the report, not the console: the report is the contract the editor
/// and the smoke tests share.
/// </summary>
internal static class LoopTests
{
    private static VariableFrame Frame(params (string Name, string Value)[] values) =>
        new(null, values.Select(v => new KeyValuePair<string, string>(v.Name, v.Value)));

    private static FlowPlan Plan(List<FlowStep> steps, FlowDocument document)
    {
        document.Steps = steps;
        return FlowPlan.Build(document, Path.Combine(Path.GetTempPath(), "keymouse-plan-test.json"));
    }

    public static void Run()
    {
        Harness.Group("loops: the plan a file produces");

        var document = new FlowDocument();
        document.Lists["rows"] = ["甲", "乙", "丙"];

        var repeat = new FlowStep
        {
            Type = "repeat",
            Times = 3,
            Steps = [new FlowStep { Type = "sleep", Ms = 5, Note = "第 {{index}} 次" }],
        };
        Harness.Equal("a repeat of three plans three steps", 3, Plan([repeat], document).PlannedSteps);

        var each = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "type", Text = "{{item}}" }],
        };
        Harness.Equal("a foreach plans one step per item", 3, Plan([each], document).PlannedSteps);

        var nested = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "repeat", Times = 2, Steps = [new FlowStep { Type = "sleep", Ms = 1 }] }],
        };
        Harness.Equal("nested loops multiply", 6, Plan([nested], document).PlannedSteps);

        Harness.Equal("a loop with no bound plans nothing",
            0, Plan([new FlowStep { Type = "repeat", Times = 0 }], document).PlannedSteps);

        Harness.Check("building a plan does not touch the document",
            ((FlowStep)repeat.Steps![0]).Note == "第 {{index}} 次");

        Harness.Group("loops: what actually runs");

        FlowRun.Result counted = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"repeat","times":3,"steps":[{"type":"set","name":"n","value":"n + 1"}]},
             {"type":"assert","cond":"n == 3"}]
            """,
            """{"n":"0"}"""));
        Harness.Equal("a repeat runs its body exactly three times", 4, counted.Report?.Succeeded);
        Harness.Equal("...and the counter survives between rounds", 0, counted.ExitCode);
        Harness.Sequence("...with the loop body's origin repeated",
            ["1.1", "1.1", "1.1", "2"],
            counted.Report?.Commands.Select(c => c.Origin ?? "") ?? []);

        FlowRun.Result walked = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"foreach","in":"rows","steps":[{"type":"set","name":"seen","value":"seen + item"}]},
             {"type":"assert","cond":"seen == \"甲乙丙\""}]
            """,
            """{"rows":["甲","乙","丙"],"seen":""}"""));
        Harness.Equal("a foreach walks its list in order", 0, walked.ExitCode);
        Harness.Equal("...four steps: three items plus the assert", 4, walked.Report?.Succeeded);

        FlowRun.Result nestedRun = FlowRun.Run(FlowRun.Flow(
            """
            [{"type":"foreach","in":"rows","steps":[
                {"type":"repeat","times":2,"steps":[{"type":"sleep","ms":0}]}]}]
            """,
            """{"rows":["a","b","c"]}"""));
        Harness.Equal("nested legacy loops execute six steps", 6, nestedRun.Report?.Succeeded);
        Harness.Equal("...and the denominator is the expanded count", 6, nestedRun.Report?.Total);

        FlowRun.Result empty = FlowRun.Run(FlowRun.Flow("""[{"type":"repeat","times":0}]"""));
        Harness.Equal("a flow that plans no steps is refused", 2, empty.ExitCode);

        Harness.Group("variables: substitution");

        VariableFrame scope = Frame(("name", "记事本"));
        Harness.Equal("a placeholder is replaced", "打开 记事本", FlowDocument.Expand("打开 {{name}}", scope));
        Harness.Equal("text without placeholders is returned as it is", "原样", FlowDocument.Expand("原样", scope));
        Harness.Throws<CommandFailure>("an unknown placeholder is refused at dispatch time",
            () => FlowDocument.Expand("{{nope}}", scope));

        FlowRun.Result cjk = FlowRun.Run(FlowRun.Flow(
            """[{"type":"type","text":"你好 {{名字}}"}]""",
            """{"名字":"世界"}"""));
        Harness.Check("变量名可以是中文，占位符照样展开",
            cjk.Dispatched.Any(argv => argv.Contains("你好 世界")),
            string.Join(" | ", cjk.Dispatched.Select(argv => string.Join(' ', argv))));

        var templated = new FlowStep
        {
            Type = "type",
            Text = "{{name}}",
            Target = new FlowTarget { Process = "{{name}}" },
            Note = "{{name}}",
        };
        FlowStep resolved = FlowDocument.Resolve(templated, scope);
        Harness.Equal("a resolved step carries the value", "记事本", resolved.Text);
        Harness.Equal("...in its target too", "记事本", resolved.Target?.Process);
        Harness.Equal("...and the step in the document is untouched", "{{name}}", templated.Text);

        Harness.Group("frames: nearer declarations win");

        var outer = Frame(("who", "外层"));
        var inner = new VariableFrame(outer, [new KeyValuePair<string, string>("who", "内层")]);
        Harness.Equal("a shadowing frame wins", "内层", inner.Lookup("who"));
        Harness.Equal("...and an unshadowed name still comes from a parent",
            "外层", new VariableFrame(inner, [new KeyValuePair<string, string>("other", "x")]).Lookup("other") is null
                ? outer.Lookup("who") : "外层");
        Harness.Equal("...and a frame can hand a value back", "改过",
            new VariableFrame(outer, [new KeyValuePair<string, string>("who", "改过")]).Lookup("who"));

        Harness.Group("variables: values are typed for expressions");

        FlowRun.Result typed = FlowRun.Run(FlowRun.Flow(
            """[{"type":"set","name":"i","value":"i + 1"},{"type":"assert","cond":"i == 1"}]""",
            """{"i":"0"}"""));
        Harness.Equal("--set text that reads as a number participates in arithmetic", 0, typed.ExitCode);

        Harness.Group("loops: what the loader refuses");

        string path = Path.Combine(Path.GetTempPath(), $"keymouse-loop-test-{Environment.ProcessId}.json");
        try
        {
            void Write(string body) =>
                File.WriteAllText(path, $$"""{"format":"keymouse-flow","version":1,{{body}}}""");

            Write(""" "steps":[{"type":"repeat","steps":[{"type":"sleep","ms":1}]}] """);
            Harness.Throws<CommandFailure>("a repeat without times is refused",
                () => FlowDocument.Load(path));

            Write(""" "steps":[{"type":"repeat","times":2}] """);
            Harness.Throws<CommandFailure>("a repeat without steps is refused",
                () => FlowDocument.Load(path));

            Write(""" "steps":[{"type":"foreach","in":"rows","steps":[{"type":"sleep","ms":1}]}] """);
            Harness.Throws<CommandFailure>("a foreach over a list the document does not have is refused",
                () => FlowDocument.Load(path));

            Write(""" "steps":[{"type":"repeat","times":2,"when":{"text":"x","region":{"space":"client","x":0,"y":0,"width":10,"height":10},"target":{"process":"p"}},"steps":[{"type":"sleep","ms":1}]}] """);
            Harness.Throws<CommandFailure>("a loop cannot carry a when (conditions live on the body)",
                () => FlowDocument.Load(path));

            Write(""" "steps":[{"type":"type","text":"{{typo}}","target":{"process":"notepad"}}] """);
            Harness.Throws<CommandFailure>("an undefined variable is refused when the file loads",
                () => FlowDocument.Load(path));

            Write(""" "variables":{"who":"notepad"},"steps":[{"type":"type","text":"{{who}}","target":{"process":"notepad"}}] """);
            Harness.Equal("...and a defined one loads", 1, FlowDocument.Load(path).Steps.Count);

            Write(""" "steps":[{"type":"repeat","times":2,"steps":[{"type":"type","text":"第 {{index}} 轮","target":{"process":"notepad"}}]}] """);
            Harness.Equal("loop variables are in scope inside their body", 1, FlowDocument.Load(path).Steps.Count);

            Write(""" "steps":[{"type":"read-text","region":{"space":"client","x":0,"y":0,"width":10,"height":10},"target":{"process":"notepad"}}] """);
            Harness.Throws<CommandFailure>("read-text without into is refused",
                () => FlowDocument.Load(path));

            Write(""" "variables":{"rows":["a","b"]},"steps":[{"type":"foreach","in":"rows","steps":[{"type":"type","text":"{{item}}","target":{"process":"notepad"}}]}] """);
            FlowDocument looped = FlowDocument.Load(path);
            Harness.Equal("a foreach over a declared list loads", "foreach", looped.Steps[0].Type);
            Harness.Sequence("...carrying its list", ["a", "b"], looped.Lists["rows"]);

            Write(""" "steps":[{"type":"sleep","ms":1,"note":"{{fromSet}}"}] """);
            Harness.Throws<CommandFailure>("a --set variable is not in scope unless it was provided",
                () => FlowDocument.Load(path));
            Harness.Equal("...and is accepted when it is",
                1, FlowDocument.Load(path, new Dictionary<string, string> { ["fromSet"] = "v" }).Steps.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
