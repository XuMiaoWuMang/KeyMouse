using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse;

/// <summary>
/// `probe` - the perception exit.
///
/// It reports what a window's pixels say; it never decides. Three things are deliberate:
///
/// * Reading is not sending, so the target does not have to be in the foreground. It does
///   have to pass the same eligibility gate as everything else (visible, not minimized,
///   not cloaked, responding) - a window that cannot paint cannot be read either.
/// * Confidence has a floor, but the floor only answers "did it read anything at all".
///   Measured on real UI text: every read under 60 was garbage, while a read with a single
///   wrong character still scored 83-96. Exactness is therefore the caller's business
///   (fuzzy matching), not something a threshold can buy.
/// * Re-reading N times and demanding identical text is what makes "seen" mean something.
///   It also drops transient noise such as an animation frame.
///
/// Failure to read is exit code 6: it belongs with 3/4/5 - nothing was sent, so retrying is
/// safe. A region that reads as genuinely empty is a *success* with no lines.
/// </summary>
internal static class Probe
{
    internal const int ExitUnreadable = 6;

    /// <summary>
    /// The confidence floor, and why it is this low.
    ///
    /// It started at 60, calibrated on synthetic renders where correct reads scored 92-96. Real
    /// screen text is different: ClearType anti-aliasing and a screen grab put a *correct* read of
    /// "ProbeCheck12345" at 55.8, while earlier garbage reads scored 14.7 to 51.6 - the distributions
    /// overlap, so no threshold can separate right from wrong here. What the floor can still do is
    /// catch the reads that failed outright, which measured 14.7 and 21.4. Correctness is therefore
    /// left where it belongs: with the consensus rule and the caller's matching budget.
    /// </summary>
    internal const int DefaultMinConfidence = 30;

    private const int EngineTimeoutMs = 30_000;

    internal static int Run(string[] args, Program.GlobalOptions g)
    {
        var (positional, options) = Program.Parse(
            args, "region", "reads", "lang", "engine", "tessdata-dir", "min-conf", "scale", "pad", "keep-image", "keep-prepared", "space", "capture", "resample");

        if (positional.Count > 0)
            throw new ArgumentException($"probe 不接受位置参数 '{positional[0]}'");
        if (!g.HasSelector)
            throw new ArgumentException("probe 需要一个窗口选择器（例如 --title 记事本）——工具不做全屏瞎猜");

        bool asJson = options.ContainsKey("json");
        int reads = Math.Clamp(Program.IntOr(options, "reads", 2), 1, 9);
        int minConfidence = Math.Clamp(Program.IntOr(options, "min-conf", DefaultMinConfidence), 0, 100);
        int scale = Math.Clamp(Program.IntOr(options, "scale", 1), 1, 8);
        int pad = Math.Clamp(Program.IntOr(options, "pad", 0), 0, 200);
        string resample = options.TryGetValue("resample", out string? sampling) && sampling.Length > 0
            ? sampling.ToLowerInvariant()
            : "bilinear";
        if (resample is not ("nearest" or "bilinear"))
            throw new ArgumentException($"--resample 只接受 nearest 或 bilinear，收到 '{resample}'");
        bool nearest = resample == "nearest";
        bool normalize = options.ContainsKey("normalize");
        string languages = options.TryGetValue("lang", out string? lang) && lang.Length > 0 ? lang : "eng+chi_sim";
        string engineCommand = ResolveEngine(
            options.TryGetValue("engine", out string? engine) && engine.Length > 0 ? engine : "tesseract");
        string? tessdata = options.TryGetValue("tessdata-dir", out string? dir) && dir.Length > 0 ? dir : DefaultTessdata();
        string? keepImage = options.TryGetValue("keep-image", out string? keep) && keep.Length > 0 ? keep : null;
        string? keepPrepared = options.TryGetValue("keep-prepared", out string? keep2) && keep2.Length > 0 ? keep2 : null;
        bool windowSpace = options.TryGetValue("space", out string? space) &&
                           string.Equals(space, "window", StringComparison.OrdinalIgnoreCase);
        string capture = options.TryGetValue("capture", out string? how) && how.Length > 0 ? how.ToLowerInvariant() : "screen";
        if (capture is not ("screen" or "print"))
            throw new ArgumentException($"--capture 只接受 screen 或 print，收到 '{capture}'");
        if (capture == "print" && windowSpace)
            throw new ArgumentException("--capture print 目前只支持客户区坐标（--space window 请用 screen）");

        // --pick-region: a human draws the rectangle, so a selector or a typed region would be a
        // second, contradictory answer to the same question.
        bool pickRegion = options.ContainsKey("pick-region");
        if (pickRegion && (g.HasSelector || options.ContainsKey("region") || options.ContainsKey("space")))
            throw new ArgumentException("--pick-region 自己决定窗口与区域：不要再给选择器、--region 或 --space");

        var started = Stopwatch.StartNew();

        // A picked region arrives in screen pixels and is converted here, once: the overlay only
        // reports what the human drew, the placement decides which window and which space it
        // belongs to. Everything after this point is the ordinary read path.
        (int X, int Y, int W, int H)? picked = null;
        WindowInfo window;
        if (pickRegion)
        {
            var selection = RegionPicker.Pick()
                ?? throw new CommandFailure(3, "已取消选区——未读取任何内容");
            var placement = RegionCommand.Describe(
                new ScreenRect(selection.X, selection.Y, selection.Width, selection.Height));
            if (placement.Window is null || placement.Space is null || placement.Region is not { } pickedRegion)
                throw new CommandFailure(3, placement.Note ?? "选区没有整个落在某个窗口里——未读取任何内容");

            picked = (pickedRegion.X, pickedRegion.Y, pickedRegion.Width, pickedRegion.Height);
            windowSpace = placement.Space == "window";
            if (capture == "print" && windowSpace)
                throw new ArgumentException("选到的是标题栏（窗口坐标），而 --capture print 只支持客户区——去掉它再试");

            var resolution = WindowResolver.Resolve(new WindowSelector { Handle = placement.Window.Handle }, false);
            if (resolution.Usable.Count == 0)
                throw new CommandFailure(4, "选中的窗口不可用：\n" + string.Join("\n", resolution.Rejections));
            window = resolution.Usable[0];
        }
        else
        {
            window = Program.ResolveUsable(g);
        }

        if (!NativeWindow.GetWindowRect(window.Handle, out NativeWindow.RECT windowRect))
            throw new CommandFailure(4, $"「{window.Title}」的窗口矩形读不出来——未读取任何内容");

        int fullWidth = windowRect.Right - windowRect.Left;
        int fullHeight = windowRect.Bottom - windowRect.Top;

        // Where does the captured image actually start? Measured, because both rectangles lie about it:
        // PrintWindow paints the window inset by the invisible resize border on all four sides, while
        // DWM reports that inset on the left, right and bottom but not the top, and GetWindowRect
        // reports none of it. A 1:1 capture settled it - the client text sat 35 pixels down while the
        // computed offset said 46, an 11 pixel error equal to the horizontal inset. So the inset is
        // taken from the frame widths and applied symmetrically.
        NativeWindow.TryGetVisibleFrame(window.Handle, out NativeWindow.RECT visibleFrame);
        int visibleWidth = visibleFrame.Right - visibleFrame.Left;
        int visibleHeight = visibleFrame.Bottom - visibleFrame.Top;
        int inset = Math.Max(0, (fullWidth - visibleWidth) / 2);
        if (visibleHeight <= 0) inset = 0;

        var geometry = new GeometryReference(
            [windowRect.Left, windowRect.Top, fullWidth, fullHeight],
            [visibleFrame.Left, visibleFrame.Top, visibleWidth, visibleHeight],
            inset);

        // Client-relative regions are the only sensible input: screen coordinates go stale the
        // moment a window is moved, and the caller cannot be expected to track that.
        var client = new NativeWindow.POINT { X = 0, Y = 0 };
        if (!NativeWindow.ClientToScreen(window.Handle, ref client))
            throw new CommandFailure(4, $"「{window.Title}」的客户区原点读不出来——未读取任何内容");
        int offsetX = client.X - windowRect.Left;
        int offsetY = client.Y - windowRect.Top;

        if (!NativeWindow.GetClientRect(window.Handle, out NativeWindow.RECT clientRect))
            throw new CommandFailure(4, $"「{window.Title}」的客户区尺寸读不出来——未读取任何内容");
        int clientWidth = clientRect.Right - clientRect.Left;
        int clientHeight = clientRect.Bottom - clientRect.Top;

        (int X, int Y, int W, int H) region = picked
            ?? (options.TryGetValue("region", out string? raw)
                ? ParseRegion(raw)
                : windowSpace ? (0, 0, fullWidth, fullHeight) : (0, 0, clientWidth, clientHeight));

        // Client-relative by default; --space window addresses the whole window, because a title
        // bar lives outside the client area and is often the only text a window has. The space
        // used is echoed in the output so a reader never has to guess which one was meant.
        int spaceWidth = windowSpace ? fullWidth : clientWidth;
        int spaceHeight = windowSpace ? fullHeight : clientHeight;
        string spaceName = windowSpace ? "窗口" : "客户区";
        if (region.X < 0 || region.Y < 0 || region.W <= 0 || region.H <= 0 ||
            region.X + region.W > spaceWidth || region.Y + region.H > spaceHeight)
        {
            throw new ArgumentException(
                $"区域 {region.X},{region.Y},{region.W},{region.H} 超出{spaceName}（{spaceWidth}x{spaceHeight}）——" +
                $"probe 的区域是{spaceName}相对坐标，不猜、也不截断");
        }

        int originX = windowSpace ? 0 : offsetX;
        int originY = windowSpace ? 0 : offsetY;

        // The screen grab starts on the desktop, so its origin depends on the space being
        // addressed: a window-space region is measured from the window's outer rectangle, a
        // client-space one from the client origin ClientToScreen reports. Getting this wrong is
        // silent - the first version always started at the client origin, so a title-bar selection
        // read the top strip of the client area instead. Proof: `--space window` and `--space client`
        // produced byte-identical images for the same region.
        int screenOriginX = windowSpace ? windowRect.Left : client.X;
        int screenOriginY = windowSpace ? windowRect.Top : client.Y;

        var engineInfo = DescribeEngine(engineCommand, languages, tessdata);

        // Read the region `reads` times. Each iteration captures again, so the consensus rule
        // covers capture noise (animations, a caret, a repaint) and not just engine
        // determinism - the engine itself measured byte-identical on identical input.
        var attempts = new List<(string Text, double? Confidence, LineInfo[] Lines)>();
        string imagePath = Path.Combine(Path.GetTempPath(), $"keymouse-probe-{Environment.ProcessId}.bmp");
        NativeCapture.Frame? prepared = null;

        try
        {
            for (int i = 0; i < reads; i++)
            {
                NativeCapture.Frame frame;
                if (capture == "print")
                {
                    var windowFrame = NativeCapture.TryCapture(window.Handle, fullWidth, fullHeight)
                        ?? throw new CommandFailure(ExitUnreadable,
                            $"「{window.Title}」拒绝为读取而绘制（最小化、被挂起或只剩一帧陈旧画面）——未读取到任何内容");
                    frame = windowFrame.Crop(originX + region.X, originY + region.Y, region.W, region.H);
                }
                else
                {
                    // Screen space: the window does have to be on screen, but there is no frame
                    // arithmetic beyond the origin picked above.
                    frame = NativeCapture.TryCaptureScreen(
                                screenOriginX + region.X, screenOriginY + region.Y, region.W, region.H)
                        ?? throw new CommandFailure(ExitUnreadable,
                            $"屏幕区域 {screenOriginX + region.X},{screenOriginY + region.Y} {region.W}x{region.H} 抓取失败——未读取到任何内容");
                }

                // --keep-image keeps the pixels as captured: no upscale, no padding, no contrast
                // normalisation. Written on every read, so a failed (inconsistent) read still leaves
                // behind what was actually on screen - which is the point of a diagnostic image.
                if (keepImage is not null) WriteBmp(Path.GetFullPath(keepImage), frame);

                // The engine sees the pixels that were captured unless the caller asks for more:
                // no upscale, no padding, no contrast stretch. Anything else is opt-in.
                prepared = Preprocess(normalize ? Normalize(frame) : frame, scale, pad, nearest);
                WriteBmp(imagePath, prepared);

                var read = RunEngine(engineCommand, imagePath, languages, tessdata);
                attempts.Add(read);
            }

            // The engine input, for when the question is "why did OCR read it that way".
            if (keepPrepared is not null && prepared is not null) WriteBmp(Path.GetFullPath(keepPrepared), prepared);
        }
        finally
        {
            if (File.Exists(imagePath))
            {
                try { File.Delete(imagePath); } catch (IOException) { /* a leftover temp file is not a failure */ }
            }
        }

        started.Stop();

        if (!IsSeen(attempts.Select(a => a.Text).ToList(), attempts[0].Confidence, minConfidence, reads, out string reason))
            throw new CommandFailure(ExitUnreadable, reason);

        string first = attempts[0].Text;
        bool consistent = attempts.All(a => a.Text == first);
        double? confidence = attempts[0].Confidence;
        bool anythingRead = attempts[0].Lines.Length > 0;
        string preparation = DescribePreparation(scale, pad, normalize, resample);

        var report = new ProbeReport(
            Ok: true,
            Engine: engineInfo,
            Window: new WindowReference($"0x{window.Handle.ToInt64():X}", window.Title, window.ProcessId),
            Geometry: geometry,
            Region: new RegionReference(windowSpace ? "window" : "client", [region.X, region.Y, region.W, region.H]),
            Prepared: preparation,
            Reads: reads,
            Consistent: consistent,
            Confidence: confidence,
            MinConfidence: minConfidence,
            ElapsedMs: started.ElapsedMilliseconds,
            Note: anythingRead ? null : "区域内没有可读文字——这是结果，不是失败",
            Lines: attempts[0].Lines);

        if (asJson) Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        else PrintHuman(report, attempts[0].Text, engineInfo, window, region, preparation, spaceName);

        return 0;
    }

    // ------------------------------------------------------------------ helpers

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
    internal static bool IsSeen(
        IReadOnlyList<string> reads, double? confidence, int minConfidence, int requested, out string reason)
    {
        reason = "";
        if (reads.Count == 0)
        {
            reason = "没有读取到任何结果";
            return false;
        }

        string first = reads[0];
        if (reads.Any(r => r != first))
        {
            string seen = string.Join("\n", reads.Select((r, i) => $"  第 {i + 1} 次：{Show(r)}"));
            reason = $"{requested} 次读取结果不一致，判定为没看清（未做出任何判断）：\n{seen}";
            return false;
        }

        if (first.Length > 0 && confidence is double c && c < minConfidence)
        {
            reason = $"读到了文字，但置信度 {c.ToString("0.0", CultureInfo.InvariantCulture)} 低于下限 {minConfidence}" +
                     $"（实测这个区间基本是乱码）：{Show(reads[0])}";
            return false;
        }

        return true;
    }

    /// <summary>Client-relative "x,y,w,h"; anything else is a usage error, not a guess.</summary>
    internal static (int X, int Y, int W, int H) ParseRegion(string raw)
    {
        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
            throw new ArgumentException($"--region 需要四个逗号分隔的整数（x,y,w,h），收到 '{raw}'");

        var values = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                throw new ArgumentException($"--region 的第 {i + 1} 个值 '{parts[i]}' 不是整数");
        }
        return (values[0], values[1], values[2], values[3]);
    }

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
    internal static NativeCapture.Frame Normalize(NativeCapture.Frame frame)
    {
        var histogram = new int[256];
        for (int i = 0; i < frame.Bgra.Length; i += 4)
        {
            int luma = (frame.Bgra[i] * 29 + frame.Bgra[i + 1] * 150 + frame.Bgra[i + 2] * 77) >> 8;
            histogram[luma]++;
        }

        int background = 0;
        for (int level = 1; level < 256; level++)
        {
            if (histogram[level] > histogram[background]) background = level;
        }

        // A blank region has nothing to normalise, and stretching it would only invent structure.
        // Tested rather than assumed: the previous guard compared the window size, which the
        // constant-size window can never trip, so a uniform grey region was being mapped to white.
        int nearBackground = 0;
        for (int level = Math.Max(0, background - 6); level <= Math.Min(255, background + 6); level++)
        {
            nearBackground += histogram[level];
        }
        int total = frame.Width * frame.Height;
        if (total - nearBackground < Math.Max(1, total / 1000)) return frame;

        bool darkOnLight = background >= 128;
        int low = darkOnLight ? background - 64 : background;
        int high = darkOnLight ? background : background + 64;
        if (high - low < 8) return frame;

        double scale = 255.0 / (high - low);
        var pixels = new byte[frame.Bgra.Length];
        for (int i = 0; i < frame.Bgra.Length; i += 4)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                double value = (frame.Bgra[i + channel] - low) * scale;
                value = Math.Clamp(value, 0, 255);
                pixels[i + channel] = (byte)(darkOnLight ? value + 0.5 : 255 - value + 0.5);
            }
            pixels[i + 3] = 255;
        }
        return new NativeCapture.Frame(frame.Width, frame.Height, pixels);
    }

    /// <summary>
    /// Upscale plus an optional white border, both opt-in: the default path hands the engine the
    /// pixels exactly as captured, so anything this does to them is something the caller asked for.
    ///
    /// Kept because it was measured to help on hard samples (three of four failing real samples
    /// were rescued by 3x upscaling) even though the current corpus cannot tell the two apart
    /// (10 samples: raw 9/10, upscaled 9~10/10 - see tests/evidence).
    ///
    /// Two samplers: `nearest` replicates each source pixel into a scale x scale block and invents
    /// nothing; `bilinear` interpolates, which measured one sample better on the current corpus.
    /// </summary>
    internal static NativeCapture.Frame Preprocess(
        NativeCapture.Frame source, int scale, int pad, bool nearest = false)
    {
        int scaledWidth = source.Width * scale;
        int scaledHeight = source.Height * scale;
        int width = scaledWidth + 2 * pad;
        int height = scaledHeight + 2 * pad;
        var pixels = new byte[width * height * 4];

        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xFF; pixels[i + 1] = 0xFF; pixels[i + 2] = 0xFF; pixels[i + 3] = 0xFF;
        }

        for (int y = 0; y < scaledHeight; y++)
        {
            int nearestY = Math.Min(source.Height - 1, y / scale);
            double sourceY = (y + 0.5) / scale - 0.5;
            int y0 = Math.Clamp((int)Math.Floor(sourceY), 0, source.Height - 1);
            int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
            double fy = Math.Clamp(sourceY - y0, 0, 1);

            for (int x = 0; x < scaledWidth; x++)
            {
                int destination = ((y + pad) * width + (x + pad)) * 4;

                if (nearest)
                {
                    int nearestX = Math.Min(source.Width - 1, x / scale);
                    int source4 = (nearestY * source.Width + nearestX) * 4;
                    pixels[destination] = source.Bgra[source4];
                    pixels[destination + 1] = source.Bgra[source4 + 1];
                    pixels[destination + 2] = source.Bgra[source4 + 2];
                    continue;
                }

                double sourceX = (x + 0.5) / scale - 0.5;
                int x0 = Math.Clamp((int)Math.Floor(sourceX), 0, source.Width - 1);
                int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
                double fx = Math.Clamp(sourceX - x0, 0, 1);

                for (int channel = 0; channel < 3; channel++)
                {
                    int i00 = (y0 * source.Width + x0) * 4 + channel;
                    int i10 = (y0 * source.Width + x1) * 4 + channel;
                    int i01 = (y1 * source.Width + x0) * 4 + channel;
                    int i11 = (y1 * source.Width + x1) * 4 + channel;

                    double top = source.Bgra[i00] * (1 - fx) + source.Bgra[i10] * fx;
                    double bottom = source.Bgra[i01] * (1 - fx) + source.Bgra[i11] * fx;
                    pixels[destination + channel] = (byte)Math.Clamp(top * (1 - fy) + bottom * fy + 0.5, 0, 255);
                }
            }
        }

        return new NativeCapture.Frame(width, height, pixels);
    }

    /// <summary>
    /// Writes a 32-bit top-down BMP. BMP rather than PNG on purpose: the encoder is 40 lines of
    /// header instead of a dependency, and the engine reads BMP natively.
    /// </summary>
    internal static void WriteBmp(string path, NativeCapture.Frame frame)
    {
        int stride = frame.Width * 4;
        int imageSize = stride * frame.Height;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(14 + 40 + imageSize);
        writer.Write(0);
        writer.Write(14 + 40);

        writer.Write(40);
        writer.Write(frame.Width);
        writer.Write(-frame.Height); // negative height = top-down rows
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);             // BI_RGB
        writer.Write(imageSize);
        writer.Write(2835);          // 72 DPI, in pixels per metre
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);
        writer.Write(frame.Bgra);
    }

    /// <summary>
    /// Runs the external engine over one image and parses its tsv.
    ///
    /// The engine is an explicit, replaceable dependency - not a bundled model. What it is and
    /// which model files it used go into the output, because "why did this work yesterday" has
    /// no answer otherwise.
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
                lines.Add(new LineInfo(key, text, rect, [words[^1]]));
            else
            {
                var line = lines[^1];
                lines[^1] = line with
                {
                    Text = line.Text + " " + text,
                    Words = [.. line.Words, words[^1]],
                };
            }
        }

        try { File.Delete(tsvPath); } catch (IOException) { }

        // Files come out one character per word for CJK, so "Save As" and "另存为" both arrive as
        // a run of pieces. The caller normalises whitespace; the pieces stay visible here.
        string joined = string.Join(" ", words.Select(w => w.Text));
        double? mean = words.Count > 0 ? Math.Round(words.Average(w => w.Conf ?? 0), 1) : null;
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
        LineInfo[] Lines);

    /// <summary>GetWindowRect versus the visible frame, so an offset bug is explainable.</summary>
    private sealed record GeometryReference(int[] ExtendedFrame, int[] VisibleFrame, int Inset);

    private sealed record EngineInfo(
        string Kind, string Command, string? Version, string Languages, string? Tessdata, EngineModel[] Models);

    private sealed record EngineModel(string Name, long Bytes);

    private sealed record WindowReference(string Handle, string Title, uint Pid);

    private sealed record RegionReference(string Space, int[] Rect);

    private sealed record WordInfo(string Text, double? Conf, int[] Rect);

    private sealed record LineInfo([property: System.Text.Json.Serialization.JsonIgnore] string Key, string Text, int[] Rect, WordInfo[] Words);
}
