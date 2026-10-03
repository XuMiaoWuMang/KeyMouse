using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// Loops and variables, without a desktop: a `repeat`/`foreach` is flattened into the steps it would
/// run (loop variables substituted), `{{name}}` is resolved per step at the last moment, and the
/// loader refuses a file whose placeholders cannot resolve or whose loops have no bounds.
/// </summary>
internal static class LoopTests
{
    public static void Run()
    {
        Harness.Group("loops: flattening a plan");

        var document = new FlowDocument();
        document.Lists["rows"] = ["甲", "乙", "丙"];

        var repeat = new FlowStep
        {
            Type = "repeat",
            Times = 3,
            Steps = [new FlowStep { Type = "sleep", Ms = 5, Note = "第 {{index}} 次" }],
        };
        List<FlowStep> flat = FlowDocument.ExpandLoops([repeat], document);
        Harness.Equal("a repeat of three becomes three steps", 3, flat.Count);
        Harness.Sequence("...with the index substituted each round",
            ["第 0 次", "第 1 次", "第 2 次"], flat.Select(s => s.Note ?? ""));

        var each = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "type", Text = "{{item}}" }],
        };
        List<FlowStep> items = FlowDocument.ExpandLoops([each], document);
        Harness.Sequence("a foreach walks its list in order", ["甲", "乙", "丙"], items.Select(s => s.Text ?? ""));

        var nested = new FlowStep
        {
            Type = "foreach",
            In = "rows",
            Steps = [new FlowStep { Type = "repeat", Times = 2, Steps = [new FlowStep { Type = "sleep", Ms = 1 }] }],
        };
        Harness.Equal("nested loops multiply", 6, FlowDocument.ExpandLoops([nested], document).Count);

        Harness.Equal("a loop with no bound runs nothing",
            0, FlowDocument.ExpandLoops([new FlowStep { Type = "repeat", Times = 0 }], document).Count);
        Harness.Equal("...and a foreach over an unknown list runs nothing",
            0, FlowDocument.ExpandLoops([new FlowStep { Type = "foreach", In = "nope" }], document).Count);

        Harness.Check("flattening does not touch the document",
            ((FlowStep)repeat.Steps![0]).Note == "第 {{index}} 次");

        Harness.Group("variables: substitution");

        var scope = new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "记事本" };
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
            var looped = FlowDocument.Load(path);
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
