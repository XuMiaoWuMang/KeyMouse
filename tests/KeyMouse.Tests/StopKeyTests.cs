using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// 录制的两个停止方式：
/// - Ctrl+Alt+Q：低级钩子报的是 VK_LCONTROL(0xA2)，只认 VK_CONTROL(0x11) 会失灵；
/// - 连按两次 ESC：第一次不能先记进流程，否则"停止键"会在自己的流程里留下一个 esc。
/// </summary>
internal static class StopKeyTests
{
    private static Recorder.Session Session() => new(
        Path.Combine(Path.GetTempPath(), "keymouse-stopkey-test.json"),
        300, 8, false, 0);

    private static HookEvent Key(int vk, uint time, bool up = false) =>
        new(up ? HookKind.KeyUp : HookKind.KeyDown, vk, 0, 0, 0, time, IntPtr.Zero);

    public static void Run()
    {
        Harness.Group("stopkey: 单按 ESC 是一次真实按键");

        var single = Session();
        single.OnEvent(Key(0x1B, 1000));
        single.Finish();
        Harness.Equal("单按 ESC 会被记进流程", 1,
            single.Steps.Count(s => s.Type == "key" && (s.Text ?? "").Contains("esc")));

        Harness.Group("stopkey: 连按两次 ESC 停止，且一个 esc 都不留");

        var twice = Session();
        twice.OnEvent(Key(0x1B, 1000));
        twice.OnEvent(Key(0x1B, 1060, up: true));   // 第一次的抬起：真实按键序列里一定存在
        twice.OnEvent(Key(0x1B, 1200));
        Harness.Check("第二次 ESC 触发停止", twice.ShouldStop());
        twice.Finish();
        Harness.Equal("停止键不进流程", 0,
            twice.Steps.Count(s => s.Type == "key" && (s.Text ?? "").Contains("esc")));

        Harness.Group("stopkey: Ctrl+Alt+Q 用左右 Ctrl 都要认");

        // 钩子报 VK_LCONTROL(0xA2)：旧实现只认 VK_CONTROL，于是这组永远不会触发。
        var combo = Session();
        combo.OnEvent(Key(0xA2, 1000));   // 左 Ctrl
        combo.OnEvent(Key(0xA4, 1010));   // 左 Alt
        combo.OnEvent(Key(0x51, 1020));   // Q
        Harness.Check("左 Ctrl + 左 Alt + Q 触发停止", combo.ShouldStop());
        combo.Finish();
        Harness.Equal("组合键本身不进流程", 0, combo.Steps.Count);
    }
}
