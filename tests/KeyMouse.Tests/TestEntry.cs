namespace KeyMouse.Tests;

/// <summary>
/// Entry point. Named TestEntry rather than Program so that inside this namespace the name
/// "Program" still resolves to the CLI's Program, which the runner tests call into.
///
///     dotnet run --project tests\KeyMouse.Tests -- --list          # 有哪些模块
///     dotnet run --project tests\KeyMouse.Tests -- --only loops    # 只跑改到的模块（平时这样跑）
///     dotnet run --project tests\KeyMouse.Tests                    # 全部（功能定稿后再跑）
/// </summary>
internal static class TestEntry
{
    private static readonly (string Name, string What, Action Run)[] Sections =
    [
        ("parsing", "命令行解析、脚本语法与循环", ParsingTests.Run),
        ("windows", "窗口选择、候选偏好与资格判定", WindowTests.Run),
        ("probe", "读屏共识、引擎调用与预处理", ProbeTests.Run),
        ("locator", "文字定位：框、并集与容错", LocatorTests.Run),
        ("region", "选区坐标换算与描述", RegionTests.Run),
        ("flow", "流程编译、条件与前置条件", FlowTests.Run),
        ("loops", "循环、变量与展平", LoopTests.Run),
        ("calls", "子流程：内联、作用域与导出", CallTests.Run),
        ("runner", "常驻 Runner 协议与作业控制", RunnerTests.Run),
        ("protocol", "前后端通信契约：方法、事件、参数与终结规则", RunnerProtocolTests.Run),
    ];

    private static int Main(string[] args)
    {
        ConsoleText.ConfigureOutputEncoding();

        if (args.Length > 0 && args[0] == "bench")
        {
            ParseBench.Run();
            return 0;
        }

        if (args.Contains("--list"))
        {
            Console.WriteLine("单元测试模块（都不需要桌面）：");
            foreach (var (name, what, _) in Sections) Console.WriteLine($"  {name,-9} {what}");
            Console.WriteLine();
            Console.WriteLine("  --only a,b   只跑指定模块");
            Console.WriteLine("  不给参数     跑全部（= 总测试的一部分，发布前跑这个）");
            return 0;
        }

        var wanted = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--only" or "-o")
            {
                if (i + 1 >= args.Length) return Fail("--only 后面要跟模块名（逗号分隔）");
                wanted.AddRange(Split(args[++i]));
            }
            else if (arg.StartsWith("--only=", StringComparison.Ordinal))
            {
                wanted.AddRange(Split(arg["--only=".Length..]));
            }
            else
            {
                return Fail($"不认识的参数 '{arg}'（可用：--list | --only 模块名[,模块名]）");
            }
        }

        List<string> unknown = wanted
            .Where(w => !Sections.Any(s => s.Name.Equals(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unknown.Count > 0) return Fail($"不认识的模块：{string.Join(", ", unknown)}（--list 看有哪些）");

        var selected = wanted.Count == 0
            ? Sections.ToList()
            : Sections.Where(s => wanted.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (selected.Count == 0) return Fail("没有选中任何模块");

        Console.WriteLine(selected.Count == Sections.Length
            ? $"KeyMouse tests（不需要桌面）：全部 {Sections.Length} 个模块"
            : $"KeyMouse tests（不需要桌面）：{string.Join("、", selected.Select(s => s.Name))}（共 {Sections.Length} 个模块）");

        foreach (var (name, _, run) in selected) Harness.Section(name, run);
        return Harness.Summary();
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Fail(string message)
    {
        Console.WriteLine(message);
        return 2;
    }
}
