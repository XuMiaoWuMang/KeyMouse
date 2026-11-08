namespace KeyMouse.Tests;

/// <summary>
/// 流程控制模块的入口：一个模块名跑三个文件里的断言。
///
/// 按功能拆文件是为了改动时只读相关的那一份；模块名合成一个是为了 `--only flow-control`
/// 一条命令把这一整块跑完。表达式求值是独立模块（expr），因为它不依赖解释器。
/// </summary>
internal static class FlowControlTests
{
    public static void Run()
    {
        IfElseTests.Run();
        WhileTests.Run();
        ForSugarTests.Run();
    }
}
