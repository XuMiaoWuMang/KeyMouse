namespace KeyMouse.Tests;

/// <summary>
/// The half of `region pick` that must not need a desktop: a screen rectangle in, a window space
/// out. The overlay itself needs a human (or simulated input) and is covered by smoke.ps1.
/// </summary>
internal static class RegionTests
{
    public static void Run()
    {
        Harness.Group("--rect parsing");

        var rect = RegionCommand.ParseRect("10,20,30,40");
        Harness.Equal("x", 10, rect.X);
        Harness.Equal("y", 20, rect.Y);
        Harness.Equal("w", 30, rect.Width);
        Harness.Equal("h", 40, rect.Height);

        var negative = RegionCommand.ParseRect(" -5 , -6 , 7 , 8 ");
        Harness.Equal("negative origin is allowed (second monitor)", -5, negative.X);
        Harness.Equal("whitespace is trimmed", 7, negative.Width);

        Harness.Throws<ArgumentException>("three numbers are rejected", () => RegionCommand.ParseRect("1,2,3"));
        Harness.Throws<ArgumentException>("five numbers are rejected", () => RegionCommand.ParseRect("1,2,3,4,5"));
        Harness.Throws<ArgumentException>("a non-integer is rejected", () => RegionCommand.ParseRect("a,2,3,4"));
        Harness.Throws<ArgumentException>("a zero width is rejected", () => RegionCommand.ParseRect("1,2,0,4"));
        Harness.Throws<ArgumentException>("a negative height is rejected", () => RegionCommand.ParseRect("1,2,3,-4"));

        Harness.Group("screen rectangle -> window space");

        // A window at (100,200), 400x300, whose client area starts at (108,240) and is 384x252.
        var window = new ScreenRect(100, 200, 400, 300);
        var client = new ScreenRect(108, 240, 384, 252);

        var inside = RegionCommand.Classify(new ScreenRect(150, 300, 60, 20), window, client);
        Harness.Equal("a rectangle inside the client area uses the client space", "client", inside.Space);
        Harness.Equal("...and is relative to the client origin", new ScreenRect(42, 60, 60, 20), inside.Region!.Value);

        var titleBar = RegionCommand.Classify(new ScreenRect(150, 205, 60, 20), window, client);
        Harness.Equal("the title bar falls back to the window space", "window", titleBar.Space);
        Harness.Equal("...and is relative to the window rectangle", new ScreenRect(50, 5, 60, 20), titleBar.Region!.Value);

        var exactClient = RegionCommand.Classify(client, window, client);
        Harness.Equal("exactly the client area is still the client space", "client", exactClient.Space);
        Harness.Equal("...at 0,0", new ScreenRect(0, 0, 384, 252), exactClient.Region!.Value);

        var oneWider = RegionCommand.Classify(new ScreenRect(108, 240, 385, 252), window, client);
        Harness.Equal("one pixel wider than the client falls back to the window space", "window", oneWider.Space);

        var spanning = RegionCommand.Classify(new ScreenRect(90, 300, 60, 20), window, client);
        Harness.Equal("a rectangle crossing the window edge has no usable space", null, spanning.Space);
        Harness.Equal("...and no region", null, spanning.Region);

        var wholeScreen = RegionCommand.Classify(new ScreenRect(0, 0, 2560, 1440), window, client);
        Harness.Equal("a full-screen selection is not a window region", null, wholeScreen.Space);

        // A shell window (WebView/Electron) was measured at 0x0 client area: ClientBounds then
        // hands back the window rectangle, so a region inside it reports "client" - probe will
        // reject it against the real 0x0 client area rather than read the wrong pixels.
        var shell = RegionCommand.Classify(new ScreenRect(150, 300, 60, 20), window, window);
        Harness.Equal("a client-less window falls back to the window rectangle", "client", shell.Space);

        Harness.Group("suggested command");

        var placement = new RegionPlacement(
            new WindowInfo { Handle = new IntPtr(0x1234), Title = "记事本" }, "client", new ScreenRect(10, 20, 30, 40), null);
        Harness.Equal("client space needs no flag",
            "KeyMouse probe --hwnd 0x1234 --region 10,20,30,40", RegionCommand.SuggestedProbe(placement));

        var windowPlacement = new RegionPlacement(
            new WindowInfo { Handle = new IntPtr(0xABCD), Title = "记事本" }, "window", new ScreenRect(1, 2, 3, 4), null);
        Harness.Equal("window space carries --space window",
            "KeyMouse probe --hwnd 0xABCD --space window --region 1,2,3,4", RegionCommand.SuggestedProbe(windowPlacement));

        var unusable = new RegionPlacement(null, null, null, "选区没有落在窗口里");
        Harness.Equal("no space means no command to suggest", null, RegionCommand.SuggestedProbe(unusable));
    }
}
