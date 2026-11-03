using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// `record --ignore-process`：整个进程的窗口都不录。
///
/// 为什么需要它（而不是只用 `--ignore-window`）：外壳的录制浮条是**录制中途**才建出来的，
/// 录制开始时根本拿不到它的句柄；而"点浮条上的停止"这一下就落在浮条上。
/// 不按进程名跳过的话，每一份录出来都会多出一步"点那块屏幕"的点击。
/// </summary>
internal static class RecordIgnoreProcessTests
{
    internal static void Run()
    {
        NameIsTrimmed();
        ExeSuffixIsAccepted();
        EmptyNameIsRejected();
        UnknownOptionIsKeptButIgnored();
    }

    private static void NameIsTrimmed()
    {
        Harness.Check("首尾空白被去掉", Recorder.ParseIgnoreProcess("  KeyMouse.Editor  ") == "KeyMouse.Editor");
    }

    /// <summary>
    /// 用户手写命令时经常连 `.exe` 一起写；引擎比的是不带扩展名的进程名，
    /// 这里顺手剥掉，别让人因为多写四个字符就失效。
    /// </summary>
    private static void ExeSuffixIsAccepted()
    {
        Harness.Check("带 .exe 也认", Recorder.ParseIgnoreProcess("KeyMouse.Editor.exe") == "KeyMouse.Editor");
        Harness.Check("大小写不敏感", Recorder.ParseIgnoreProcess("keymouse.editor.EXE") == "keymouse.editor");
    }

    private static void EmptyNameIsRejected()
    {
        bool refused = false;
        try
        {
            _ = Recorder.ParseIgnoreProcess("   ");
        }
        catch (ArgumentException)
        {
            refused = true;
        }

        Harness.Check("空的 --ignore-process 直接报错", refused);

        bool refusedExeOnly = false;
        try
        {
            _ = Recorder.ParseIgnoreProcess(".exe");
        }
        catch (ArgumentException)
        {
            refusedExeOnly = true;
        }

        Harness.Check("只写 .exe 也算空", refusedExeOnly);
    }

    /// <summary>
    /// 选项名写错时，引擎目前**不会**报错（`Commands.Parse` 只做切分，各命令自己认自己的名字）。
    /// 这条用例钉住这个现状：写错的名字会被原样放进选项表，由命令自己忽略——
    /// 所以外壳传参必须与引擎的选项名逐字一致（`--ignore-process`）。
    /// </summary>
    private static void UnknownOptionIsKeptButIgnored()
    {
        var (positional, options) = Commands.Parse(["--ignore-proces", "KeyMouse.Editor"], "out", "ignore-process");

        // 写错名字的后果比"被忽略"更糟：因为它不在 valueFlags 里，后面的值掉出来变成位置参数，
        // record 于是报"不接受位置参数 'KeyMouse.Editor'"。名字必须逐字对上。
        Harness.Check("写错名字会让它的值掉成位置参数", positional.Count == 1 && positional[0] == "KeyMouse.Editor");
        Harness.Check("写错的选项名原样留在选项表里", options.ContainsKey("ignore-proces"));
    }
}
