using Microsoft.Win32;

namespace KeyMouse;

/// <summary>
/// 安装位置登记处：HKCU\Software\KeyMouse。
///
/// 只有两件事要知道：界面在哪（GuiPath）、命令行在哪（CliPath）。
/// 写它的人是安装程序，读它的人是编辑器——**这是两者之间唯一的约定**，
/// 谁也不该再在代码里写"命令行应该在我旁边"这种猜测（发布环境一换就错，实测过）。
///
/// 用 HKCU 而不是 HKLM：安装不需要管理员权限，卸载也就干净。
/// </summary>
internal static class ProductRegistry
{
    internal const string KeyPath = @"Software\KeyMouse";
    internal const string GuiValue = "GuiPath";
    internal const string CliValue = "CliPath";

    /// <summary>读一条已登记的文件路径；登记了但文件已经不在，就当没登记。</summary>
    internal static string? Read(string name)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue(name) is not string path || path.Length == 0) return null;
            return File.Exists(path) ? path : null;
        }
        catch (Exception)
        {
            // 注册表读不了（被策略挡了之类）不是致命问题：调用方还有别的候选
            return null;
        }
    }

    internal static void Write(string name, string path)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
                ?? throw new InvalidOperationException("建不了注册表项 " + KeyPath);
            key.SetValue(name, path, RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            // 写不进去也要让安装继续：文件已经到位，编辑器还有相对路径兜底。
            // 但不能静默——安装程序的问题必须留下痕迹。
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "keymouse-registry.log"),
                    DateTime.Now.ToString("HH:mm:ss") + " write " + name + "=" + path + " -> " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>卸载时清掉整个键。</summary>
    internal static void Clear()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception)
        {
            // 同理
        }
    }
}
