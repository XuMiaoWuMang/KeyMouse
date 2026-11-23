using System.Text;
using System.Text.Json;

namespace KeyMouse.Runtime;

/// <summary>
/// 批处理的出口与落盘：可重试判定、脚本读取、报告写出、失败输出。
/// </summary>
internal static partial class ScriptRunner
{
    /// <summary>
    /// Exit codes that prove nothing was sent, so trying again is safe: 3 (no match / cancelled),
    /// 4 (target unusable), 5 (focus not verified) and 6 (could not read). The invariant is written
    /// down in docs/development.md; 6 was missing here while the docs promised it, which is exactly
    /// the kind of drift this comment exists to stop.
    /// </summary>
    internal static bool IsRetryable(int exitCode) => exitCode is 3 or 4 or 5 or 6;

    private static string[] ReadScript(string path)
    {
        byte[] bytes = path == "-" ? ReadStdin() : File.ReadAllBytes(path);

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new CommandFailure(2, "脚本不是合法的 UTF-8——请另存为 UTF-8" +
                                        "（ANSI/GBK 无法解码，中文必须用 UTF-8）");
        }

        if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static byte[] ReadStdin()
    {
        using Stream stdin = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        stdin.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// 编辑器临时目录之外的另一处报告落点。`LOCALAPPDATA\KeyMouse\tmp` 可能被安全软件锁住、
    /// 被上一个进程留下的只读文件占住，或权限被改坏；报告只是补充，换一个用户一定能写的
    /// `%TEMP%` 不应该让整次运行看起来失败。
    /// </summary>
    internal static string FallbackReportPath =>
        Path.Combine(Path.GetTempPath(), "KeyMouse", "editor-run-report.json");

    internal static void WriteReport(string path, ScriptReport report)
    {
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Keep CJK window titles and quotes readable instead of \uXXXX escapes.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        string body = json + Environment.NewLine;

        Exception? first = null;
        foreach (string candidate in new[] { path, FallbackReportPath })
        {
            try
            {
                WriteWithRetry(candidate, body);
                if (!string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase))
                {
                    CommandOutput.Error.WriteLine($"提示：主报告路径写不了（{first?.Message}），已改写到 '{candidate}'");
                }
                CommandOutput.Out.WriteLine($"报告已写入 {Path.GetFullPath(candidate)}");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                first ??= ex;
            }
        }

        // The script's own result stays authoritative; a broken report path must not hide it.
        CommandOutput.Error.WriteLine($"错误：写不了报告 '{path}'：{first?.Message}");
    }

    /// <summary>
    /// Windows 上杀毒/索引器短暂打开目标文件是常态；一次 AccessDenied 不代表路径真的坏了。
    /// 三次短重试后还不行才换备用路径。
    /// </summary>
    private static void WriteWithRetry(string path, string body)
    {
        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        for (int attempt = 1; ; attempt++)
        {
            // Keep staging on the same volume so the completed report can be committed
            // by a single rename; failed writes or replacements leave the old report intact.
            string temporary = Path.Combine(directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, body, new UTF8Encoding(false));
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 3) throw;
                Thread.Sleep(80 * attempt);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cleanup is best effort and must not hide the write failure.
                }
            }
        }
    }

    private static string Quote(string token) =>
        token.Length == 0 || token.Any(ch => char.IsWhiteSpace(ch) || ch == '#') ? $"\"{token}\"" : token;

    private static int Fail(int code, string message)
    {
        CommandOutput.Error.WriteLine("错误：" + message);
        return code;
    }
}
