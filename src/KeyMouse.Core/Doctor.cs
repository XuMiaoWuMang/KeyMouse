using System.Diagnostics;
using System.Security.Principal;

namespace KeyMouse;

/// <summary>
/// `KeyMouse doctor`：把"这台机器上装成什么样了"一次说清。
///
/// 为什么值得有一条专门的命令：开发树与安装版的目录形态不同，而症状长得很像别的问题——
/// 编辑器播不动、命令行说找不到编辑器、装完还是旧版本。这些都不是功能坏了，是找错了文件。
/// 这里把**判定依据**逐条打出来，而不是只给一句结论。
/// </summary>
internal static class Doctor
{
    internal static int Run(string[] args)
    {
        ConsoleText.ConfigureOutputEncoding();

        foreach (string line in ProductLayout.Describe()) Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine($"版本：{Commands.ProductVersion}（引擎 {Commands.Version}）");
        Console.WriteLine($"权限：{(IsElevated() ? "管理员（UIPI 拦不到我们，能操作提权窗口）" : "普通用户（提权窗口会拦下输入）")}");
        Console.WriteLine($"编码：输出用 {Console.OutputEncoding.WebName}（跟随控制台码页；重定向时按 UTF-8）");
        Console.WriteLine($"注册表：HKCU\\{ProductRegistry.KeyPath} " +
            $"GuiPath={Show(ProductRegistry.Read(ProductRegistry.GuiValue))} " +
            $"CliPath={Show(ProductRegistry.Read(ProductRegistry.CliValue))}");

        Console.WriteLine();
        Console.WriteLine("检查：");
        bool ok = true;
        ok &= Report("命令行可用", ProductLayout.CliPath is not null, "没有找到 KeyMouse.exe");
        ok &= Report("编辑器可用", ProductLayout.EditorPath is not null,
            "没有找到 KeyMouse.FlowEditor.exe（只想用命令行的话，这一条可以不管）", fatal: false);
        ok &= Report("流程 schema 打得开", SchemaReadable(), "内置 flow.schema.json 读不出来，安装包可能不完整");

        return ok ? 0 : 1;
    }

    private static bool Report(string what, bool ok, string detail, bool fatal = true)
    {
        if (ok)
        {
            Console.WriteLine($"  ok   {what}");
            return true;
        }

        Console.WriteLine($"  {(fatal ? "FAIL" : "--  ")} {what}：{detail}");
        return false;
    }

    private static bool SchemaReadable()
    {
        try
        {
            return FlowSchema.Steps.Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Show(string? value) => string.IsNullOrEmpty(value) ? "（无）" : value;

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
