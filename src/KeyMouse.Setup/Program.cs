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

/// <summary>安装/卸载轨迹：%TEMP%\keymouse-setup.log（不可写时退到 %LOCALAPPDATA%\KeyMouse\tmp）。用户报"装不上"时先看它。</summary>
internal static class Log
{
    internal static string File => KeyMouse.Scratch.PathFor("keymouse-setup.log");

    internal static void Write(string line)
    {
        try
        {
            System.IO.File.AppendAllText(File, DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
        }
        catch (Exception)
        {
        }
    }
}

internal static class Installer
{
    private static string FallbackDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "KeyMouse");

    /// <summary>上次装到哪儿（注册表里的 GuiPath，且文件确实还在）；没装过返回 null。</summary>
    internal static string? InstalledDirectory
    {
        get
        {
            if (KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.GuiValue) is { } gui
                && File.Exists(gui))
            {
                return Path.GetDirectoryName(gui);
            }

            // 注册表被清过但文件还在的情形：认目录也算装过。
            if (File.Exists(Path.Combine(FallbackDirectory, "KeyMouse.FlowEditor.exe"))) return FallbackDirectory;
            return null;
        }
    }

    internal static bool IsFirstInstall => InstalledDirectory is null;

    /// <summary>默认安装位置：装过就回原处，没装过才用用户目录。</summary>
    internal static string DefaultDirectory => InstalledDirectory ?? FallbackDirectory;

    internal static string Clip(string? text) => text ?? "";

    /// <summary>这台机器上能用的命令行（注册表只认确实存在的文件）。</summary>
    internal static string? FindExistingCli(string installDirectory)
    {
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

    internal static int Run(string directory, bool silent, Action<string>? progress, bool updateCli)
    {
        try
        {
            bool first = IsFirstInstall;
            if (first) updateCli = true;   // 首次安装：命令行是必需件

            Directory.CreateDirectory(directory);

            progress?.Invoke("正在清理旧文件…");
            Replace(directory, progress);

            progress?.Invoke("正在安装编辑器…");
            Extract("flowedit.zip", directory, progress);

            string cli = Path.Combine(directory, "cli", "KeyMouse.exe");
            string? existing = FindExistingCli(directory);
            if (updateCli || !File.Exists(cli))
            {
                progress?.Invoke(first ? "首次安装：一并安装命令行…" : "正在更新命令行…");
                RemoveDirectory(Path.Combine(directory, "cli"));
                Extract("cli.zip", Path.Combine(directory, "cli"), progress);
                existing = cli;
            }
            else
            {
                progress?.Invoke("保留已有的命令行：" + (existing ?? cli));
            }

            string gui = Path.Combine(directory, "KeyMouse.FlowEditor.exe");
            KeyMouse.ProductRegistry.Write(KeyMouse.ProductRegistry.GuiValue, gui);
            if (existing is not null && File.Exists(existing))
                KeyMouse.ProductRegistry.Write(KeyMouse.ProductRegistry.CliValue, existing);

            progress?.Invoke("正在创建快捷方式…");
            Shortcut(directory);

            progress?.Invoke("正在放置卸载程序…");
            Uninstaller(directory);

            progress?.Invoke("完成。");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Write("install failed: " + ex);
            progress?.Invoke("安装失败：" + ex.Message);
            Console.Error.WriteLine("安装失败：" + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 完全替换：目标目录如果属于 KeyMouse（有我们的 exe，或注册表就指向这里），整体清空；
    /// 否则只删我们已知的文件名，免得把用户自己放在同一目录里的东西一起删掉。
    /// </summary>
    private static void Replace(string directory, Action<string>? progress)
    {
        if (!Directory.Exists(directory)) return;

        string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        if (full.Length <= 3)
            throw new InvalidOperationException("拒绝安装到磁盘根目录：" + directory);

        bool ours = File.Exists(Path.Combine(directory, "KeyMouse.FlowEditor.exe"))
                    || (KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.GuiValue) is { } gui
                        && Path.GetFullPath(gui).StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        if (ours)
        {
            progress?.Invoke("清空旧的安装目录…");
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                try
                {
                    if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                    else File.Delete(entry);
                }
                catch (Exception)
                {
                    // 被占用的文件（比如正在运行的编辑器）删不掉时，后面的覆盖安装会再试一次
                }
            }
            return;
        }

        foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
        {
            string name = Path.GetFileName(entry);
            bool known = name.StartsWith("KeyMouse.", StringComparison.OrdinalIgnoreCase)
                         || name.Equals("cli", StringComparison.OrdinalIgnoreCase)
                         || name.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase)
                         || name.Equals("卸载.cmd", StringComparison.OrdinalIgnoreCase);
            if (!known) continue;
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }
            catch (Exception)
            {
            }
        }
    }

    private static void RemoveDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception)
        {
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
            // 旧文件若是只读，覆盖会失败——先去掉只读；文件还不存在时不能调 SetAttributes。
            if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
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

    /// <summary>把本程序自己复制成 uninstall.exe：一个二进制，两个身份。</summary>
    private static void Uninstaller(string directory)
    {
        try
        {
            string self = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            string target = Path.Combine(directory, "uninstall.exe");
            if (self.Length > 0 && File.Exists(self) && !string.Equals(self, target, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(self, target, overwrite: true);

                // 单文件发布只有一个 exe，但**不是**单文件时（构建方式不同），改名后的副本还缺
                // 它自己的 dll / runtimeconfig / deps——不一起复制，uninstall.exe 根本起不来
                // （实测："The application to execute does not exist: KeyMouseSetup.dll"）。
                string folder = Path.GetDirectoryName(self)!;
                string stem = Path.GetFileNameWithoutExtension(self);
                foreach (string suffix in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
                {
                    string source = Path.Combine(folder, stem + suffix);
                    if (File.Exists(source)) File.Copy(source, Path.Combine(directory, "uninstall" + suffix), overwrite: true);
                }
            }

            // 旧版的批处理卸载脚本不再需要
            string legacy = Path.Combine(directory, "卸载.cmd");
            if (File.Exists(legacy)) File.Delete(legacy);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// 一键卸载：杀掉进程、删编辑器目录与命令行目录、删快捷方式、清注册表，最后把自己也删掉。
    /// 返回 0 表示该删的都删了。
    /// </summary>
    internal static int Remove(Action<string>? progress)
    {
        int problems = 0;
        string? gui = KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.GuiValue);
        string? cli = KeyMouse.ProductRegistry.Read(KeyMouse.ProductRegistry.CliValue);

        string? guiDirectory = gui is not null && Path.GetDirectoryName(gui) is { Length: > 0 } g ? g : null;
        if (guiDirectory is null && File.Exists(Path.Combine(FallbackDirectory, "KeyMouse.FlowEditor.exe")))
            guiDirectory = FallbackDirectory;

        foreach (string process in new[] { "KeyMouse.FlowEditor", "KeyMouse" })
        {
            try
            {
                foreach (Process running in Process.GetProcessesByName(process))
                {
                    try { running.Kill(entireProcessTree: true); running.WaitForExit(3000); }
                    catch (Exception) { }
                }
            }
            catch (Exception)
            {
            }
        }

        Thread.Sleep(400);   // 让文件句柄松开

        string self = Environment.ProcessPath ?? "";
        bool selfInside = guiDirectory is not null && self.Length > 0
            && Path.GetFullPath(self).StartsWith(
                Path.GetFullPath(guiDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

        if (guiDirectory is not null)
        {
            if (selfInside)
            {
                // 卸载程序自己就在这个目录里，直接删必然失败。交给退出后运行的临时脚本，
                // 并且**不算失败**——用户要的是"目录最后没了"，不是"现在就没了"。
                progress?.Invoke("卸载程序正在退出，目录随后由系统删除：" + guiDirectory);
            }
            else
            {
                progress?.Invoke("删除编辑器目录：" + guiDirectory);
                problems += DeleteTree(guiDirectory);
            }
        }

        // 命令行可能在安装目录里（<dir>\cli），也可能登记在别处
        var cliDirectories = new List<string>();
        if (guiDirectory is not null) cliDirectories.Add(Path.Combine(guiDirectory, "cli"));
        if (cli is not null && Path.GetDirectoryName(cli) is { Length: > 0 } c) cliDirectories.Add(c);

        foreach (string directory in cliDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory)) continue;
            // 只删“像我们”的目录，绝不把用户选过的任意目录整棵删掉
            string leaf = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar));
            bool ours = leaf.Equals("cli", StringComparison.OrdinalIgnoreCase)
                        || leaf.StartsWith("KeyMouse", StringComparison.OrdinalIgnoreCase);
            if (ours)
            {
                progress?.Invoke("删除命令行目录：" + directory);
                problems += DeleteTree(directory);
            }
            else
            {
                foreach (string file in new[] { "KeyMouse.exe", "KeyMouse.dll", "KeyMouse.Core.dll", "KeyMouse.Runner.dll" })
                {
                    string path = Path.Combine(directory, file);
                    if (!File.Exists(path)) continue;
                    try { File.Delete(path); } catch (Exception) { problems++; }
                }
            }
        }

        try
        {
            string link = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.ProductName + ".lnk");
            if (File.Exists(link)) File.Delete(link);
        }
        catch (Exception)
        {
            problems++;
        }

        progress?.Invoke("清空注册表登记…");
        KeyMouse.ProductRegistry.Clear();

        progress?.Invoke(problems == 0 ? "已卸载。" : $"卸载完成，但有 {problems} 项没删掉（可能正被占用）。");
        Log.Write("uninstall done, problems=" + problems);
        if (selfInside) SelfDelete(Path.GetDirectoryName(self));
        return problems == 0 ? 0 : 1;
    }

    private static int DeleteTree(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return 0;
            Directory.Delete(directory, recursive: true);
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    /// <summary>本进程占着 uninstall.exe：写一个临时脚本，等我们退出后再删目录。</summary>
    private static void SelfDelete(string? directory)
    {
        try
        {
            if (directory is null) return;

            string script = KeyMouse.Scratch.PathFor("keymouse-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
            File.WriteAllText(script, string.Join(Environment.NewLine,
                "@echo off",
                "ping 127.0.0.1 -n 3 >nul",
                "rmdir /s /q \"" + directory + "\" 2>nul",
                "del /f /q \"" + script + "\" 2>nul"), System.Text.Encoding.UTF8);

            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception)
        {
        }
    }
}
