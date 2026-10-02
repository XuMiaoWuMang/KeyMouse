using System.Globalization;

namespace KeyMouse;

/// <summary>A window query built from the CLI selectors. All given constraints are ANDed.</summary>
internal sealed class WindowSelector
{
    public string? Title { get; init; }
    public string? TitleExact { get; init; }
    public string? ClassName { get; init; }
    public string? ProcessName { get; init; }
    public uint? ProcessId { get; init; }
    public IntPtr? Handle { get; init; }

    public bool Matches(WindowInfo w)
    {
        if (Handle is IntPtr h && w.Handle != h) return false;
        if (ProcessId is uint p && w.ProcessId != p) return false;
        if (ProcessName is { } pn && !w.ProcessName.Equals(pn, StringComparison.OrdinalIgnoreCase)) return false;
        if (ClassName is { } cn && !w.ClassName.Equals(cn, StringComparison.OrdinalIgnoreCase)) return false;
        if (TitleExact is { } te && !w.Title.Equals(te, StringComparison.OrdinalIgnoreCase)) return false;
        if (Title is { } t && !w.Title.Contains(t, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    public string Describe()
    {
        var parts = new List<string>();
        if (Handle is IntPtr h) parts.Add($"--hwnd 0x{h.ToInt64():X}");
        if (ProcessId is uint p) parts.Add($"--pid {p}");
        if (ProcessName is { } pn) parts.Add($"--process {pn}");
        if (ClassName is { } cn) parts.Add($"--class {cn}");
        if (TitleExact is { } te) parts.Add($"--title-exact \"{te}\"");
        else if (Title is { } t) parts.Add($"--title \"{t}\"");
        return parts.Count == 0 ? "(no selector)" : string.Join(" ", parts);
    }
}

internal static class WindowLocator
{
    public static List<WindowInfo> EnumerateTopLevel(uint probeTimeoutMs = 0)
    {
        var list = new List<WindowInfo>();
        NativeWindow.EnumWindows((h, _) =>
        {
            list.Add(WindowInfo.Capture(h, probeTimeoutMs));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static List<WindowInfo> Find(WindowSelector selector, uint probeTimeoutMs = 0) =>
        EnumerateTopLevel(probeTimeoutMs).Where(selector.Matches).ToList();

    /// <summary>Top-level ancestor of the window under a screen point.</summary>
    public static IntPtr WindowAt(int x, int y) =>
        NativeWindow.Root(NativeWindow.WindowFromPoint(new NativeWindow.POINT { X = x, Y = y }));

    public static (int X, int Y) ClientToScreen(IntPtr h, int x, int y)
    {
        var point = new NativeWindow.POINT { X = x, Y = y };
        if (!NativeWindow.ClientToScreen(h, ref point))
            throw new CommandFailure(1, "ClientToScreen failed for the target window");
        return (point.X, point.Y);
    }

    public static bool IsInsideClientArea(IntPtr h, int screenX, int screenY)
    {
        var point = new NativeWindow.POINT { X = screenX, Y = screenY };
        if (!NativeWindow.ScreenToClient(h, ref point)) return false;
        if (!NativeWindow.GetClientRect(h, out var client)) return false;
        return point.X >= 0 && point.Y >= 0 && point.X < client.Width && point.Y < client.Height;
    }

    public static IntPtr ParseHandle(string raw)
    {
        string v = raw.Trim();
        long value;
        if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                throw new ArgumentException($"'{raw}' is not a valid window handle");
        }
        else if (!long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                 !long.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
        {
            throw new ArgumentException($"'{raw}' is not a valid window handle");
        }
        return new IntPtr(value);
    }

    public static string NoMatchMessage(WindowSelector selector, int sampleSize = 10)
    {
        var sample = EnumerateTopLevel()
            .Where(w => w.Visible && w.Title.Length > 0)
            .Take(sampleSize)
            .ToList();
        string listing = sample.Count == 0
            ? "  (no visible titled windows on this desktop)"
            : string.Join("\n", sample.Select(w => "  " + w.Describe()));
        return $"no window matches {selector.Describe()}\n\nvisible windows right now:\n{listing}";
    }

    public static string CandidateTable(IEnumerable<WindowInfo> windows) =>
        string.Join("\n", windows.Select((w, i) => $"  [{i + 1}] {w.Describe()}"));
}
