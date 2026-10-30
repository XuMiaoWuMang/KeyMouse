namespace KeyMouse.Cli;

/// <summary>
/// The command-line entry point. One product, two entry points: this one, and the editor.
///
/// Everything that is not `serve`, `runner ...` or `flow edit` goes straight into the capability
/// layer, exactly as before - the CLI keeps being a plain blocking tool with exit codes, because that
/// is what scripts and the smoke suite depend on. `serve` starts the resident Runner; `runner ...`
/// drives it from a script instead of doing the work in this process; and the editor connects to the
/// same service over the same pipe. All three run the same dispatch.
/// </summary>
internal static class Program
{
    [STAThread]
    internal static int Main(string[] args)
    {
        // --elevate：以管理员身份重跑同一条命令。
        // 不把提权写进 manifest，是因为命令行会被脚本和 CI 大量调用——
        // 那样每次 `mouse click` 都弹一次 UAC，等于把工具废掉。
        if (args.Length > 0 && args[0] == "--elevate")
        {
            return RelaunchElevated(args[1..]);
        }

        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "serve":
                    return Runner.RunnerHost.Run(args[1..]);
                case "runner":
                    return RunnerCommand.Run(args[1..]);
                case "flow" when args.Length > 1 && args[1] == "edit":
                    // Launching the editor is a front-end concern (it starts another application).
                    return FlowEditorCommand.Run(args[1..]);
            }
        }
        // 每次启动都挂一个"可以被按停"的命名事件（名字带自己的 pid，外壳按 pid 找得到）。
        using PauseControl? pause = PauseControl.Install(Environment.ProcessId);
        return Commands.Execute(args);
    }

    /// <summary>
    /// 用 runas 重新启动自己并等它跑完，把它的退出码原样带回来。
    /// 用户点了 UAC 的"否"时返回 1223（ERROR_CANCELLED）——和 Windows 的约定一致。
    /// </summary>
    private static int RelaunchElevated(string[] args)
    {
        string? self = Environment.ProcessPath;
        if (self is null || !File.Exists(self))
        {
            Console.Error.WriteLine("提权失败：找不到自己的可执行文件路径");
            return 2;
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo(self)
        {
            UseShellExecute = true,     // runas 需要 ShellExecute
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory,
        };
        foreach (string argument in args) startInfo.ArgumentList.Add(argument);

        try
        {
            using System.Diagnostics.Process? elevated = System.Diagnostics.Process.Start(startInfo);
            if (elevated is null)
            {
                Console.Error.WriteLine("提权失败：系统没有启动进程");
                return 2;
            }
            elevated.WaitForExit();
            return elevated.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Console.Error.WriteLine("已取消提权（UAC 被拒绝）——没有执行任何操作。");
            return 1223;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("提权失败：" + ex.Message);
            return 2;
        }
    }
}
