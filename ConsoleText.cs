using System.Runtime.InteropServices;
using System.Text;

namespace KeyMouse;

/// <summary>
/// Console text helpers. A CJK glyph occupies two terminal cells but counts as one
/// char, so tables padded with string.PadRight drift as soon as the text is Chinese.
/// </summary>
internal static class ConsoleText
{
    [DllImport("kernel32.dll")]
    private static extern uint GetConsoleOutputCP();

    /// <summary>
    /// Makes stdout speak the console's own code page.
    ///
    /// PowerShell decodes a native command's output with [Console]::OutputEncoding, which
    /// follows the console code page. .NET, however, defaults to UTF-8 whenever stdout is
    /// redirected - so capturing our output in a cp936 console decoded UTF-8 bytes as GBK and
    /// turned "已移动到" into "宸茬Щ鍔ㄥ埌". Writing what the console expects fixes both the
    /// captured and the on-screen case.
    ///
    /// With no console attached (CI, a plain pipe) GetConsoleOutputCP returns 0 and .NET's
    /// UTF-8 default is already the right answer, so nothing is changed.
    /// </summary>
    public static void ConfigureOutputEncoding()
    {
        try
        {
            uint codePage = GetConsoleOutputCP();
            if (codePage == 0 || codePage == Console.OutputEncoding.CodePage) return;

            // The legacy code pages are not part of .NET's default set; the provider ships
            // with the runtime, but has to be registered before GetEncoding can see them.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.GetEncoding((int)codePage);
        }
        catch
        {
            // Display encoding is never worth taking the program down for; the default stays.
        }
    }

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
