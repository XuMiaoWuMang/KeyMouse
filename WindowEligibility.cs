namespace KeyMouse;

internal sealed class EligibilityVerdict
{
    public List<string> Problems { get; } = new();
    public List<string> Notes { get; } = new();
    public bool Ok => Problems.Count == 0;

    public string Summary => Ok
        ? (Notes.Count > 0 ? "可用（" + string.Join("；", Notes) + "）" : "可用")
        : string.Join("；", Problems);
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
            verdict.Problems.Add("句柄已不再是窗口");
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
                verdict.Problems.Add("--allow-restore 没能把窗口恢复到屏幕上");
            else
                verdict.Notes.Add("已按 --allow-restore 还原（把窗口藏在托盘的应用可能只画出一片空白）");
        }
        else if (w.Minimized)
        {
            verdict.Problems.Add("窗口已最小化——加 --allow-restore 允许 KeyMouse 还原它");
        }

        if (!w.Visible)
            verdict.Problems.Add("窗口是隐藏的（托盘/后台窗口）——KeyMouse 拒绝隐藏窗口，请先让它显示出来");
        if (w.Cloaked)
            verdict.Problems.Add("窗口被 DWM 遮盖（挂起的 UWP 应用，或位于其他虚拟桌面）");
        if (!w.Enabled)
            verdict.Problems.Add("窗口已被禁用（WS_DISABLED）——它设计上就忽略输入，通常意味着有模态对话框占着它");

        long? response = w.ResponseProbed ? w.ResponseMs : NativeWindow.ResponseMs(w.Handle, responseTimeoutMs);
        current = w.WithResponse(response);

        if (response is null)
            verdict.Problems.Add($"窗口在 {responseTimeoutMs}ms 内没有应答 WM_NULL（无响应）");
        else if (response > 400)
            verdict.Notes.Add($"响应偏慢（{response}ms）");

        return verdict;
    }
}
