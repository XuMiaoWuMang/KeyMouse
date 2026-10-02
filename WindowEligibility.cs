namespace KeyMouse;

internal sealed class EligibilityVerdict
{
    public List<string> Problems { get; } = new();
    public List<string> Notes { get; } = new();
    public bool Ok => Problems.Count == 0;

    public string Summary => Ok
        ? (Notes.Count > 0 ? "usable (" + string.Join("; ", Notes) + ")" : "usable")
        : string.Join("; ", Problems);
}

/// <summary>
/// The v1.1 objective gate. Only checks that can be decided without guessing:
/// window still exists, is visible, is not minimized (unless --allow-restore),
/// is not DWM-cloaked and answers its message queue.
/// Heuristic liveness detection (Chromium husks, UIA, pixel diff) is v1.2.
/// </summary>
internal static class WindowEligibility
{
    /// <summary>
    /// <paramref name="current"/> is the window as it stands after the check (it differs from
    /// <paramref name="w"/> when --allow-restore actually restored it).
    /// </summary>
    public static EligibilityVerdict Check(WindowInfo w, bool allowRestore, out WindowInfo current, uint responseTimeoutMs = 500)
    {
        var verdict = new EligibilityVerdict();

        if (!NativeWindow.IsWindow(w.Handle))
        {
            verdict.Problems.Add("handle is no longer a window");
            current = w;
            return verdict;
        }

        if (w.Minimized && allowRestore && ExecutionMode.DryRun)
        {
            verdict.Notes.Add("dry-run: would restore from minimized");
        }
        else if (w.Minimized && allowRestore)
        {
            NativeWindow.ShowWindow(w.Handle, NativeWindow.SW_RESTORE);
            Thread.Sleep(300);
            w = WindowInfo.Capture(w.Handle);
            if (w.Minimized || !w.Visible)
                verdict.Problems.Add("--allow-restore could not bring the window back on screen");
            else
                verdict.Notes.Add("restored by --allow-restore (apps that keep their window hidden may paint blank)");
        }
        else if (w.Minimized)
        {
            verdict.Problems.Add("window is minimized - pass --allow-restore to let KeyMouse restore it");
        }

        if (!w.Visible)
            verdict.Problems.Add("window is hidden (tray / background window) - KeyMouse refuses hidden windows, bring it up first");
        if (w.Cloaked)
            verdict.Problems.Add("window is DWM-cloaked (suspended UWP app or on another virtual desktop)");

        long? response = w.ResponseProbed ? w.ResponseMs : NativeWindow.ResponseMs(w.Handle, responseTimeoutMs);
        current = w.WithResponse(response);

        if (response is null)
            verdict.Problems.Add($"window does not answer WM_NULL within {responseTimeoutMs} ms (not responding)");
        else if (response > 400)
            verdict.Notes.Add($"slow to respond ({response} ms)");

        return verdict;
    }
}
