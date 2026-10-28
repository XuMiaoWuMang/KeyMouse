using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse.Capture;

/// <summary>
/// 引擎：调用外部 OCR（命令行可替换）、解析 TSV、看引擎与模型是谁；ReadOnce 是给步骤用的取值入口。
/// </summary>
internal static partial class Probe
{

    internal static (string Text, double? Confidence, LineInfo[] Lines) ReadOnce(
        WindowInfo window, bool windowSpace, (int X, int Y, int W, int H) region, string capture = "screen")
    {
        ValidateRegion(window, windowSpace, region);
        NativeCapture.Frame frame = CaptureRegion(window, windowSpace, region, capture);
        string engineCommand = ResolveEngine("tesseract");
        string? tessdata = DefaultTessdata();
        var prepared = Preprocess(frame, 1, 0, nearest: false);
        string imagePath = Scratch.PathFor($"keymouse-probe-{Environment.ProcessId}.bmp");
        try
        {
            WriteBmp(imagePath, prepared);
            var read = RunEngine(engineCommand, imagePath, DefaultLanguages, tessdata);
            return (read.Text, read.Confidence, read.Lines);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                try { File.Delete(imagePath); } catch (IOException) { /* a leftover temp file is not a failure */ }
            }
        }
    }

    /// <summary>The language set the CLI defaults to; the predicate must agree with it.</summary>
    internal const string DefaultLanguages = "eng+chi_sim";

    /// <summary>
    /// The whole "did we see it" policy, in one testable place.
    ///
    /// Two rules: the reads must be **identical**, character for character - a disagreement means
    /// the picture was not stable, so there is nothing to report - and the confidence must clear
    /// the floor. The floor is deliberately low: it answers "did it read anything at all", not
    /// "is it correct".
    ///
    /// The comparison is literal on purpose. It used to fold whitespace and strip a trailing caret
    /// bar; both were the tool second-guessing the caller. A blinking caret inside the region now
    /// simply makes the two reads differ, which is reported as "not seen" (exit 6) and left to the
    /// caller to retry - the tool reads the image it was given and does nothing else with it.
    ///
    /// An empty read passes: nothing on screen is a fact, not a failure.
    /// </summary>
    private static string? DefaultTessdata()
    {
        string candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMouse", "tessdata");
        return Directory.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Turns a bare engine name into something runnable.
    ///
    /// Measured the hard way: the Tesseract installer puts the binary in Program Files and a
    /// child process of the tool did not resolve the bare name, so "tesseract" failed with a
    /// file-not-found while the same name worked in a shell. The well-known install locations
    /// are checked before giving up, and an explicit path is passed through untouched.
    /// </summary>
    internal static string ResolveEngine(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains('/')) return command;

        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", command + ".exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Tesseract-OCR", command + ".exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", command + ".exe"),
        ];
        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        return command;
    }

    /// <summary>
    /// Normalises contrast against the background, and flips dark backgrounds to dark-on-light.
    ///
    /// Measured twice over. First: a window in the background paints its title bar in the inactive
    /// style, light grey on white at roughly 15% contrast, and the engine reads that as garbage.
    /// Second: a plain percentile stretch did nothing about it, because the region also holds the
    /// window icon - a handful of pure black pixels that pin the bottom of the histogram. So the
    /// background is taken as the histogram mode instead (the level most pixels share) and a
    /// 64-level window ending at it is mapped across the full range. That is monotone and
    /// idempotent, so an already high-contrast image passes through unchanged.
    ///
    /// A dark background additionally gets inverted, because the engine wants dark on light; this
    /// is the one place polarity is decided, and it is decided from pixels rather than a flag.
    /// </summary>
    private static (string Text, double? Confidence, LineInfo[] Lines) RunEngine(
        string command, string imagePath, string languages, string? tessdata)
    {
        string outputBase = Path.ChangeExtension(imagePath, null) + "-out";
        string tsvPath = outputBase + ".tsv";
        if (File.Exists(tsvPath)) File.Delete(tsvPath);

        var psi = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add(outputBase);
        psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(languages);
        psi.ArgumentList.Add("--psm"); psi.ArgumentList.Add("6");
        if (tessdata is not null) { psi.ArgumentList.Add("--tessdata-dir"); psi.ArgumentList.Add(tessdata); }
        psi.ArgumentList.Add("tsv");

        try
        {
            using var process = Process.Start(psi)
                ?? throw new CommandFailure(ExitUnreadable, $"启动 OCR 引擎 '{command}' 失败——未读取到任何内容");
            string stderr = process.StandardError.ReadToEnd();
            process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(EngineTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                throw new CommandFailure(ExitUnreadable, $"OCR 引擎 '{command}' 超过 {EngineTimeoutMs / 1000} 秒未返回——未读取到任何内容");
            }
            if (process.ExitCode != 0)
            {
                throw new CommandFailure(ExitUnreadable,
                    $"OCR 引擎 '{command}' 退出码 {process.ExitCode}：{Show(stderr)}");
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new CommandFailure(ExitUnreadable,
                $"找不到 OCR 引擎 '{command}'——用 --engine 指定可执行文件，或先安装它（未读取到任何内容）");
        }

        if (!File.Exists(tsvPath))
        {
            // This is the exact trap that cost a whole debugging round: tsv is a config file
            // that lives in <tessdata>/configs, so a hand-made model directory without it
            // silently produces no tsv at all.
            throw new CommandFailure(ExitUnreadable,
                $"OCR 引擎没有产出 tsv（{tsvPath}）——多半是模型目录缺少 configs/ 子目录，" +
                "而 tsv 正是置信度的来源");
        }

        var words = new List<WordInfo>();
        var lines = new List<LineInfo>();
        foreach (string row in File.ReadAllLines(tsvPath).Skip(1))
        {
            string[] cells = row.Split('\t');
            if (cells.Length < 12) continue;
            if (cells[0] != "5") continue;                       // level 5 = word
            if (!double.TryParse(cells[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double conf) || conf < 0) continue;
            string text = cells[11];
            if (text.Length == 0) continue;

            var rect = new[] { Int(cells[6]), Int(cells[7]), Int(cells[8]), Int(cells[9]) };
            words.Add(new WordInfo(text, Math.Round(conf, 1), rect));

            string key = $"{cells[2]}/{cells[3]}/{cells[4]}";
            if (lines.Count == 0 || lines[^1].Key != key)
            {
                lines.Add(new LineInfo(key, text, rect, [words[^1]]));
            }
            else
            {
                // The line's box is the union of its words. It used to stay the *first* word's box,
                // which made lines[].rect quietly wrong for any line with more than one word.
                var line = lines[^1];
                WordInfo[] extended = [.. line.Words, words[^1]];
                lines[^1] = line with
                {
                    Text = line.Text + " " + text,
                    Rect = TextLocator.Union(extended.Select(w => w.Rect)),
                    Words = extended,
                };
            }
        }

        try { File.Delete(tsvPath); } catch (IOException) { }

        // Files come out one character per word for CJK, so "Save As" and "另存为" both arrive as
        // a run of pieces. The caller normalises whitespace; the pieces stay visible here.
        string joined = string.Join(" ", words.Select(w => w.Text));        double? mean = words.Count > 0 ? Math.Round(words.Average(w => w.Conf ?? 0), 1) : null;
        return (joined, mean, [.. lines]);
    }

    private static EngineInfo DescribeEngine(string command, string languages, string? tessdata)
    {
        string? version = null;
        try
        {
            var psi = new ProcessStartInfo(command)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");
            using var process = Process.Start(psi);
            if (process is not null && process.WaitForExit(5000))
                version = process.StandardOutput.ReadToEnd().Split('\n').FirstOrDefault()?.Trim();
        }
        catch (Exception)
        {
            // Version is identity, not function: a missing answer must not fail the read.
        }

        var models = new List<EngineModel>();
        if (tessdata is not null && Directory.Exists(tessdata))
        {
            foreach (string file in Directory.EnumerateFiles(tessdata, "*.traineddata").OrderBy(f => f))
            {
                var info = new FileInfo(file);
                models.Add(new EngineModel(Path.GetFileNameWithoutExtension(file), info.Length));
            }
        }

        return new EngineInfo("external", command, version, languages, tessdata, [.. models]);
    }

    /// <summary>What, if anything, was done to the pixels before the engine saw them.</summary>
    private static string DescribePreparation(int scale, int pad, bool normalize, string resample)
    {
        var parts = new List<string>();
        if (normalize) parts.Add("对比度归一化");
        if (scale > 1 || pad > 0) parts.Add($"放大 {scale}× + 白边 {pad}px（{resample}）");
        return parts.Count == 0 ? "原始像素直送引擎" : string.Join(" + ", parts) + " 后直送引擎";
    }

}

