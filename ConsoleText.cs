namespace KeyMouse;

/// <summary>
/// Console text helpers. A CJK glyph occupies two terminal cells but counts as one
/// char, so tables padded with string.PadRight drift as soon as the text is Chinese.
/// </summary>
internal static class ConsoleText
{
    /// <summary>Width of the string in terminal cells (CJK counts as two).</summary>
    public static int DisplayWidth(string text)
    {
        int width = 0;
        foreach (char c in text) width += IsWide(c) ? 2 : 1;
        return width;
    }

    /// <summary>Pads with spaces to the requested number of terminal cells.</summary>
    public static string Pad(string text, int width)
    {
        int current = DisplayWidth(text);
        return current >= width ? text : text + new string(' ', width - current);
    }

    /// <summary>Truncates to a number of terminal cells, appending '…' when it had to cut.</summary>
    public static string Truncate(string text, int width)
    {
        if (DisplayWidth(text) <= width) return text;

        int used = 0;
        var result = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            int cell = IsWide(c) ? 2 : 1;
            if (used + cell > width - 1) break;
            result.Append(c);
            used += cell;
        }
        return result.Append('…').ToString();
    }

    /// <summary>Rough East Asian Width: the CJK ranges are double width.</summary>
    private static bool IsWide(char c) =>
        (c >= 0x1100 && c <= 0x115F) ||     // Hangul Jamo
        (c >= 0x2E80 && c <= 0xA4CF) ||     // CJK radicals through Yi
        (c >= 0xAC00 && c <= 0xD7A3) ||     // Hangul syllables
        (c >= 0xF900 && c <= 0xFAFF) ||     // CJK compatibility ideographs
        (c >= 0xFE30 && c <= 0xFE6F) ||     // CJK compatibility forms
        (c >= 0xFF00 && c <= 0xFF60) ||     // fullwidth forms
        (c >= 0xFFE0 && c <= 0xFFE6);
}
