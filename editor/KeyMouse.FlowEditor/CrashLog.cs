using System.Text;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 崩溃落地。两个入口共用一份文件：UI 线程的 <c>UnhandledException</c>，
/// 以及**非 UI 线程**的 <c>AppDomain.UnhandledException</c> / <c>TaskScheduler.UnobservedTaskException</c>。
///
/// 后者才是"程序直接没了、日志里什么都没有"的典型原因：管道读线程或 Runner 回调里的异常
/// 不走 XAML 的处理器，进程就此结束。记录它，问题才有得查。
/// </summary>
internal static class CrashLog
{
    private static readonly object Gate = new();

    internal static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMouse", "editor-crash.log");

    internal static void Write(Exception? exception, string? scope = null)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var text = new StringBuilder()
                .Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
                .Append(scope is { Length: > 0 } ? scope + ": " : "")
                .Append(exception).AppendLine().AppendLine();
            lock (Gate) File.AppendAllText(Path, text.ToString());
        }
        catch (Exception)
        {
            // 连写文件都失败时，兜底本身也不许再把进程带走。
        }
    }
}
