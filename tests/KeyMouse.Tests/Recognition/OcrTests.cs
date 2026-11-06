using KeyMouse.Capture;
using KeyMouse.Recognition.Ocr;

namespace KeyMouse.Tests.Recognition;

/// <summary>
/// OCR, tested where it can be tested without a screen: the three stages that decide what the text
/// says (post-processing, decoding, tensor layout) run on synthetic numbers, and the model load is
/// asserted to actually produce a dictionary.
///
/// What is **not** here on purpose: "does it read the screenshot correctly". That answer depends on
/// the machine's fonts and scaling, and it is checked by hand with a real probe run - see
/// `docs/dev/ocr.md` for the command and the expected text.
/// </summary>
internal static class OcrTests
{
    internal static void Run()
    {
        TensorLayout();
        BoxesFromProbabilityMap();
        Decoding();
        Dictionary();
        ModelLoads();
    }

    private static void TensorLayout()
    {
        // 2×1 picture: one white pixel then one black pixel, BGR = (255,255,255) and (0,0,0).
        byte[] pixels = [255, 255, 255, 255, 0, 0, 0, 255];
        float[] tensor = OcrImage.ToTensor(pixels, 2, 1, [0.5f, 0.5f, 0.5f], [0.5f, 0.5f, 0.5f]);

        Harness.Check("OCR 张量是 CHW 且按 (值/255-mean)/std 归一化",
            Math.Abs(tensor[0] - 1f) < 0.001f && Math.Abs(tensor[1] + 1f) < 0.001f,
            $"得到 {tensor[0]:0.###} / {tensor[1]:0.###}");

        Harness.Check("OCR 缩放比：长边超过上限时按比例缩，否则不动",
            Math.Abs(OcrImage.ScaleForMax(1920, 1080, 960) - 0.5) < 0.001 &&
            Math.Abs(OcrImage.ScaleForMax(800, 100, 960) - 1.0) < 0.001,
            $"1920→{OcrImage.ScaleForMax(1920, 1080, 960):0.###}，800→{OcrImage.ScaleForMax(800, 100, 960):0.###}");

        Harness.Check("OCR 边长对齐到 32", OcrImage.AlignUp(100, 32) == 96 && OcrImage.AlignUp(5, 32) == 32,
            $"{OcrImage.AlignUp(100, 32)} / {OcrImage.AlignUp(5, 32)}");
    }

    private static void BoxesFromProbabilityMap()
    {
        // 20×10 map with one lit rectangle: x 4..7, y 2..4, probability 0.9.
        const int width = 20, height = 10;
        var probability = new float[width * height];
        for (int y = 2; y <= 4; y++)
        {
            for (int x = 4; x <= 7; x++) probability[y * width + x] = 0.9f;
        }
        // A single lit pixel must not become a box (MinSize 3).
        probability[9 * width + 19] = 0.95f;

        List<DbPostProcess.Box> boxes = DbPostProcess.Extract(
            probability, width, height, scaleX: 2, scaleY: 2, DbPostProcess.Options.Default);

        Harness.Check("概率图里连成一片的文字只出一个框，单像素被最小尺寸挡掉",
            boxes.Count == 1, $"得到 {boxes.Count} 个框");

        if (boxes.Count == 1)
        {
            DbPostProcess.Box box = boxes[0];
            Harness.Check("框按源图比例放大，分数取区域均值",
                box.Width >= 8 && box.Height >= 4 && Math.Abs(box.Score - 0.9) < 0.01,
                $"{box.Width}x{box.Height} @ {box.X},{box.Y} score {box.Score:0.00}");
        }

        // Same row, right of the first: order must be left to right, not by discovery order.
        var two = new float[width * height];
        for (int y = 2; y <= 4; y++)
        {
            two[y * width + 12] = 0.9f;
            two[y * width + 13] = 0.9f;
            two[y * width + 14] = 0.9f;
            two[y * width + 3] = 0.9f;
            two[y * width + 4] = 0.9f;
            two[y * width + 5] = 0.9f;
        }
        List<DbPostProcess.Box> sorted = DbPostProcess.Extract(two, width, height, 1, 1, DbPostProcess.Options.Default);
        Harness.Check("同一行的框按从左到右排", sorted.Count == 2 && sorted[0].X < sorted[1].X,
            string.Join(" / ", sorted.Select(b => b.X)));
    }

    private static void Decoding()
    {
        // 3 timesteps × 4 classes; dictionary is blank + a + b + c.
        var dictionary = new List<string> { string.Empty, "a", "b", "c" };
        float[] logits =
        [
            0.05f, 0.90f, 0.03f, 0.02f,   // a
            0.05f, 0.90f, 0.03f, 0.02f,   // a again → repeat, dropped
            0.02f, 0.03f, 0.90f, 0.05f,   // b
        ];
        CtcDecoder.Result result = CtcDecoder.Decode(logits, 3, 4, dictionary);

        Harness.Check("CTC 解码：重复帧只出一个字、空白不留字",
            result.Text == "ab", $"得到「{result.Text}」");
        Harness.Check("CTC 置信度是产出字符那几帧的平均（不把空白帧算进去）",
            Math.Abs(result.Confidence - 0.9) < 0.01, $"{result.Confidence:0.###}");

        float[] blanks = [0.9f, 0.02f, 0.02f, 0.02f, 0.9f, 0.02f, 0.02f, 0.02f];
        Harness.Check("全空白读出空串，不是乱码",
            CtcDecoder.Decode(blanks, 2, 4, dictionary).Text.Length == 0, "ok");
    }

    private static void Dictionary()
    {
        List<string> dictionary = CtcDecoder.ParseDictionary("保\n存\nA\n");
        Harness.Check("字符表：第 0 位是空白，后面按行取字",
            dictionary.Count == 4 && dictionary[0].Length == 0 && dictionary[1] == "保" && dictionary[3] == "A",
            string.Join("|", dictionary.Select(c => c.Length == 0 ? "∅" : c)));

        Harness.Check("没有 metadata 时只有空白位，不会假装认识字",
            CtcDecoder.ParseDictionary(null).Count == 1, "ok");
    }

    private static void ModelLoads()
    {
        OcrModels.Location? location = OcrModels.Locate();
        if (location is null)
        {
            Console.WriteLine("  跳过：这台机器上没有 OCR 模型（跑 tools/build/fetch-ocr-models.ps1 之后就会测）");
            return;
        }

        bool created = PaddleOcr.TryCreate(out PaddleOcr? ocr, out string? why);
        using (ocr)
        {
            Harness.Check("内置 OCR 能加载模型并读出字符表", created && ocr is not null, why ?? "ok");
            if (ocr is null) return;

            // PP-OCRv4 的中文字表是 6623 个字（含拉丁与符号）。远低于 6000 说明读错了 metadata。
            Harness.Check("字符表规模合理（> 6000 个字）", ocr.DictionarySize > 6000,
                $"{ocr.DictionarySize} 个字");
        }
    }
}
