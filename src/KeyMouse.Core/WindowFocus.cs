namespace KeyMouse;

internal sealed class FocusResult
{
    public bool Ok { get; init; }
    public int Attempts { get; init; }
    public string Detail { get; init; } = "";
}

/// <summary>
/// Focus that is verified rather than assumed: SetForegroundWindow (with BringWindowToTop as the
/// attempts wear on), preceded by attaching our input queue to whatever holds the foreground - the
/// documented way to make the call legal without sending a keystroke anywhere. Then the **real**
/// foreground window is read back: the only evidence that counts. Three attempts and out.
/// </summary>
internal static class WindowFocus
{
    public const int DefaultAttempts = 3;
    private const int WaitAfterCallMs = 120;

    public static bool IsForeground(IntPtr h) =>
        NativeWindow.Root(NativeWindow.GetForegroundWindow()) == NativeWindow.Root(h);

    public static FocusResult Focus(IntPtr h, int maxAttempts = DefaultAttempts)
    {
        // A dry run must not touch the desktop either, so focus is reported as satisfied.
        if (ExecutionMode.DryRun)
            return new FocusResult { Ok = true, Attempts = 0, Detail = "演练模式：本来会聚焦" };

        if (IsForeground(h))
            return new FocusResult { Ok = true, Attempts = 0, Detail = "已经是前台窗口" };

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            // Measured: with another window holding the foreground, three plain SetForegroundWindow
            // attempts all failed (exit 5) - the foreground lock refuses a caller that does not own the
            // foreground. Attaching our input queue to that thread first makes the same call legal, and
            // it sends nothing anywhere, so it is tried before the gentle path gives up.
            uint attached = NativeWindow.AttachToForegroundThread();
            try
            {
                if (attempt > 1) NativeWindow.BringWindowToTop(h);
                NativeWindow.SetForegroundWindow(h);
                if (attempt == maxAttempts) NativeWindow.BringWindowToTop(h);
            }
            finally
            {
                NativeWindow.DetachFromThread(attached);
            }

            Thread.Sleep(WaitAfterCallMs);
            if (IsForeground(h))
            {
                string how = attached != 0 ? $"{attempt} 次尝试（含线程输入附着）" : $"{attempt} 次尝试";
                return new FocusResult { Ok = true, Attempts = attempt, Detail = $"经过 {how} 后成为前台窗口" };
            }
        }

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

/// <summary>Aborts the command with a specific exit code and message. Nothing is sent on the way out.</summary>
internal sealed class CommandFailure : Exception
{
    public int Code { get; }

    public CommandFailure(int code, string message) : base(message) => Code = code;
}
