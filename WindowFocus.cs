namespace KeyMouse;

internal sealed class FocusResult
{
    public bool Ok { get; init; }
    public int Attempts { get; init; }
    public string Detail { get; init; } = "";
}

/// <summary>
/// Gentle focus: SetForegroundWindow (+ BringWindowToTop from the second attempt on),
/// then re-read the real foreground window. No thread-input attachment, no synthetic
/// Alt key, no retry storms - three attempts and out.
/// </summary>
internal static class WindowFocus
{
    public const int DefaultAttempts = 3;
    private const int WaitAfterCallMs = 120;

    public static bool IsForeground(IntPtr h) =>
        NativeWindow.Root(NativeWindow.GetForegroundWindow()) == NativeWindow.Root(h);

    public static FocusResult Focus(IntPtr h, int maxAttempts = DefaultAttempts)
    {
        if (IsForeground(h))
            return new FocusResult { Ok = true, Attempts = 0, Detail = "already foreground" };

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1) NativeWindow.BringWindowToTop(h);
            NativeWindow.SetForegroundWindow(h);
            Thread.Sleep(WaitAfterCallMs);

            if (IsForeground(h))
                return new FocusResult { Ok = true, Attempts = attempt, Detail = $"foreground after {attempt} gentle attempt(s)" };
        }

        IntPtr fg = NativeWindow.GetForegroundWindow();
        return new FocusResult
        {
            Ok = false,
            Attempts = maxAttempts,
            Detail = $"still not foreground after {maxAttempts} attempts (foreground is \"{NativeWindow.GetTitle(fg)}\")"
        };
    }
}

/// <summary>Aborts the command with a specific exit code and message. Nothing is sent on the way out.</summary>
internal sealed class CommandFailure : Exception
{
    public int Code { get; }

    public CommandFailure(int code, string message) : base(message) => Code = code;
}
