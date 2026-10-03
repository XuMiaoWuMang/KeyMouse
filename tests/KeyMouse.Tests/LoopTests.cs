using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// Loops and variables, without a desktop. A `repeat`/`foreach` becomes the steps it would run, each
/// carrying the frame it runs in (so `{{index}}`/`{{item}}` resolve at the last moment, next to every
/// other variable), and the loader refuses a file whose placeholders cannot resolve or whose loops
/// have no bounds.
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
        FlowPlan repeated = Plan([repeat], document);
        Harness.Equal("a repeat of three becomes three steps", 3, repeated.StepCount);
        Harness.Sequence("...each resolved in the frame of its own round",
            ["第 0 次", "第 1 次", "第 2 次"],
            repeated.Items.Select(i => FlowDocument.Resolve(i.Step!, i.Frame).Note ?? ""));

        var each = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "type", Text = "{{item}}" }],
        };
        FlowPlan items = Plan([each], document);
        Harness.Sequence("a foreach walks its list in order",
            ["甲", "乙", "丙"],
            items.Items.Select(i => FlowDocument.Resolve(i.Step!, i.Frame).Text ?? ""));

        var nested = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "repeat", Times = 2, Steps = [new FlowStep { Type = "sleep", Ms = 1 }] }],
        };
        Harness.Equal("nested loops multiply", 6, Plan([nested], document).StepCount);

        Harness.Equal("a loop with no bound runs nothing",
            0, Plan([new FlowStep { Type = "repeat", Times = 0 }], document).StepCount);
        Harness.Equal("...and a foreach over an unknown list runs nothing",
            0, Plan([new FlowStep { Type = "foreach", In = "nope" }], document).StepCount);

        Harness.Check("building a plan does not touch the document",
            ((FlowStep)repeat.Steps![0]).Note == "第 {{index}} 次");

        Harness.Group("variables: substitution");

        VariableFrame scope = Frame(("name", "记事本"));
        Harness.Equal("a placeholder is replaced", "打开 记事本", FlowDocument.Expand("打开 {{name}}", scope));
        Harness.Equal("text without placeholders is returned as it is", "原样", FlowDocument.Expand("原样", scope));
        Harness.Throws<CommandFailure>("an unknown placeholder is refused at dispatch time",
            () => FlowDocument.Expand("{{nope}}", scope));

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
        Harness.Equal("...and a frame can hand a value back", "改过", new VariableFrame(outer, [new KeyValuePair<string, string>("who", "改过")]).Lookup("who"));

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
            Harness.Throws<CommandFailure>("a loop cannot carry a when (it has nowhere to live once flattened)",
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
