namespace KeyMouse.Windowing;
// 检测窗口是否可以接受模拟输入
internal sealed class EligibilityVerdict
{
    // 导致窗口不可用的硬性问题。
    public List<string> Problems { get; } = new();
    // 不影响可用性、但值得提示的备注（例如“响应偏慢”）。
    public List<string> Notes { get; } = new();
    // 只要 Problems 为空，就认为窗口可用。
    public bool Ok => Problems.Count == 0;

    // 返回一个简短的、可读的总结字符串，描述窗口的可用性状态。
    // 1. 可用且有备注："可用（备注1；备注2）"
    // 2. 可用且无备注："可用"
    // 3. 不可用：把所有问题用 “；” 连接起来。
    public string Summary => Ok
        ? (Notes.Count > 0 ? "可用（" + string.Join("；", Notes) + "）" : "可用")
        : string.Join("；", Problems);
}

internal static class WindowEligibility
{
    // 检测指定窗口是否可以接受模拟输入，并返回一个 EligibilityVerdict 对象，描述窗口的可用性状态。
    public static EligibilityVerdict Check(WindowInfo w, bool allowRestore, out WindowInfo current, uint responseTimeoutMs = 500)
    {
        var verdict = new EligibilityVerdict();

        // 检测是否是有效的窗口句柄
        if (!NativeWindow.IsWindow(w.Handle))
        {
            verdict.Problems.Add("句柄已不再是窗口");
            current = w;
            return verdict;
        }

        // 检测窗口是否最小化，并根据 allowRestore 参数决定是否尝试还原窗口。
        if (w.Minimized && allowRestore && ExecutionMode.DryRun)
        {
            // 干跑模式：不实际还原，只加一条备注 "dry-run: would restore from minimized"。
            verdict.Notes.Add("dry-run: would restore from minimized");
        }
        else if (w.Minimized && allowRestore)
        {
            // 实际还原：调用 ShowWindow(SW_RESTORE)，等待 300ms，重新捕获窗口信息。
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
            // 添加问题："窗口已最小化——加 --allow-restore 允许 KeyMouse 还原它"。
            verdict.Problems.Add("窗口已最小化——加 --allow-restore 允许 KeyMouse 还原它");
        }

        // 是否可见（简单判断是任务栏有没有程序图标
        if (!w.Visible)
            verdict.Problems.Add("窗口是隐藏的（托盘/后台窗口）——KeyMouse 拒绝隐藏窗口，请先让它显示出来");
        // 检测窗口是否被 DWM 遮盖（挂起的 UWP 应用，或位于其他虚拟桌面）。
        if (w.Cloaked)
            verdict.Problems.Add("窗口被 DWM 遮盖（挂起的 UWP 应用，或位于其他虚拟桌面）");
        // 检测窗口是否被禁用（WS_DISABLED），通常意味着有模态对话框占着它。
        if (!w.Enabled)
            verdict.Problems.Add("窗口已被禁用（WS_DISABLED）——它设计上就忽略输入，通常意味着有模态对话框占着它");

        // 如果之前已经探测过响应时间（ResponseProbed），直接复用；否则调用 NativeWindow.ResponseMs 发送 WM_NULL 并测量响应时间。
        long? response = w.ResponseProbed ? w.ResponseMs : NativeWindow.ResponseMs(w.Handle, responseTimeoutMs);
        // 将响应信息更新到 current 窗口信息中，以便外部获取最新的窗口状态。
        current = w.WithResponse(response);

        
        if (response is null)
            // response 为 null，需要添加Problems
            verdict.Problems.Add($"窗口在 {responseTimeoutMs}ms 内没有应答 WM_NULL（无响应）");
        else if (response > 400)
            // response 不为 null 但是响应时间大于 400ms，添加 Notes
            verdict.Notes.Add($"响应偏慢（{response}ms）");

        return verdict;
    }
}
