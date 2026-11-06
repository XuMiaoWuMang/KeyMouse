using System.Globalization;
using KeyMouse.Capture;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace KeyMouse.Recognition.Ocr;

/// <summary>
/// Built-in OCR: PaddleOCR's detection + recognition models, run through ONNX Runtime.
///
/// Modelled on BetterGI's `PaddleOcrService` (see `docs/dev/ocr.md` for the reading list): same two
/// models, same preprocessing constants, same CTC rules, same "drop low-score boxes" policy. The
/// difference is what we do **not** take: no OpenCV, no model downloader, no language matrix. One
/// Chinese+English model pair, one code path, models fetched by a build script.
///
/// Why built-in at all: the previous engine was Tesseract behind an external install, and on screen
/// text it was the weakest link - no boxes per line, weak on CJK, and a missing install turned
/// "read the screen" into "install something first". This keeps the same `(text, confidence, lines)`
/// shape so the rest of the probe did not change.
/// </summary>
internal sealed class PaddleOcr : IDisposable
{
    // Detection, from PaddleOCR's PP-OCRv4 config (the same numbers BetterGI's Det defaults use).
    private const int DetLimitSide = 960;
    private const int DetMaxSide = 4000;
    private static readonly float[] DetMean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] DetStd = [0.229f, 0.224f, 0.225f];

    // Recognition: the model's input height is fixed at 48; width is free.
    private const int RecHeight = 48;
    private const int RecMaxWidth = 2000;
    private static readonly float[] RecMean = [0.5f, 0.5f, 0.5f];
    private static readonly float[] RecStd = [0.5f, 0.5f, 0.5f];

    /// <summary>Boxes below this score are dropped: PP-OCR's own default for the rec stage.</summary>
    private const double DropScore = 0.5;

    private readonly InferenceSession _det;
    private readonly InferenceSession _rec;
    private readonly string _detInput;
    private readonly string _recInput;
    private readonly List<string> _dictionary;
    private readonly object _gate = new();
    private bool _disposed;

    internal string DetPath { get; }
    internal string RecPath { get; }

    private PaddleOcr(InferenceSession det, InferenceSession rec, List<string> dictionary, OcrModels.Location location)
    {
        _det = det;
        _rec = rec;
        _detInput = det.InputMetadata.Keys.First();
        _recInput = rec.InputMetadata.Keys.First();
        _dictionary = dictionary;
        DetPath = location.DetPath;
        RecPath = location.RecPath;
    }

    /// <summary>
    /// Loads the models, or explains why it could not. A missing model is a normal state (a fresh
    /// checkout before `fetch-ocr-models.ps1`), so this returns false instead of throwing.
    /// </summary>
    internal static bool TryCreate(out PaddleOcr? ocr, out string? reason)
    {
        ocr = null;
        OcrModels.Location? location = OcrModels.Locate();
        if (location is null)
        {
            reason = $"没找到 OCR 模型（{OcrModels.DetFile} / {OcrModels.RecFile}）；" +
                     "跑一次 tools/build/fetch-ocr-models.ps1，或用 --engine tesseract 走外部引擎";
            return false;
        }

        InferenceSession? det = null;
        InferenceSession? rec = null;
        try
        {
            var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
            det = new InferenceSession(location.DetPath, options);
            rec = new InferenceSession(location.RecPath, options);

            string? metadata = null;
            try
            {
                metadata = rec.ModelMetadata.CustomMetadataMap.TryGetValue("character", out string? characters)
                    ? characters
                    : null;
            }
            catch (Exception)
            {
                // Metadata is optional in the format; a model without it is reported as "no words".
            }

            List<string> dictionary = CtcDecoder.ParseDictionary(metadata);
            if (dictionary.Count < 2)
            {
                reason = $"模型里没有字符表（{Path.GetFileName(location.RecPath)} 的 metadata.character），" +
                         "读出来的字会全是空白";
                det.Dispose();
                rec.Dispose();
                return false;
            }

            ocr = new PaddleOcr(det, rec, dictionary, location);
            reason = null;
            return true;
        }
        catch (Exception failure)
        {
            det?.Dispose();
            rec?.Dispose();
            reason = $"加载 OCR 模型失败：{failure.Message}";
            return false;
        }
    }

    /// <summary>The number of characters the rec model can produce; identity, not behaviour.</summary>
    internal int DictionarySize => _dictionary.Count;

    /// <summary>
    /// Reads one frame. Boxes and rects come back in frame coordinates, which is what the probe's
    /// region maths already expects.
    /// </summary>
    internal OcrRead Read(byte[] bgra, int width, int height)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            List<DbPostProcess.Box> boxes = Detect(bgra, width, height);
            if (boxes.Count == 0) return OcrRead.Empty;

            var lines = new List<OcrLine>(boxes.Count);
            foreach (DbPostProcess.Box box in boxes)
            {
                byte[] crop = OcrImage.Crop(bgra, width, height, (box.X, box.Y, box.Width, box.Height), out int cropWidth,
                    out int cropHeight);
                (string text, double score) = Recognize(crop, cropWidth, cropHeight);
                if (text.Length == 0 || score < DropScore) continue;
                lines.Add(new OcrLine(text, score, [box.X, box.Y, box.Width, box.Height]));
            }

            if (lines.Count == 0) return OcrRead.Empty;

            string joined = string.Join("\n", lines.Select(l => l.Text));
            double mean = Math.Round(lines.Average(l => l.Score), 1);
            return new OcrRead(joined, mean, [.. lines]);
        }
    }

    private List<DbPostProcess.Box> Detect(byte[] bgra, int width, int height)
    {
        double scale = OcrImage.ScaleForMax(width, height, DetLimitSide);
        int targetWidth = Math.Min(DetMaxSide, Math.Max(32, (int)Math.Round(width * scale)));
        int targetHeight = Math.Min(DetMaxSide, Math.Max(32, (int)Math.Round(height * scale)));
        targetWidth = OcrImage.AlignUp(targetWidth, 32);
        targetHeight = OcrImage.AlignUp(targetHeight, 32);

        byte[] resized = OcrImage.Resize(bgra, width, height, targetWidth, targetHeight);
        float[] tensor = OcrImage.ToTensor(resized, targetWidth, targetHeight, DetMean, DetStd);

        var input = new DenseTensor<float>(tensor, [1, 3, targetHeight, targetWidth]);
        using var results = _det.Run([NamedOnnxValue.CreateFromTensor(_detInput, input)]);
        Tensor<float> output = results.First().AsTensor<float>();
        int[] dims = output.Dimensions.ToArray();
        int mapHeight = dims[^2];
        int mapWidth = dims[^1];

        double scaleX = (double)width / mapWidth;
        double scaleY = (double)height / mapHeight;
        return DbPostProcess.Extract(output.ToArray(), mapWidth, mapHeight, scaleX, scaleY, DbPostProcess.Options.Default);
    }

    private (string Text, double Score) Recognize(byte[] crop, int cropWidth, int cropHeight)
    {
        int targetWidth = Math.Max(1, (int)Math.Round(cropWidth * (double)RecHeight / cropHeight));
        targetWidth = Math.Min(RecMaxWidth, targetWidth);
        byte[] resized = OcrImage.Resize(crop, cropWidth, cropHeight, targetWidth, RecHeight);
        float[] tensor = OcrImage.ToTensor(resized, targetWidth, RecHeight, RecMean, RecStd);

        var input = new DenseTensor<float>(tensor, [1, 3, RecHeight, targetWidth]);
        using var results = _rec.Run([NamedOnnxValue.CreateFromTensor(_recInput, input)]);
        Tensor<float> output = results.First().AsTensor<float>();
        int[] dims = output.Dimensions.ToArray();
        int timesteps = dims[^2];
        int classes = dims[^1];

        CtcDecoder.Result decoded = CtcDecoder.Decode(output.ToArray(), timesteps, classes, _dictionary);
        return (decoded.Text, Math.Round(decoded.Confidence * 100, 1));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _det.Dispose();
            _rec.Dispose();
        }
    }

    /// <summary>One line of text with the box it occupies (frame coordinates).</summary>
    internal sealed record OcrLine(string Text, double Score, int[] Rect);

    /// <summary>What a read produced, in the shape `Probe` already speaks.</summary>
    internal sealed record OcrRead(string Text, double? Confidence, OcrLine[] Lines)
    {
        internal static OcrRead Empty { get; } = new(string.Empty, null, []);
    }

    /// <summary>Kept for the report: the model files are the identity of this engine.</summary>
    internal string Describe() =>
        string.Create(CultureInfo.InvariantCulture,
            $"paddle-ocr（{Path.GetFileName(DetPath)} + {Path.GetFileName(RecPath)}，{_dictionary.Count - 1} 个字）");
}
