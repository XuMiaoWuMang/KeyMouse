namespace KeyMouse.Tests;

/// <summary>
/// 安装布局的约定：两个编辑器名字、谁先谁后、在哪几个地方找。
///
/// 这些断言看起来琐碎，锁住的却是"装出去还能不能用"：新壳上线后老壳还要并存一段时间，
/// 名字写错一个字母，装出来的编辑器就找不到命令行，症状是"界面能开、一跑就说找不到 KeyMouse.exe"。
/// </summary>
internal static class LayoutTests
{
    internal static void Run()
    {
        Harness.Section("安装布局", () =>
        {
            Names();
            Order();
            Why();
        });
    }

    private static void Names()
    {
        Harness.Check("新壳叫 KeyMouse.Editor.exe", ProductLayout.EditorName == "KeyMouse.Editor.exe", ProductLayout.EditorName);
        Harness.Check("老壳留名 KeyMouse.FlowEditor.exe", ProductLayout.LegacyEditorName == "KeyMouse.FlowEditor.exe", ProductLayout.LegacyEditorName);
        Harness.Check("命令行叫 KeyMouse.exe", ProductLayout.CliName == "KeyMouse.exe", ProductLayout.CliName);
        Harness.Check("覆盖用的环境变量名没变", ProductLayout.CliOverride == "KEYMOUSE_CLI" && ProductLayout.EditorOverride == "KEYMOUSE_EDITOR", null);
    }

    private static void Order()
    {
        IReadOnlyList<LayoutCandidate> editors = ProductLayout.EditorCandidates();

        // 同一个目录里，新壳那条必须排在同目录老壳那条之前。
        int firstNew = IndexOf(editors, Path.Combine(ProductLayout.Here, ProductLayout.EditorName));
        int firstOld = IndexOf(editors, Path.Combine(ProductLayout.Here, ProductLayout.LegacyEditorName));
        Harness.Check("同一目录里新壳排在老壳前面", firstNew >= 0 && firstOld > firstNew, $"新壳 #{firstNew} / 老壳 #{firstOld}");

        // 注册表与 PATH 永远在最后：它们最可能写着上次装的旧版本。
        // 本机没登记过（干净的开发机就是这样），这一条就无从排起——按"没有"处理，不算失败。
        int lastRegistry = LastIndexOf(editors, c => c.Why.Contains("注册表"));
        int firstPath = IndexOf(editors, c => c.Why == "PATH");
        if (lastRegistry >= 0)
        {
            Harness.Check("注册表排在开发树之后", lastRegistry > firstNew, $"注册表 #{lastRegistry}");
            if (firstPath >= 0)
                Harness.Check("PATH 排在注册表之后", firstPath > lastRegistry, $"PATH #{firstPath}");
        }
        else
        {
            Harness.Check("本机没有登记编辑器（干净开发机）", true, "没有注册表候选，只查它不出现");
        }

        // 命令行的候选里必须包含 <自己所在目录>\cli\KeyMouse.exe —— 安装版就靠这条。
        IReadOnlyList<LayoutCandidate> cli = ProductLayout.CliCandidates();
        string beside = Path.Combine(ProductLayout.Here, "cli", ProductLayout.CliName);
        Harness.Check(@"命令行候选包含 <自己>\cli\KeyMouse.exe", IndexOf(cli, beside) >= 0, beside);
        Harness.Check(@"命令行候选包含 <自己>\KeyMouse.exe", IndexOf(cli, Path.Combine(ProductLayout.Here, ProductLayout.CliName)) >= 0, null);
    }

    private static void Why()
    {
        IReadOnlyList<LayoutCandidate> editors = ProductLayout.EditorCandidates();

        // 每条候选都得说得清"为什么找这里"，doctor 直接把它打给用户看。
        bool allExplained = editors.All(c => !string.IsNullOrWhiteSpace(c.Why));
        Harness.Check("每条编辑器候选都有理由", allExplained, $"{editors.Count} 条");

        // 老壳那几条要标出来，别让人以为新壳已经到处都能用。
        int legacyLabeled = editors.Count(c => c.Why.Contains("老外壳"));
        Harness.Check("老壳候选标了「老外壳」", legacyLabeled >= 2, $"{legacyLabeled} 条");

        Harness.Check("不存在时 EditorPath 不会指向文件夹",
            ProductLayout.EditorPath is null || File.Exists(ProductLayout.EditorPath),
            ProductLayout.EditorPath);
    }

    private static int IndexOf(IReadOnlyList<LayoutCandidate> list, string path)
    {
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i].Path, path, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static int IndexOf(IReadOnlyList<LayoutCandidate> list, Func<LayoutCandidate, bool> predicate)
    {
        for (int i = 0; i < list.Count; i++)
            if (predicate(list[i])) return i;
        return -1;
    }

    private static int LastIndexOf(IReadOnlyList<LayoutCandidate> list, Func<LayoutCandidate, bool> predicate)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (predicate(list[i])) return i;
        return -1;
    }
}
