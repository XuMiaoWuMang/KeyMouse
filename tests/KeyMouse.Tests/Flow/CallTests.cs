using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// Subflows, without a desktop: a `call` is inlined into one flat plan, each step carrying the frame
/// it runs in. The tests here pin the three things that make subflows reusable rather than surprising:
/// the caller's values stay the caller's, the call's `vars` win over the subflow's own defaults, and
/// only what the subflow explicitly exports lands back in the caller.
/// </summary>
internal static class CallTests
{
    public static void Run()
    {
        Harness.Group("call: inlining and scope");

        string directory = Path.Combine(Path.GetTempPath(), $"keymouse-call-{Environment.ProcessId}");
        Directory.CreateDirectory(directory);
        try
        {
            // 子流程：自己的默认值 + 一个由调用者覆盖的值 + 一次捕获
            File.WriteAllText(Path.Combine(directory, "sub.json"), """
                {"format":"keymouse-flow","version":1,
                 "variables":{"greeting":"你好"},
                 "steps":[
                   {"type":"type","text":"{{greeting}}-{{who}}","target":{"process":"p"}},
                   {"type":"read-text","target":{"process":"p"},
                    "region":{"space":"client","x":0,"y":0,"width":10,"height":10},"into":"seen"}]}
                """);

            FlowDocument Main(string body)
            {
                string path = Path.Combine(directory, "main.json");
                File.WriteAllText(path, $$"""{"format":"keymouse-flow","version":1,"variables":{"who":"调用者"},{{body}}}""");
                return FlowDocument.Load(path);
            }

            FlowDocument main = Main(""" "steps":[{"type":"call","flow":"sub.json"}] """);
            FlowPlan plan = FlowPlan.Build(main, Path.Combine(directory, "main.json"));
            Harness.Equal("a call inlines the subflow's steps", 2, plan.StepCount);
            Harness.Equal("...and leaves nothing else behind without an export", 2, plan.Items.Count);

            FlowStep first = FlowDocument.Resolve(plan.Items[0].Step!, plan.Items[0].Frame);
            Harness.Equal("the subflow's own variables are in scope inside it", "你好-调用者", first.Text);

            // vars 覆盖子流程默认值，并且能引用调用者的变量
            FlowDocument overridden = Main(""" "steps":[{"type":"call","flow":"sub.json","vars":{"who":"{{who}}-改成"}}] """);
            FlowPlan overriddenPlan = FlowPlan.Build(overridden, Path.Combine(directory, "main.json"));
            FlowStep overriddenStep = FlowDocument.Resolve(overriddenPlan.Items[0].Step!, overriddenPlan.Items[0].Frame);
            Harness.Equal("vars win over the subflow and may use the caller's values",
                "你好-调用者-改成", overriddenStep.Text);

            Harness.Group("call: what stays inside and what is handed back");

            FlowDocument hidden = Main(""" "steps":[{"type":"call","flow":"sub.json"}] """);
            FlowPlan hiddenPlan = FlowPlan.Build(hidden, Path.Combine(directory, "main.json"));
            Harness.Check("a capture inside the subflow does not leak into the caller",
                hiddenPlan.Items[0].Frame == hiddenPlan.Items[1].Frame &&
                hiddenPlan.Items[1].Frame.Lookup("seen") is null);

            FlowDocument exported = Main(""" "steps":[{"type":"call","flow":"sub.json","export":["seen"]}] """);
            FlowPlan exportedPlan = FlowPlan.Build(exported, Path.Combine(directory, "main.json"));
            Harness.Equal("an export adds one step-less item", 3, exportedPlan.Items.Count);
            FlowPlan.Item exportItem = exportedPlan.Items[2];
            Harness.Check("...which is a bookkeeping item, not a step", exportItem.Step is null);
            Harness.Equal("...naming what to hand back", "seen", exportItem.Export?[0]);
            Harness.Check("...reading from the subflow's frame and writing into the caller's",
                exportItem.ExportFrom is not null && !ReferenceEquals(exportItem.ExportFrom, exportItem.Frame));

            // 真的跑一遍导出逻辑：没有桌面也能验证"值回到调用者"
            exportItem.ExportFrom!.Set("seen", "屏幕上的字");
            Harness.Check("the caller does not have it before the export item runs",
                exportItem.Frame.Lookup("seen") is null);
            foreach (string name in exportItem.Export!)
            {
                if (exportItem.ExportFrom.Lookup(name) is { } value) exportItem.Frame.Set(name, value);
            }
            Harness.Equal("...and has it afterwards", "屏幕上的字", exportItem.Frame.Lookup("seen"));

            Harness.Group("call: what the loader refuses");

            string missing = Path.Combine(directory, "bare.json");
            File.WriteAllText(missing, """{"format":"keymouse-flow","version":1,"steps":[{"type":"call"}]}""");
            Harness.Throws<CommandFailure>("a call without flow is refused",
                () => FlowDocument.Load(missing));

            File.WriteAllText(missing, """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"nope.json"}]}""");
            Harness.Throws<CommandFailure>("...and one pointing at a file that is not there",
                () => FlowDocument.Load(missing));

            // 环：a -> b -> a
            File.WriteAllText(Path.Combine(directory, "a.json"),
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"b.json"}]}""");
            File.WriteAllText(Path.Combine(directory, "b.json"),
                """{"format":"keymouse-flow","version":1,"steps":[{"type":"call","flow":"a.json"}]}""");
            FlowDocument cycle = FlowDocument.Load(Path.Combine(directory, "a.json"));
            Harness.Throws<CommandFailure>("a cycle is caught",
                () => FlowPlan.Build(cycle, Path.Combine(directory, "a.json")));

            // 嵌套：子流程里再调一层，作用域层层可见
            File.WriteAllText(Path.Combine(directory, "outer.json"), """
                {"format":"keymouse-flow","version":1,"variables":{"depth":"外层"},
                 "steps":[{"type":"call","flow":"inner.json","vars":{"extra":"{{depth}}+额外"}}]}
                """);
            File.WriteAllText(Path.Combine(directory, "inner.json"), """
                {"format":"keymouse-flow","version":1,
                 "steps":[{"type":"type","text":"{{depth}}/{{extra}}","target":{"process":"p"}}]}
                """);
            FlowDocument nested = FlowDocument.Load(Path.Combine(directory, "outer.json"));
            FlowPlan nestedPlan = FlowPlan.Build(nested, Path.Combine(directory, "outer.json"));
            Harness.Equal("nested calls inline to one step", 1, nestedPlan.StepCount);
            Harness.Equal("...where the outer variables are still visible",
                "外层/外层+额外",
                FlowDocument.Resolve(nestedPlan.Items[0].Step!, nestedPlan.Items[0].Frame).Text);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
