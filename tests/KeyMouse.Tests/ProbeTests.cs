using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// Unit cover for `probe`'s pure parts.
///
/// What is worth testing here is the policy and the pixels, not the window plumbing: the
/// decision rules ("did we see it"), the client-region parser, and the three image
/// transforms - normalise, upscale, and the BMP writer - because a silent bug in any of
/// them would show up as "OCR is bad" rather than as a failure.
/// </summary>
internal static class ProbeTests
{
    public static void Run()
    {
        Harness.Group("probe: region parsing");
        Harness.Section("--region", () =>
        {
            var (x, y, w, h) = Probe.ParseRegion("6,5,100,30");
            Harness.Equal("x", 6, x);
            Harness.Equal("y", 5, y);
            Harness.Equal("w", 100, w);
            Harness.Equal("h", 30, h);

            var spaced = Probe.ParseRegion(" 1 , 2 , 3 , 4 ");
            Harness.Equal("whitespace tolerated", 3, spaced.W);

            var negative = Probe.ParseRegion("-4,-5,10,10");
            Harness.Equal("negative offsets survive parsing (bounds are checked later)", -4, negative.X);

            Harness.Throws<ArgumentException>("three values are a usage error", () => Probe.ParseRegion("6,5,100"));
            Harness.Throws<ArgumentException>("five values are a usage error", () => Probe.ParseRegion("1,2,3,4,5"));
            Harness.Throws<ArgumentException>("a non-integer is a usage error", () => Probe.ParseRegion("a,5,100,30"));
            Harness.Throws<ArgumentException>("an empty string is a usage error", () => Probe.ParseRegion(""));
        });

        Harness.Group("probe: did we see it");
        Harness.Section("consensus", () =>
        {
            Harness.Check("one read, high confidence: seen",
                Probe.IsSeen(["另存为"], 95, 60, 1, out _));
            Harness.Check("two agreeing reads: seen",
                Probe.IsSeen(["另存为", "另存为"], 95, 60, 2, out _));
            Harness.Check("whitespace differences are not disagreements",
                Probe.IsSeen(["Save As", "SaveAs"], 95, 60, 2, out _));

            Harness.Check("disagreeing reads: not seen",
                !Probe.IsSeen(["另存为", "另存力"], 95, 60, 2, out string mismatch));
            Harness.Check("the reason names the reads", mismatch.Contains("另存力"), mismatch);

            Harness.Check("an empty read is a fact, not a failure",
                Probe.IsSeen(["", ""], null, 60, 2, out _));
            Harness.Check("an empty read is not judged by confidence",
                Probe.IsSeen(["", ""], 3, 60, 2, out _));

            Harness.Check("confidence below the floor: not seen",
                !Probe.IsSeen(["FRA"], 49.3, 60, 1, out string low));
            Harness.Check("the reason quotes the confidence", low.Contains("49.3"), low);

            Harness.Check("confidence exactly at the floor passes",
                Probe.IsSeen(["保存"], 60, 60, 1, out _));
            Harness.Check("no reads at all: not seen",
                !Probe.IsSeen([], null, 60, 0, out _));
        });

        Harness.Group("probe: normalise");
        Harness.Section("contrast and polarity", () =>
        {
            // Light grey on white, as a background window paints its title bar. Measured to read
            // as garbage before normalisation; the point of the test is that the text gets darker.
            var faint = Frame(20, 4, (x, _) => x < 8 ? (byte)205 : (byte)250);
            var fixedUp = Probe.Normalize(faint);
            Harness.Check("faint grey text on white becomes dark",
                Luma(fixedUp, 3, 1) < 80, $"luma {Luma(fixedUp, 3, 1)}");
            Harness.Equal("the background stays light", (byte)255, fixedUp.Bgra[((0 * 20) + 15) * 4]);

            // Already black on white: normalising must not damage it.
            var strong = Frame(20, 4, (x, _) => x < 8 ? (byte)0 : (byte)255);
            var kept = Probe.Normalize(strong);
            Harness.Equal("black text stays black", (byte)0, kept.Bgra[3 * 4]);
            Harness.Equal("white stays white", (byte)255, kept.Bgra[15 * 4]);

            // Dark mode: light text on a dark background must come out inverted, because the
            // engine wants dark on light. This is the one place polarity is decided.
            var dark = Frame(20, 4, (x, _) => x < 8 ? (byte)235 : (byte)32);
            var flipped = Probe.Normalize(dark);
            Harness.Check("light-on-dark gets inverted to dark-on-light",
                Luma(flipped, 3, 1) < 80 && Luma(flipped, 15, 1) > 200,
                $"text {Luma(flipped, 3, 1)}, background {Luma(flipped, 15, 1)}");

            var flat = Frame(20, 4, (_, _) => (byte)128);
            Harness.Check("a flat image is left alone rather than stretched into noise",
                ReferenceEquals(flat, Probe.Normalize(flat)));
        });

        Harness.Group("probe: preprocess");
        Harness.Section("upscale and padding", () =>
        {
            var source = Frame(10, 3, (_, _) => (byte)10);
            var prepared = Probe.Preprocess(source, 3, 16);
            Harness.Equal("width = w*scale + 2*pad", 10 * 3 + 32, prepared.Width);
            Harness.Equal("height = h*scale + 2*pad", 3 * 3 + 32, prepared.Height);

            Harness.Equal("the border is white (top-left)", (byte)255, prepared.Bgra[0]);
            Harness.Equal("the border is white (bottom-right)",
                (byte)255, prepared.Bgra[prepared.Bgra.Length - 1]);
            Harness.Equal("the image body survives inside the border",
                (byte)10, prepared.Bgra[((16 + 1) * prepared.Width + 16 + 1) * 4]);

            var again = Probe.Preprocess(source, 3, 16);
            Harness.Check("preprocessing is deterministic",
                prepared.Bgra.SequenceEqual(again.Bgra));

            var identity = Probe.Preprocess(source, 1, 0);
            Harness.Equal("scale 1 with no padding is a copy", (byte)10, identity.Bgra[5 * 4]);
        });

        Harness.Group("probe: BMP writer");
        Harness.Section("header", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"keymouse-bmp-test-{Environment.ProcessId}.bmp");
            try
            {
                var frame = Frame(5, 2, (_, _) => (byte)7);
                Probe.WriteBmp(path, frame);
                byte[] bytes = File.ReadAllBytes(path);

                Harness.Equal("magic", "BM", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
                Harness.Equal("declared file size matches the file",
                    bytes.Length, BitConverter.ToInt32(bytes, 2));
                Harness.Equal("pixel data starts after both headers", 54, BitConverter.ToInt32(bytes, 10));
                Harness.Equal("header size", 40, BitConverter.ToInt32(bytes, 14));
                Harness.Equal("width", 5, BitConverter.ToInt32(bytes, 18));
                Harness.Equal("negative height means top-down rows", -2, BitConverter.ToInt32(bytes, 22));
                Harness.Equal("32 bits per pixel", 32, BitConverter.ToInt16(bytes, 28));
                Harness.Equal("no compression", 0, BitConverter.ToInt32(bytes, 30));
                Harness.Equal("body size", 5 * 2 * 4, bytes.Length - 54);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        });

        Harness.Group("probe: engine resolution");
        Harness.Section("engine path", () =>
        {
            string explicitPath = @"C:\some\where\tesseract.exe";
            Harness.Equal("an explicit path is passed through untouched",
                explicitPath, Probe.ResolveEngine(explicitPath));

            // A bare name either resolves to a real install or is handed back for PATH to try.
            string resolved = Probe.ResolveEngine("tesseract");
            Harness.Check("a bare name yields something runnable or the name itself",
                resolved.Length > 0, resolved);
            bool looksAbsolute = Path.IsPathRooted(resolved);
            Harness.Check("resolution either finds a file or leaves the name alone",
                !looksAbsolute || File.Exists(resolved), resolved);
        });
    }

    private static NativeCapture.Frame Frame(int width, int height, Func<int, int, byte> grey)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                byte value = grey(x, y);
                pixels[i] = value; pixels[i + 1] = value; pixels[i + 2] = value; pixels[i + 3] = 255;
            }
        }
        return new NativeCapture.Frame(width, height, pixels);
    }

    private static int Luma(NativeCapture.Frame frame, int x, int y)
    {
        int i = (y * frame.Width + x) * 4;
        return (frame.Bgra[i] * 29 + frame.Bgra[i + 1] * 150 + frame.Bgra[i + 2] * 77) >> 8;
    }
}
