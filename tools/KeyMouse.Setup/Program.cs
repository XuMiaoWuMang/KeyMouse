using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace KeyMouse.Setup;

/// <summary>
/// 极简安装程序：解开两个载荷，顺手把"这台机器上有没有命令行"这件事判断掉。
///
/// 三条决定：
/// * 编辑器是自包含的（WinUI 自带运行时），所以装完就能用，不需要先装 .NET。
/// * 命令行只在**没找到**的时候才装进 `cli\`——已经装了的人不该被覆盖成旧版本。
/// * 不写注册表、不装服务、不做"组件选择"。卸载脚本就放在安装目录里，删掉目录就等于卸干净。
/// </summary>
internal static class Program
{
    internal const string ProductName = "KeyMouse 流程编辑器";
    internal const string Version = "0.1.0";

    [STAThread]
    private static void Main(string[] args)
    {
        // --silent --dir <路径>：给脚本和自动化用（CI 里验安装结果）。
        string? dir = ArgValue(args, "--dir");
        bool wantCli = !args.Contains("--no-cli");
        if (args.Contains("--silent"))
        {
            int code = Installer.Run(dir ?? Installer.DefaultDirectory, silent: true, progress: null, installCli: wantCli);
            Environment.Exit(code);
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm(dir ?? Installer.DefaultDirectory));
    }

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}

internal static class Installer
{
    /// <summary>默认装在用户目录下：不需要管理员权限，卸载就是删目录。</summary>
    internal static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "KeyMouse");

    internal static string Clip(string? text) => text ?? "";

    /// <summary>
    /// 这台机器上已有的命令行，按可信度排：注册表登记 → 安装目录的 cli → PATH。
    /// 注册表排第一是因为它记的是"上次装到哪儿"，与本次选的目标目录无关。
    /// </summary>
    internal static string? FindExistingCli(string installDirectory)
    {
        // 只认**确实还在**的那一个：注册表里可能是几天前装到临时目录、早就被清掉的路径。
        // 以前不检查存在性，于是"已检测到命令行，跳过安装"把新 CLI 一起跳过了（实测踩到）。
        if (KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.CliValue) is { } registered && File.Exists(registered))
            return registered;

        string local = Path.Combine(installDirectory, "cli", "KeyMouse.exe");
        if (File.Exists(local)) return local;

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null) return null;
        foreach (string folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(folder.Trim(), "KeyMouse.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // PATH 里有一条畸形路径不值得让安装失败
            }
        }
        return null;
    }

    internal static int Run(string directory, bool silent, Action<string>? progress, bool installCli = true)
    {
        try
        {
            Directory.CreateDirectory(directory);
            progress?.Invoke("正在安装编辑器…");
            Extract("flowedit.zip", directory, progress);

            // 先登记界面：只装 GUI 的时候，注册表里就只有这一条。
            string gui = Path.Combine(directory, "KeyMouse.FlowEditor.exe");
            KeyMouse.ProductRegistry.Write(KeyMouse.ProductRegistry.GuiValue, gui);

            if (FindExistingCli(directory) is { } existing)
            {
                progress?.Invoke("已检测到命令行，跳过安装：" + existing);
                // 只在这次真的要装命令行时才写 CliPath；明确"只装界面"就一个字都不碰它。
                if (installCli) KeyMouse.ProductRegistry.Write(KeyMouse.ProductRegistry.CliValue, existing);
            }
            else if (!installCli)
            {
                // 明确只要界面：只写 GuiPath。但如果注册表里留着一条**已经不存在**的 CliPath，
                // 必须清掉——否则编辑器会一直去找那个早就没了的引擎（实测：指向临时目录的旧副本）。
                if (KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.CliValue) is { Length: > 0 } stale
                    && !File.Exists(stale))
                {
                    KeyMouse.ProductRegistry.Clear(KeyMouse.ProductRegistry.CliValue);
                    progress?.Invoke("清掉了失效的命令行登记：" + stale);
                }
                progress?.Invoke("只安装界面（没有登记命令行）。");
            }
            else
            {
                progress?.Invoke("未检测到命令行，一并安装到 cli…");
                string cli = Path.Combine(directory, "cli", "KeyMouse.exe");
                Extract("cli.zip", Path.Combine(directory, "cli"), progress);
                KeyMouse.ProductRegistry.Write(KeyMouse.ProductRegistry.CliValue, cli);
            }

            progress?.Invoke("正在创建快捷方式…");
            Shortcut(directory);
            Uninstall(directory);
            progress?.Invoke("完成。");
            return 0;
        }
        catch (Exception ex)
        {
            progress?.Invoke("安装失败：" + ex.Message);
            if (silent) Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Extract(string resource, string directory, Action<string>? progress)
    {
        Assembly assembly = typeof(Installer).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream("KeyMouse.Setup.Assets." + resource)
            ?? throw new InvalidOperationException("安装包缺少载荷：" + resource);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Directory.CreateDirectory(directory);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string target = Path.GetFullPath(Path.Combine(directory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("载荷里有越界路径：" + entry.FullName);

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            progress?.Invoke("解开 " + entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>开始菜单里放一个快捷方式。COM 调用失败不算安装失败——文件已经到位了。</summary>
    private static void Shortcut(string directory)
    {
        try
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string link = Path.Combine(programs, Program.ProductName + ".lnk");
            Type? shell = Type.GetTypeFromProgID("WScript.Shell");
            if (shell is null) return;

            dynamic wsh = Activator.CreateInstance(shell)!;
            dynamic shortcut = wsh.CreateShortcut(link);
            shortcut.TargetPath = Path.Combine(directory, "KeyMouse.FlowEditor.exe");
            shortcut.WorkingDirectory = directory;
            shortcut.IconLocation = Path.Combine(directory, "KeyMouse.FlowEditor.exe") + ",0";
            shortcut.Save();
        }
        catch (Exception)
        {
            // 快捷方式只是顺手的事
        }
    }

    /// <summary>卸载脚本放在安装目录里：删目录之前先把快捷方式和残留进程收掉。</summary>
    private static void Uninstall(string directory)
    {
        string script = string.Join(Environment.NewLine,
            "@echo off",
            "chcp 65001 >nul",
            "taskkill /f /im KeyMouse.FlowEditor.exe >nul 2>&1",
            "taskkill /f /im KeyMouse.exe >nul 2>&1",
            "del \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\" + Program.ProductName + ".lnk\" >nul 2>&1",
            "reg delete \"HKCU\\" + KeyMouse.ProductRegistry.KeyPath + "\" /f >nul 2>&1",
            "cd /d \"%TEMP%\"",
            "rmdir /s /q \"" + directory + "\"",
            "echo 已卸载。",
            "pause");

        File.WriteAllText(Path.Combine(directory, "卸载.cmd"), script, System.Text.Encoding.UTF8);
    }
}
