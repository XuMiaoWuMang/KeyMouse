namespace KeyMouse.Tests;

/// <summary>
/// 换行只有一种写法：\n。
///
/// 这不是洁癖。WinUI 的 TextBox 用裸 \r 存换行，Windows 文件里是 \r\n；两者原样写进流程文件后，
/// 引擎会把 \r 当成 CRLF 的前半个字符跳过，于是一个回车都不发——"编辑器里明明三行，打出来一行"。
/// 所以在模型这一层就把三种写法折平。
/// </summary>
internal static class NewlineTests
{
    public static void Run()
    {
        Harness.Group("newlines: 换行只留 \\n");

        Harness.Equal("CRLF 折成 LF", "甲\n乙", new FlowStep { Text = "甲\r\n乙" }.Text);
        Harness.Equal("裸 CR 折成 LF", "甲\n乙", new FlowStep { Text = "甲\r乙" }.Text);
        Harness.Equal("已经是 LF 就不动", "甲\n乙", new FlowStep { Text = "甲\n乙" }.Text);
        Harness.Equal("连续多个换行一个不少", "甲\n\n乙", new FlowStep { Text = "甲\r\n\r\n乙" }.Text);
        Harness.Equal("空文本还是空", "", new FlowStep { Text = "" }.Text);
        Harness.Equal("null 保持 null", null, new FlowStep { Text = null }.Text);
    }
}
