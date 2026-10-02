namespace KeyMouse.Tests;

/// <summary>Window selection and state reporting - the two places where real bugs shipped,
/// so they get covered with synthetic windows (no desktop needed).</summary>
internal static class WindowTests
{
    private static int _nextHandle = 0x1000;

    private static WindowInfo Win(
        string title,
        string className = "TestClass",
        uint pid = 1,
        IntPtr? owner = null,
        bool visible = true,
        bool minimized = false,
        bool cloaked = false,
        bool enabled = true,
        long? responseMs = 0,
        bool probed = true)
    {
        return new WindowInfo
        {
            Handle = new IntPtr(_nextHandle++),
            Title = title,
            ClassName = className,
            ProcessId = pid,
            ProcessName = "test",
            Visible = visible,
            Minimized = minimized,
            Cloaked = cloaked,
            Owner = owner ?? IntPtr.Zero,
            Enabled = enabled,
            ResponseMs = responseMs,
            ResponseProbed = probed
        };
    }

    public static void Run()
    {
        Selectors();
        Preference();
        StateSummary();
    }

    private static void Selectors()
    {
        Harness.Group("window selector matching");
        var notepad = Win("无标题 - Notepad", className: "Notepad", pid: 42);

        Harness.Check("title substring, case-insensitive", new WindowSelector { Title = "notepad" }.Matches(notepad));
        Harness.Check("process must match", !new WindowSelector { Title = "notepad", ProcessName = "other" }.Matches(notepad));
        Harness.Check("class match is exact", new WindowSelector { ClassName = "Notepad" }.Matches(notepad));
        Harness.Check("class prefix does not match", !new WindowSelector { ClassName = "Note" }.Matches(notepad));
        Harness.Check("title-exact rejects a substring", !new WindowSelector { TitleExact = "notepad" }.Matches(notepad));
        Harness.Check("title-exact accepts the full title", new WindowSelector { TitleExact = "无标题 - Notepad" }.Matches(notepad));
        Harness.Check("pid matches", new WindowSelector { ProcessId = 42 }.Matches(notepad));
        Harness.Check("pid mismatch rejects", !new WindowSelector { ProcessId = 43 }.Matches(notepad));
        Harness.Check("handle matches", new WindowSelector { Handle = notepad.Handle }.Matches(notepad));
        Harness.Check("handle mismatch rejects", !new WindowSelector { Handle = IntPtr.Zero }.Matches(notepad));
    }

    private static void Preference()
    {
        Harness.Group("candidate preference");

        var main = Win("main");
        var popup = Win("popup", owner: main.Handle);   // WinUI bridge / dialog shape
        var hidden = Win("hidden", visible: false);
        var hiddenOwned = Win("hidden-owned", visible: false, owner: main.Handle);
        var otherMain = Win("other-main");

        Harness.Sequence("unowned wins over owned",
            new[] { "main" }, Program.PreferCandidates(new[] { main, popup }).Select(w => w.Title));

        Harness.Sequence("order is preserved",
            new[] { "main", "other-main" }, Program.PreferCandidates(new[] { main, popup, otherMain }).Select(w => w.Title));

        Harness.Sequence("hidden windows only win when nothing is visible",
            new[] { "hidden" }, Program.PreferCandidates(new[] { hidden }).Select(w => w.Title));

        Harness.Sequence("visible beats hidden",
            new[] { "main" }, Program.PreferCandidates(new[] { hidden, main }).Select(w => w.Title));

        Harness.Sequence("hidden owned window still wins when alone",
            new[] { "hidden-owned" }, Program.PreferCandidates(new[] { hiddenOwned }).Select(w => w.Title));

        Harness.Sequence("an owned window is still usable on its own",
            new[] { "popup" }, Program.PreferCandidates(new[] { popup }).Select(w => w.Title));

        Harness.Sequence("two unowned windows stay ambiguous",
            new[] { "main", "other-main" }, Program.PreferCandidates(new[] { main, otherMain }).Select(w => w.Title));
    }

    private static void StateSummary()
    {
        Harness.Group("window state summary");

        Harness.Check("an unprobed window is not reported as dead",
            Win("x", responseMs: null, probed: false).StateSummary.Contains("未探测"));

        Harness.Check("a timed-out probe is reported as not responding",
            Win("x", responseMs: null, probed: true).StateSummary.Contains("无响应"));

        Harness.Check("a probed window reports its latency",
            Win("x", responseMs: 3, probed: true).StateSummary.Contains("响应=3ms"));

        Harness.Check("an owner is surfaced",
            Win("x", owner: new IntPtr(0xABCD)).StateSummary.Contains("属主=0xABCD"));

        Harness.Check("a hidden window is shouted about",
            Win("x", visible: false).StateSummary.Contains("隐藏"));

        Harness.Check("a disabled window is shouted about",
            Win("x", enabled: false).StateSummary.Contains("已禁用"));

        Harness.Check("an enabled window is not marked disabled",
            !Win("x").StateSummary.Contains("已禁用"));

        Harness.Check("a plain window reads as visible",
            Win("x").StateSummary.StartsWith("可见"));
    }
}
