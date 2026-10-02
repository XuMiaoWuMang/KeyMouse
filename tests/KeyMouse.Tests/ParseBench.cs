using System.Diagnostics;
using System.Text.Json;

namespace KeyMouse.Tests;

/// <summary>
/// Answers "is a text script slower to parse than a JSON one?" with numbers, and shows the
/// numbers next to what a command actually costs at runtime.
/// Run it with: dotnet run -c Release --project tests/KeyMouse.Tests -- bench
/// </summary>
internal static class ParseBench
{
    public static void Run()
    {
        const int count = 2000;
        const int reps = 50;

        string[] lines = BuildLines(count);
        string[][] commands = lines
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => ScriptRunner.Tokenize(l).ToArray())
            .ToArray();
        string json = JsonSerializer.Serialize(commands);

        // Warm up both paths so JIT and first-allocation costs stay out of the numbers.
        ScriptRunner.Parse(lines.Take(20).ToArray());
        JsonSerializer.Deserialize<string[][]>(json);

        double textMs = Measure(reps, () => ScriptRunner.Parse(lines));
        double jsonMs = Measure(reps, () => JsonSerializer.Deserialize<string[][]>(json));

        Console.WriteLine($"""
            脚本规模：{count} 行命令；文本 {lines.Sum(l => l.Length) / 1024.0:F1} KB，等价 JSON {json.Length / 1024.0:F1} KB

            【1】解析整个脚本（{reps} 次平均）
              文本脚本（KeyMouse 分词器）{textMs,8:F3} ms  ({textMs * 1000 / count,5:F2} µs/行)
              JSON（System.Text.Json）  {jsonMs,8:F3} ms  ({jsonMs * 1000 / count,5:F2} µs/行)
              文本比 JSON 快 {jsonMs / textMs:F2} 倍——但两者都是 {Math.Min(textMs, jsonMs):F1}~{Math.Max(textMs, jsonMs):F1} 毫秒级别

            【2】执行开销（真实 dispatcher，输出已静音）
            """);

        double posMs = MeasureQuiet(200, () => Program.Main(new[] { "mouse", "pos" }));
        double selectorMs = MeasureQuiet(20, () => Program.Main(new[]
        {
            "key", "press", "f24", "--process", "definitely-not-running-xyz"
        }));
        double typeMs = MeasureQuiet(5, () => Program.Main(new[]
        {
            "key", "type", "0123456789ABCDEFGHIJ", "--process", "definitely-not-running-xyz"
        }));

        Console.WriteLine($"  mouse pos（无选择器，最便宜）        {posMs,9:F3} ms");
        Console.WriteLine($"  带选择器（枚举窗口 + 进程名查询）    {selectorMs,9:F3} ms");
        Console.WriteLine($"  同上，已是可用窗口并打 20 个字符     {typeMs,9:F3} ms  （光打字间隔就 20×15ms）");

        Console.WriteLine($"""

            【3】"一条选择器命令为什么这么贵"的拆解
            """);

        var windows = WindowLocator.EnumerateTopLevel();
        double enumMs = Measure(20, () => WindowLocator.EnumerateTopLevel());
        var pids = windows.Select(w => w.ProcessId).Distinct().ToList();
        double lookupMs = Measure(20, () =>
        {
            foreach (uint pid in pids)
            {
                try { _ = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
            }
        });

        Console.WriteLine($"  顶层窗口数                        {windows.Count,9}");
        Console.WriteLine($"  枚举全部窗口（不含探测）           {enumMs,9:F3} ms");
        Console.WriteLine($"  其中：{pids.Count} 个不同进程的 Process.GetProcessById 查询 {lookupMs,9:F3} ms");

        Console.WriteLine($"""

            【结论】
              解析 {count} 行：文本 {textMs:F2} ms / JSON {jsonMs:F2} ms
              执行 {count} 条命令：最快 {posMs * count / 1000:F1} 秒，带选择器约 {selectorMs * count / 1000:F0} 秒
              解析占总耗时：文本 {textMs / (selectorMs * count) * 100:F4}%，JSON {jsonMs / (selectorMs * count) * 100:F4}%
            """);
    }

    private static double Measure(int reps, Action action)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < reps; i++) action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / reps;
    }

    /// <summary>Same as Measure, but swallows whatever the action writes to stdout and stderr
    /// (the dispatcher reports failures on stderr, which would otherwise flood the report).</summary>
    private static double MeasureQuiet(int reps, Action action)
    {
        TextWriter savedOut = Console.Out;
        TextWriter savedErr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            return Measure(reps, action);
        }
        finally
        {
            Console.SetOut(savedOut);
            Console.SetError(savedErr);
        }
    }

    private static string[] BuildLines(int count)
    {
        var lines = new List<string>(count + 8) { "# 基准用脚本" };
        for (int i = 0; i < count; i++)
        {
            switch (i % 5)
            {
                case 0: lines.Add($"key type \"第 {i} 行：中文与 English 混排\" --process notepad"); break;
                case 1: lines.Add($"mouse click left -wx {i % 800} -wy {i % 600} --process notepad"); break;
                case 2: lines.Add("key press enter --process notepad"); break;
                case 3: lines.Add($"mouse wheel -120 -x {i % 1200} -y {i % 700}"); break;
                default: lines.Add($"sleep {i % 50}"); break;
            }
        }
        return lines.ToArray();
    }
}
