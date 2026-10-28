namespace KeyMouse.Windowing;
internal sealed class FocusResult
{
    // 是否成功成为前台窗口
    public bool Ok { get; init; } 
    // 尝试次数
    public int Attempts { get; init; }
    // 详细描述
    public string Detail { get; init; } = ""; 
}


internal static class WindowFocus
{
    // 默认尝试次数
    public const int DefaultAttempts = 3; 
    // 每次调用 SetForegroundWindow 后等待的毫秒数，给系统一些时间来处理前台切换
    private const int WaitAfterCallMs = 120; 
    // 检测指定窗口是否是前台窗口
    // 用 Root 是为了处理子窗口/控件的情况：只要它们属于同一个顶层窗口，就算目标在前台
    public static bool IsForeground(IntPtr h) =>
        NativeWindow.Root(NativeWindow.GetForegroundWindow()) == NativeWindow.Root(h);
    // 
    public static FocusResult Focus(IntPtr h, int maxAttempts = DefaultAttempts)
    {
        
        if (ExecutionMode.DryRun)
            // 干跑模式不应该真的动桌面，所以直接报告“本来会聚焦”。
            return new FocusResult { Ok = true, Attempts = 0, Detail = "演练模式：本来会聚焦" };

        if (IsForeground(h))
            // 目标已经是前台窗口，不需要再尝试聚焦
            return new FocusResult { Ok = true, Attempts = 0, Detail = "已经是前台窗口" };

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // 尝试把窗口线程直接添加到前台线程的输入队列中
            uint attached = NativeWindow.AttachToForegroundThread();
            try
            {
                // 三次尝试:
                // 第1次: 只调 SetForegroundWindow（温和路径）。
                // 第2次: 先 BringWindowToTop 再 SetForegroundWindow。
                // 第3次: BringWindowToTop + SetForegroundWindow 后再补一次 BringWindowToTop。
                if (attempt > 1) NativeWindow.BringWindowToTop(h);
                NativeWindow.SetForegroundWindow(h);
                if (attempt == maxAttempts) NativeWindow.BringWindowToTop(h);
            }
            finally
            {   // 无论成功失败，都要 DetachFromThread，避免影响后续操作。
                NativeWindow.DetachFromThread(attached);
            }

            // 等待系统处理前后台切换
            Thread.Sleep(WaitAfterCallMs);
            if (IsForeground(h))
            {
                // 成功成为前台窗口，返回成功结果
                string how = attached != 0 ? $"{attempt} 次尝试（含线程输入附着）" : $"{attempt} 次尝试";
                return new FocusResult { Ok = true, Attempts = attempt, Detail = $"经过 {how} 后成为前台窗口" };
            }
        }

        // 失败时给出当前前台窗口的标题，并提示 UIPI（用户界面特权隔离）问题。
        // 如果目标进程以管理员权限运行，而 KeyMouse 不是，那么前台切换和输入注入都会被系统拦截。
        // 解决方法是让 KeyMouse 也以管理员身份运行。
        IntPtr fg = NativeWindow.GetForegroundWindow();
        return new FocusResult
        {
            Ok = false,
            Attempts = maxAttempts,
            Detail = $"尝试 {maxAttempts} 次仍未成为前台窗口（当前前台是「{NativeWindow.GetTitle(fg)}」）" +
                     "；若目标以管理员身份运行，请同样以管理员身份运行 KeyMouse（UIPI 会挡住前台切换与输入注入）"
        };
    }
}

// 一个带退出码的自定义异常类
internal sealed class CommandFailure : Exception
{
    public int Code { get; }

    public CommandFailure(int code, string message) : base(message) => Code = code;
}
