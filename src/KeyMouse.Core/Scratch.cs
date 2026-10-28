namespace KeyMouse;

/// <summary>
/// 临时文件的落脚点。
///
/// 首选系统的 %TEMP%，但它不一定可写：受限的自动化沙箱、被重定向的配置、只读漫游配置……
/// 以前这种情况会让 probe / 录制整条路上报 "Access to the path ... is denied"——
/// 这句话既说不清原因，也不是流程本身的问题。临时文件本来就是可以换地方的，
/// 所以这里挑第一个真能写进去的目录，并在 doctor 里说明最后用的是哪一个。
///
/// 顺序：%TEMP% -> %LOCALAPPDATA%\KeyMouse\tmp -> 程序所在目录。
/// </summary>
internal static class Scratch
{
    private static readonly Lazy<string> Selected = new(Select);

    /// <summary>临时文件应当写到的目录。</summary>
    internal static string Directory => Selected.Value;

    /// <summary>这个目录下的一个文件名。</summary>
    internal static string PathFor(string name) => Path.Combine(Directory, name);

    /// <summary>是否退到了备用目录（也就是 %TEMP% 不可写）。doctor 用它决定要不要多解释一句。</summary>
    internal static bool FellBack => !SamePath(Directory, SystemTemp());

    private static string Select()
    {
        foreach (string candidate in Candidates())
        {
            if (CanWrite(candidate)) return candidate;
        }

        // 三个都写不进去时仍然返回程序目录：至少让调用方拿到一个确定的路径，
        // 报错会发生在真正写文件的那一行，带着它自己的上下文。
        return AppContext.BaseDirectory;
    }

    private static IEnumerable<string> Candidates()
    {
        string? temp = SystemTemp();
        if (!string.IsNullOrWhiteSpace(temp)) yield return temp!;

        string? local = System.Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(local)) yield return Path.Combine(local!, "KeyMouse", "tmp");

        yield return AppContext.BaseDirectory;
    }

    private static string? SystemTemp()
    {
        // GetTempPath 只在环境变量全都缺失时才抛（InsufficientExecutionStack 之外）。
        try { return Path.GetTempPath(); }
        catch (Exception) { return null; }
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $"keymouse-write-test-{System.Environment.ProcessId}");
            using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None, 1))
            {
                stream.WriteByte(0);
            }
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            // 这里的异常只说明"这个目录用不了"，而它的全部意义就是换下一个。
            return false;
        }
    }

    private static bool SamePath(string left, string? right)
    {
        if (right is null) return false;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }
}
