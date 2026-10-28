using System.Diagnostics;

namespace KeyMouse;

/// <summary>
/// `flow edit`：用图形编辑器打开一个流程。
///
/// 编辑器和命令行是同一份产品的两半，位置约定写在 <see cref="ProductLayout"/> 一个地方：
/// 安装版里编辑器在 &lt;安装目录&gt;\、命令行在 &lt;安装目录&gt;\cli\，开发树里编辑器在
/// src\KeyMouse.FlowEditor\bin\…、命令行在 artifacts\cli\。以前这里只认 `edit\` 子目录，
/// 两种形态都不存在那个文件夹，于是这条命令永远是坏的。
/// </summary>
internal static class FlowEditorCommand
{
    internal static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] != "edit")
            return Commands.Fail(2, "用法：KeyMouse flow edit <流程.json>（用图形编辑器打开一个流程）");
        if (args.Length < 2)
            return Commands.Fail(2, "用法：KeyMouse flow edit <流程.json>");

        string path = Path.GetFullPath(args[1]);
        if (!File.Exists(path)) return Commands.Fail(2, $"没有这个文件：{path}");

        string? editor = ProductLayout.EditorPath;
        if (editor is null)
        {
            Console.Error.WriteLine("找不到编辑器 KeyMouse.FlowEditor.exe。找过这些地方：");
            foreach (LayoutCandidate candidate in ProductLayout.EditorCandidates())
            {
                if (candidate.Path.Length == 0) continue;
                Console.Error.WriteLine($"  {(candidate.Exists ? "有" : "无")} {candidate.Path}（{candidate.Why}）");
            }
            Console.Error.WriteLine();
            Console.Error.WriteLine("安装版请重新运行安装程序；开发树里先编一次：pwsh tools\\dev\\km.ps1 ba");
            Console.Error.WriteLine("排障：KeyMouse doctor");
            return 4;
        }

        // 不用 ShellExecute：编辑器不需要 UAC，用 false 能直接把参数按数组传过去（标题里有空格也不怕）。
        var startInfo = new ProcessStartInfo(editor) { UseShellExecute = false };
        startInfo.ArgumentList.Add(path);
        Process.Start(startInfo);
        Console.WriteLine($"已打开编辑器 {editor}");
        Console.WriteLine($"  流程 {path}");
        return 0;
    }
}
