using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 数字字段的"能填多少、不填算多少"只有一份出处：`flow.schema.json`。
///
/// 以前引擎自己写死（`Math.Clamp(step.IntervalMs ?? 200, 20, 5000)`），契约里写的却是 `min: 100`：
/// 界面输不进 20，手写一份 JSON 却能跑 20 —— 两边对"合法流程"的定义不一样。这组用例钉两件事：
/// 范围与默认值确实是从契约里问出来的；越界在**加载时**就失败，而不是悄悄夹一下。
/// </summary>
internal static class SchemaRangeTests
{
    public static void Run()
    {
        Harness.Group("schema-range: 数字范围与默认值来自契约");

        Harness.Check("wait-text.intervalMs 是 100..5000、默认 1000",
            FlowSchema.TryGetNumberRule("wait-text", "intervalMs", out NumberRule interval)
            && interval.Min == 100 && interval.Max == 5000 && interval.Default == 1000,
            interval.ToString());

        Harness.Check("when 这种嵌套形状也问得到（timeoutMs 默认 2000）",
            FlowSchema.TryGetShapeNumberRule("when", "timeoutMs", out NumberRule whenTimeout)
            && whenTimeout.Default == 2000,
            whenTimeout.ToString());

        Harness.Check("不是数字的字段不给规则",
            !FlowSchema.TryGetNumberRule("wait-text", "text", out _)
            && !FlowSchema.TryGetShapeNumberRule("when", "match", out _));

        Harness.Check("没填时用契约默认值（Fallback 优先取 default）",
            interval.Fallback(999) == 1000 && interval.Fallback(20) == 1000);

        Harness.Check("填了越界值会被夹进范围（运行时兜底）",
            interval.Clamp(20) == 100 && interval.Clamp(99999) == 5000 && interval.Clamp(500) == 500);

        Harness.Group("schema-range: 越界在加载时就失败");

        string low = Failure(() => FlowDocument.Load(Write(
            """{"type":"wait-text","text":"保存","target":{"process":"notepad"},"region":{"space":"client","x":0,"y":0,"width":420,"height":30},"intervalMs":20}""")));
        Harness.Check("intervalMs 低于 100 会被拒", low.Contains("intervalMs", StringComparison.Ordinal) && low.Contains("100..5000", StringComparison.Ordinal), low);

        string high = Failure(() => FlowDocument.Load(Write(
            """{"type":"wait-text","text":"保存","target":{"process":"notepad"},"region":{"space":"client","x":0,"y":0,"width":420,"height":30},"intervalMs":9000}""")));
        Harness.Check("高于 5000 也会被拒", high.Contains("100..5000", StringComparison.Ordinal), high);

        string when = Failure(() => FlowDocument.Load(Write(
            """{"type":"click","at":{"space":"client","x":1,"y":2},"target":{"process":"notepad"},"when":{"text":"完成","region":{"space":"client","x":0,"y":0,"width":10,"height":10},"intervalMs":10}}""")));
        Harness.Check("when 里的 intervalMs 一样管", when.Contains("when", StringComparison.Ordinal) && when.Contains("100..5000", StringComparison.Ordinal), when);

        Harness.Check("边界值本身是合法的（100 与 5000 都放行）",
            Accepted("""{"type":"wait-text","text":"保存","target":{"process":"notepad"},"region":{"space":"client","x":0,"y":0,"width":420,"height":30},"intervalMs":100}""")
            && Accepted("""{"type":"wait-text","text":"保存","target":{"process":"notepad"},"region":{"space":"client","x":0,"y":0,"width":420,"height":30},"intervalMs":5000}"""));

        Harness.Check("不写这个字段当然也合法（默认值只在运行时用）",
            Accepted("""{"type":"wait-text","text":"保存","target":{"process":"notepad"},"region":{"space":"client","x":0,"y":0,"width":420,"height":30}}"""));
    }

    private static bool Accepted(string step)
    {
        string path = Write(step);
        try
        {
            FlowDocument.Load(path);
            return true;
        }
        catch (CommandFailure)
        {
            return false;
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string Failure(Action action)
    {
        try
        {
            action();
            return "（没有失败）";
        }
        catch (CommandFailure ex)
        {
            return ex.Message;
        }
    }

    /// <summary>把一步包成一份最小流程写成临时文件；加载出错也是"一步就错"，与真实文件一致。</summary>
    private static string Write(string step)
    {
        string path = Path.Combine(Path.GetTempPath(), $"keymouse-range-{Environment.ProcessId}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, $$"""
        {"version":1,"variables":{},"steps":[{{step}}]}
        """);
        LastFile = path;
        return path;
    }

    [ThreadStatic]
    private static string? LastFile;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // 临时文件删不掉不影响结论
        }
    }
}
