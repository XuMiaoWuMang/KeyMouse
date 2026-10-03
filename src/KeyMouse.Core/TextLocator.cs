using System.Text.Json.Serialization;

namespace KeyMouse;

/// <summary>One word the engine read, with the box it sits in (relative to the region).</summary>
internal sealed record WordInfo(string Text, double? Conf, int[] Rect);

/// <summary>
/// One line: its words, and the box that covers all of them. The box used to be the first word's,
/// which made `lines[].rect` quietly wrong for any line with more than one word.
/// </summary>
internal sealed record LineInfo(
    [property: JsonIgnore] string Key, string Text, int[] Rect, WordInfo[] Words);

/// <summary>Where an expected piece of text sits inside what was read.</summary>
internal sealed record TextMatch(int[] Rect, string LineText, string Matched, int Distance, double? Confidence);

/// <summary>
/// Finds text in what the engine read - and therefore *where on screen* it is, which is what turns
/// "read the screen" into "click the thing you saw".
///
/// The engine splits CJK into one word per character (保 存 取 消), so matching word by word would
/// never find "保存". The line is squashed into one string and every character remembers which word
/// it came from; that is what turns a character range back into a rectangle.
/// </summary>
internal static class TextLocator
{
    /// <summary>First (contains/exact) or best (fuzzy) match across the lines, or null.</summary>
    internal static TextMatch? Find(IReadOnlyList<LineInfo> lines, string expected, string mode, int maxErrors)
    {
        string wanted = TextPredicate.Squash(expected);
        if (wanted.Length == 0) return null;

        TextMatch? best = null;
        foreach (LineInfo line in lines)
        {
            TextMatch? match = FindInLine(line, wanted, mode, maxErrors);
            if (match is null) continue;
            if (mode != TextPredicate.Fuzzy) return match;
            if (best is null || match.Distance < best.Distance) best = match;
        }
        return best;
    }

    private static TextMatch? FindInLine(LineInfo line, string wanted, string mode, int maxErrors)
    {
        (string text, List<int> owner) = Squash(line);
        if (text.Length == 0) return null;

        switch (mode)
        {
            case TextPredicate.Contains:
            {
                int at = text.IndexOf(wanted, StringComparison.Ordinal);
                return at < 0 ? null : Build(line, owner, at, wanted.Length, text[at..(at + wanted.Length)], 0);
            }

            case TextPredicate.Exact:
                return text == wanted ? Build(line, owner, 0, text.Length, text, 0) : null;

            case TextPredicate.Fuzzy:
            {
                // Windows a little shorter and longer than the expected text: a dropped or doubled
                // character is exactly the failure this budget exists for.
                TextMatch? best = null;
                int from = Math.Max(1, wanted.Length - maxErrors);
                int to = Math.Min(text.Length, wanted.Length + maxErrors);
                for (int length = from; length <= to; length++)
                {
                    for (int at = 0; at + length <= text.Length; at++)
                    {
                        int distance = TextPredicate.Distance(text.Substring(at, length), wanted);
                        if (distance > maxErrors) continue;
                        if (best is null || distance < best.Distance)
                        {
                            best = Build(line, owner, at, length, text.Substring(at, length), distance);
                        }
                    }
                }
                return best;
            }

            default:
                return null;
        }
    }

    /// <summary>The line's characters without whitespace, plus which word each character came from.</summary>
    private static (string Text, List<int> Owner) Squash(LineInfo line)
    {
        var builder = new System.Text.StringBuilder();
        var owner = new List<int>();
        for (int w = 0; w < line.Words.Length; w++)
        {
            foreach (char c in line.Words[w].Text)
            {
                if (char.IsWhiteSpace(c)) continue;
                builder.Append(c);
                owner.Add(w);
            }
        }
        return (builder.ToString(), owner);
    }

    private static TextMatch Build(LineInfo line, List<int> owner, int at, int length, string matched, int distance)
    {
        int first = owner[at];
        int last = owner[at + length - 1];
        var rects = new List<int[]>();
        double confidence = 0;
        for (int w = first; w <= last && w < line.Words.Length; w++)
        {
            rects.Add(line.Words[w].Rect);
            confidence += line.Words[w].Conf ?? 0;
        }
        int count = Math.Max(1, last - first + 1);
        return new TextMatch(Union(rects), line.Text, matched, distance, Math.Round(confidence / count, 1));
    }

    /// <summary>The box that covers all of them (the engine gives one box per word).</summary>
    internal static int[] Union(IEnumerable<int[]> rects)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (int[] rect in rects)
        {
            if (rect.Length < 4) continue;
            left = Math.Min(left, rect[0]);
            top = Math.Min(top, rect[1]);
            right = Math.Max(right, rect[0] + rect[2]);
            bottom = Math.Max(bottom, rect[1] + rect[3]);
        }
        if (left == int.MaxValue) return [0, 0, 0, 0];
        return [left, top, right - left, bottom - top];
    }
}
