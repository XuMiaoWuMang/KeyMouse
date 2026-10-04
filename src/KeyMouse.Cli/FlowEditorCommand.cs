using System.Diagnostics;

namespace KeyMouse;

/// <summary>
/// `flow edit` - opens the WinUI editor on a flow. The editor ships separately (its own project under
/// editor/), so this looks for it next to the tool first and then in a development checkout, and says
/// what to build when it is nowhere to be found.
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

        // 查找图形编辑器
        string? editor = ResolveEditor();
        if (editor is null)
        {
            return Commands.Fail(4,
                "找不到编辑器 KeyMouse.FlowEditor.exe，它应当在相同目录下");
        }

        // todo：未知作用的 UseShellExecute
        var startInfo = new ProcessStartInfo(editor) { UseShellExecute = false };
        startInfo.ArgumentList.Add(path);
        Process.Start(startInfo);
        Console.WriteLine($"已打开编辑器 {editor}");
        Console.WriteLine($"  流程 {path}");
        return 0;
    }

    private static string? ResolveEditor()
    {
        string candidates ;
        string here = AppContext.BaseDirectory;
        candidates = Path.Combine(here, "edit\\KeyMouse.FlowEditor.exe");

        // // A development checkout: walk up looking for the editor's build output.
        // var directory = new DirectoryInfo(here);
        // for (int i = 0; i < 6 && directory is not null; i++, directory = directory.Parent)
        // {
        //     foreach (string configuration in (string[])["Release", "Debug"])
        //     {
        //         candidates.Add(Path.Combine(directory.FullName, "editor", "KeyMouse.FlowEditor", "bin",
        //             configuration, "net10.0-windows10.0.26100.0", "win-x64", "KeyMouse.FlowEditor.exe"));
        //     }
        // }
        // candidates.Add(Path.Combine(Environment.CurrentDirectory, "editor", "KeyMouse.FlowEditor", "bin",
        //     "Release", "net10.0-windows10.0.26100.0", "win-x64", "KeyMouse.FlowEditor.exe"));
        // foreach (string candidate in candidates)
        // {
        //     if (File.Exists(candidate)) return candidate;
        // }

        // 不要自己遍历，浪费时间，约定好 KeyMouse.FlowEditor.exe 和 KeyMouse.exe 在同一目录下就行了
        if (File.Exists(candidates)) return candidates;
        else return null;
    }
}
