namespace KeyMouse;

/// <summary>
/// The comparison rule behind `wait-text`: does what the engine read count as the text the caller is
/// waiting for?
///
/// Whitespace is ignored on purpose, on both sides. The engine splits CJK into one word per
/// character and puts spaces between them (`你 好 ， 世 界`), so a caller writing `你好，世界` - the
/// text that is actually on screen - would otherwise never match. That is a *comparison* rule: the
/// read itself stays exactly what the engine produced, and `probe` still prints it verbatim.
///
/// Exactness is a budget the caller declares, not a threshold the tool invents:
/// `exact` demands every character, `fuzzy` allows a stated number of edits (measured on real
/// screen text: single-character errors like `按钮` -> `近钮` are the common failure, so one error is
/// usually the right budget for a UI string).
/// </summary>
internal static class TextPredicate
{
    internal const string Contains = "contains";
    internal const string Exact = "exact";
    internal const string Fuzzy = "fuzzy";

    internal static readonly string[] Modes = [Contains, Exact, Fuzzy];

    /// <summary>A mode the loader accepts; anything else is a usage error, not a non-match.</summary>
    internal static bool IsKnownMode(string mode) => Modes.Contains(mode);

    /// <summary>
    /// True when <paramref name="actual"/> (what was read) satisfies <paramref name="expected"/>
    /// under <paramref name="mode"/>. <paramref name="detail"/> explains a near miss, which is what
    /// makes a timeout debuggable: "read X, wanted Y, 3 edits away" instead of "timed out".
    /// </summary>
    internal static bool Matches(string actual, string expected, string mode, int maxErrors, out string detail)
    {
        string a = Squash(actual);
        string e = Squash(expected);
        switch (mode)
        {
            case Contains:
                if (a.Contains(e, StringComparison.Ordinal)) { detail = ""; return true; }
                detail = $"读到「{Clip(a)}」，里面没有「{Clip(e)}」";
                return false;

            case Exact:
                if (a == e) { detail = ""; return true; }
                detail = $"读到「{Clip(a)}」，与期望的「{Clip(e)}」差 {Distance(a, e)} 个字符";
                return false;

            case Fuzzy:
                int distance = Distance(a, e);
                if (distance <= maxErrors) { detail = ""; return true; }
                detail = $"读到「{Clip(a)}」，与期望差 {distance} 个字符（预算 {maxErrors}）";
                return false;

            default:
                detail = $"未知的匹配方式 '{mode}'（可用：{string.Join(" | ", Modes)}）";
                return false;
        }
    }

    /// <summary>Whitespace-insensitive form used for every comparison (see the class comment).</summary>
    internal static string Squash(string text)
    {
        Span<char> buffer = text.Length <= 256 ? stackalloc char[text.Length] : new char[text.Length];
        int count = 0;
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c)) buffer[count++] = c;
        }
        return new string(buffer[..count]);
    }

    /// <summary>Levenshtein distance: the edit budget is stated in characters, so it is measured in them.</summary>
    internal static int Distance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    private static string Clip(string text) => text.Length <= 60 ? text : text[..57] + "…";
}
