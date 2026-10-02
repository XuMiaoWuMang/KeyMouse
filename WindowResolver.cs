namespace KeyMouse;

internal sealed class WindowResolution
{
    /// <summary>Every top-level window on the desktop at resolution time.</summary>
    public List<WindowInfo> All { get; } = new();

    /// <summary>Every top-level window the selector matched.</summary>
    public List<WindowInfo> Matched { get; } = new();

    /// <summary>The subset left after the preference tiers (visible first, unowned first).</summary>
    public List<WindowInfo> Considered { get; } = new();

    /// <summary>The subset that passed the eligibility gate.</summary>
    public List<WindowInfo> Usable { get; } = new();

    /// <summary>One formatted "window -> why it was refused" block per rejected candidate.</summary>
    public List<string> Rejections { get; } = new();
}

/// <summary>
/// The single place that turns a selector into usable windows: enumerate, narrow with the
/// preference tiers, then run the eligibility gate. `mouse`/`key` commands, `window inspect`
/// and the script `waitfor` / `waitgone` pseudo-commands all go through this, so they can
/// never disagree about what counts as a usable window.
/// </summary>
internal static class WindowResolver
{
    public static WindowResolution Resolve(WindowSelector selector, bool allowRestore)
    {
        var resolution = new WindowResolution();
        resolution.All.AddRange(WindowLocator.EnumerateTopLevel());
        resolution.Matched.AddRange(resolution.All.Where(selector.Matches));
        resolution.Considered.AddRange(Program.PreferCandidates(resolution.Matched));

        foreach (var candidate in resolution.Considered)
        {
            var verdict = WindowEligibility.Check(candidate, allowRestore, out var current);
            if (verdict.Ok) resolution.Usable.Add(current);
            else resolution.Rejections.Add($"  {current.Describe()}\n      -> {verdict.Summary}");
        }

        return resolution;
    }
}
