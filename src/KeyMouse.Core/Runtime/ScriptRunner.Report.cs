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

    internal static void WriteReport(string path, ScriptReport report)
    {
        try
        {
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Keep CJK window titles and quotes readable instead of \uXXXX escapes.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
            Console.WriteLine($"报告已写入 {Path.GetFullPath(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The script's own result stays authoritative; a broken report path must not hide it.
            Console.Error.WriteLine($"错误：写不了报告 '{path}'：{ex.Message}");
        }
    }

    private static string Quote(string token) =>
        token.Length == 0 || token.Any(ch => char.IsWhiteSpace(ch) || ch == '#') ? $"\"{token}\"" : token;

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine("错误：" + message);
        return code;
    }
}
