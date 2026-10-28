namespace KeyMouse.Setup;

/// <summary>安装/卸载轨迹：%TEMP%\keymouse-setup.log（不可写时退到 %LOCALAPPDATA%\KeyMouse\tmp）。用户报"装不上"时先看它。</summary>
internal static class Log
{
    internal static string File => Scratch.PathFor("keymouse-setup.log");

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
