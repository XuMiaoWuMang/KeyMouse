using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace KeyMouse.Setup;

/// <summary>
/// 极简安装程序。五条规则（都是实测踩出来的）：
/// * 编辑器自包含，装完就能用，不需要先装 .NET。
/// * **首次安装**（注册表里没有可用的 GuiPath）强制一并装命令行——没有 CLI，编辑器跑不了流程。
/// * **非首次**改为"是否更新命令行"，默认更新；仓库里两份载荷本来就该同版本。
/// * 默认安装位置 = **上次装过的地方**（注册表 GuiPath 所在目录），不再一律塞进 LocalAppData。
/// * 安装**完全替换**旧文件：目标目录如果是我们的，先整体清空，避免旧版本文件残留。
/// 卸载由安装目录里的 uninstall.exe 完成（本程序自身复制过去，用 --uninstall 或改名都认）。
/// </summary>
internal static class Program
{
    internal const string ProductName = "KeyMouse 流程编辑器";

    /// <summary>安装程序版本 = 本次安装的产品版本，取自程序集（根 Directory.Build.props）。</summary>
    internal static string Version
    {
        get
        {
            System.Version? v = typeof(Program).Assembly.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        // WinExe 默认没有控制台：脚本里跑 --silent / --print-default 时把输出接回父控制台，
        // 同时留一份日志——出错时"退出码 1 什么都不说"是最难查的形态。
        AttachConsole(-1);
        Log.Write("start " + string.Join(' ', args));

        bool silent = args.Contains("--silent");

        // 改名成 uninstall.exe 或带 --uninstall 都走卸载：一个二进制，两个身份。
        string self = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "setup");
        if (args.Contains("--uninstall") || self.Equals("uninstall", StringComparison.OrdinalIgnoreCase))
        {
            if (!silent && !args.Contains("--yes"))
            {
                DialogResult answer = MessageBox.Show(
                    "卸载 KeyMouse？\n\n会删除安装目录（含命令行）、开始菜单快捷方式，并清空注册表登记。",
                    Program.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            int removed = Installer.Remove(silent ? null : text => Console.WriteLine(text));
            if (silent) Environment.Exit(removed);
            return;
        }

        string target = ArgValue(args, "--dir") ?? Installer.DefaultDirectory;

        // 给测试与脚本用：只报告算出来的默认安装位置。
        if (args.Contains("--print-default"))
        {
            Console.WriteLine(target);
            return;
        }

        bool updateCli = !args.Contains("--no-cli");
        if (silent)
        {
            Environment.Exit(Installer.Run(target, silent: true, progress: null, updateCli));
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm(target));
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
