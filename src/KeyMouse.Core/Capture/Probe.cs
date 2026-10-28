using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KeyMouse.Capture;
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
internal static partial class Probe
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

    internal static int Run(string[] args, Commands.GlobalOptions g)
    {
        var (positional, options) = Commands.Parse(
            args, "region", "reads", "lang", "engine", "tessdata-dir", "min-conf", "scale", "pad", "keep-image", "keep-prepared", "space", "capture", "resample", "find", "match", "max-errors");

        if (positional.Count > 0)
            throw new ArgumentException($"probe 不接受位置参数 '{positional[0]}'");
        if (!g.HasSelector)
            throw new ArgumentException("probe 需要一个窗口选择器（例如 --title 记事本）——工具不做全屏瞎猜");

        bool asJson = options.ContainsKey("json");
        string? findText = options.TryGetValue("find", out string? rawFind) && rawFind.Length > 0 ? rawFind : null;
        string matchMode = options.TryGetValue("match", out string? rawMatch) ? rawMatch : TextPredicate.Contains;
        if (!TextPredicate.IsKnownMode(matchMode))
        {
            throw new ArgumentException(
                $"--match '{matchMode}' 不认识（可用：{string.Join(" | ", TextPredicate.Modes)}）");
        }
        int maxErrors = Math.Clamp(Commands.IntOr(options, "max-errors", 1), 0, 20);
        int reads = Math.Clamp(Commands.IntOr(options, "reads", 2), 1, 9);
        int minConfidence = Math.Clamp(Commands.IntOr(options, "min-conf", DefaultMinConfidence), 0, 100);
        int scale = Math.Clamp(Commands.IntOr(options, "scale", 1), 1, 8);
        int pad = Math.Clamp(Commands.IntOr(options, "pad", 0), 0, 200);
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
            window = Commands.ResolveUsable(g);
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
        string spaceName = windowSpace ? "窗口" : "客户区";
        ValidateRegion(window, windowSpace, region);

        var engineInfo = DescribeEngine(engineCommand, languages, tessdata);

        // Read the region `reads` times. Each iteration captures again, so the consensus rule
        // covers capture noise (animations, a caret, a repaint) and not just engine
        // determinism - the engine itself measured byte-identical on identical input.
        var attempts = new List<(string Text, double? Confidence, LineInfo[] Lines)>();
        string imagePath = Scratch.PathFor($"keymouse-probe-{Environment.ProcessId}.bmp");
        NativeCapture.Frame? prepared = null;

        try
        {
            for (int i = 0; i < reads; i++)
            {
                NativeCapture.Frame frame = CaptureRegion(window, windowSpace, region, capture);

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
            Lines: attempts[0].Lines,
            Find: findText is null
                ? null
                : DescribeFind(
                    TextLocator.Find(attempts[0].Lines, findText, matchMode, maxErrors),
                    findText, matchMode, maxErrors, window, windowSpace, region));

        if (asJson) Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        else PrintHuman(report, attempts[0].Text, engineInfo, window, region, preparation, spaceName);

        // "Not there" is a match failure, not a read failure: exit 3 means nothing was sent and the
        // caller may retry - the same promise the rest of the tool makes for "no match".
        if (report.Find is { Found: false } missing)
        {
            if (!asJson)
            {
                Console.WriteLine($"没找到「{missing.Text}」" +
                                  (attempts[0].Text.Length > 0 ? $"：读到「{Show(attempts[0].Text)}」" : "：区域里没读到文字"));
            }
            return 3;
        }

        return 0;
    }

    /// <summary>
    /// Turns a match into something a caller can act on: the box it was found in, and the point to
    /// click - in client coordinates, whichever space the region was addressed in (a window-space
    /// region is measured from the window's outer rectangle, so the client origin has to be removed).
    /// </summary>
}

