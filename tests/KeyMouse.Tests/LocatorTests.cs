using KeyMouse;

namespace KeyMouse.Tests;

/// <summary>
/// Turning "what the engine read" into "where it is on screen". This is what lets a step click the
/// thing it saw, so the geometry is worth testing without a desktop: the engine hands out one box per
/// word (CJK arrives as one word per character), and a match has to be turned back into a rectangle.
/// </summary>
internal static class LocatorTests
{
    // 保存 取消 确定 - the boxes a real read produced (region-relative).
    private static readonly LineInfo[] Line =
    [
        new("1/1/1", "保存 取消 确定", [11, 7, 187, 23],
        [
            new WordInfo("保存", 93.5, [11, 7, 52, 22]),
            new WordInfo("取消", 91.0, [79, 7, 51, 22]),
            new WordInfo("确定", 88.0, [147, 7, 51, 23]),
        ]),
    ];

    public static void Run()
    {
        Harness.Group("locator: a match becomes a rectangle");

        TextMatch? single = TextLocator.Find(Line, "保存", TextPredicate.Contains, 0);
        Harness.Check("contains finds a word inside a line", single is not null);
        Harness.Sequence("...and returns that word's own box", ["11", "7", "52", "22"],
            (single?.Rect ?? []).Select(v => v.ToString()));
        Harness.Equal("...with the engine's confidence for it", 93.5, single?.Confidence);

        TextMatch? across = TextLocator.Find(Line, "取消确定", TextPredicate.Contains, 0);
        Harness.Sequence("a match that spans two words gets the union of their boxes",
            ["79", "7", "119", "23"], (across?.Rect ?? []).Select(v => v.ToString()));

        TextMatch? exact = TextLocator.Find(Line, "保存取消确定", TextPredicate.Exact, 0);
        Harness.Sequence("exact needs the whole line, whitespace ignored",
            ["11", "7", "187", "23"], (exact?.Rect ?? []).Select(v => v.ToString()));

        Harness.Check("exact refuses a line that is longer",
            TextLocator.Find(Line, "保存取消", TextPredicate.Exact, 0) is null);

        TextMatch? fuzzy = TextLocator.Find(Line, "保存取肖", TextPredicate.Fuzzy, 1);
        Harness.Check("fuzzy survives one wrong character", fuzzy is not null);
        Harness.Equal("...and says how far off it was", 1, fuzzy?.Distance);
        Harness.Sequence("...and still points at the right words",
            ["11", "7", "119", "22"], (fuzzy?.Rect ?? []).Select(v => v.ToString()));

        Harness.Check("a budget of zero refuses that same near miss",
            TextLocator.Find(Line, "保存取肖", TextPredicate.Fuzzy, 0) is null);
        Harness.Check("text that is not there is not found",
            TextLocator.Find(Line, "另存为", TextPredicate.Contains, 0) is null);
        Harness.Check("an empty expectation finds nothing (and does not match everything)",
            TextLocator.Find(Line, "   ", TextPredicate.Contains, 0) is null);

        Harness.Group("locator: boxes and lines");
        Harness.Sequence("a union covers every box",
            ["10", "20", "90", "60"], TextLocator.Union([[10, 20, 20, 20], [70, 40, 30, 40]]).Select(v => v.ToString()));
        Harness.Sequence("a union of nothing is an empty box",
            ["0", "0", "0", "0"], TextLocator.Union([]).Select(v => v.ToString()));
        Harness.Check("the line box of a multi-word line is its words' union, not the first word's",
            Line[0].Rect[2] == 187 && Line[0].Rect[2] != Line[0].Words[0].Rect[2]);
        Harness.Check("words and lines are still shaped for the JSON consumers",
            Line[0].Words[1].Text == "取消" && Line[0].Words[1].Conf == 91.0);
    }
}
