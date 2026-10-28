using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse.Capture;

/// <summary>
/// 输出形状：人读的表格、JSON 选项、以及报告里的记录类型。
/// </summary>
internal static partial class Probe
{

    private static int Int(string raw) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static string Show(string text)
    {
        string clean = text.Replace("\r", " ").Replace("\n", " / ").Trim();
        return clean.Length == 0 ? "(空)" : clean;
    }

    private static void PrintHuman(
        ProbeReport report, string text, EngineInfo engine, WindowInfo window,
        (int X, int Y, int W, int H) region, string preparation, string spaceName)
    {
        Console.WriteLine($"引擎      {engine.Command} {engine.Version}".TrimEnd());
        Console.WriteLine($"语言      {engine.Languages}" +
                          (engine.Tessdata is null ? "（引擎自带 tessdata）" : $"  模型 {engine.Tessdata}"));
        if (engine.Models.Length > 0)
        {
            Console.WriteLine("模型      " + string.Join("  ", engine.Models.Select(m => $"{m.Name} {m.Bytes / 1024.0 / 1024.0:0.0} MB")));
        }
        Console.WriteLine($"目标      「{window.Title}」 {report.Window.Handle}");
        Console.WriteLine($"区域      {spaceName} {region.X},{region.Y},{region.W}x{region.H}  →  {preparation}");
        Console.WriteLine($"读取      {report.Reads} 次，{(report.Consistent ? "逐字一致 ✓" : "不一致")}" +
                          (report.Confidence is double c ? $"  置信度 {c:0.0}（下限 {report.MinConfidence}）" : "  置信度 n/a"));
        Console.WriteLine($"耗时      {report.ElapsedMs} ms");

        if (report.Lines.Length == 0)
        {
            Console.WriteLine("文本      （读到空——这是结果，不是失败）");
            return;
        }

        Console.WriteLine($"文本      {Show(text)}");
        foreach (var line in report.Lines)
        {
            Console.WriteLine($"          [{line.Rect[0]},{line.Rect[1]} {line.Rect[2]}x{line.Rect[3]}] {line.Text}");
        }

        if (report.Find is { } find)
        {
            Console.WriteLine(find is { Found: true, At: { } at, Rect: { } rect }
                ? $"找到      「{find.Text}」→ 客户点 {at.X},{at.Y}   框 {rect[0]},{rect[1]} {rect[2]}x{rect[3]}" +
                  $"   差 {find.Distance} 字   置信度 {find.Confidence?.ToString("0.0", CultureInfo.InvariantCulture) ?? "n/a"}"
                : $"查找      「{find.Text}」不在读到的内容里（匹配方式 {find.Match}，容错 {find.MaxErrors}）");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    // ------------------------------------------------------------------ report shape

    private sealed record ProbeReport(
        bool Ok,
        EngineInfo Engine,
        WindowReference Window,
        GeometryReference Geometry,
        RegionReference Region,
        string Prepared,
        int Reads,
        bool Consistent,
        double? Confidence,
        int MinConfidence,
        long ElapsedMs,
        string? Note,
        LineInfo[] Lines,
        FindReference? Find);

    /// <summary>Where the text the caller asked for was found, and where to click for it.</summary>
    private sealed record FindReference(
        string Text, bool Found, string Match, int MaxErrors, int[]? Rect, FindPoint? At,
        string? Line, int? Distance, double? Confidence);

    private sealed record FindPoint(string Space, int X, int Y);

    /// <summary>GetWindowRect versus the visible frame, so an offset bug is explainable.</summary>
    private sealed record GeometryReference(int[] ExtendedFrame, int[] VisibleFrame, int Inset);

    private sealed record EngineInfo(
        string Kind, string Command, string? Version, string Languages, string? Tessdata, EngineModel[] Models);

    private sealed record EngineModel(string Name, long Bytes);

    private sealed record WindowReference(string Handle, string Title, uint Pid);

    private sealed record RegionReference(string Space, int[] Rect);

}

