namespace KeyMouse.Tests;

internal static class NativeInputFailureTests
{
    internal static void Run()
    {
        // Only the returned count goes through the real accounting path; no Win32 input call.
        using (ExecutionMode.Begin(false))
        {
            void Refused(uint sent, int error)
            {
                string message = "";
                try { NativeInput.AccountSend(2, sent, error); }
                catch (InvalidOperationException ex) { message = ex.Message; }
                Harness.Check($"{sent}/2 send reports its captured error {error}", message.Contains($"{sent}/2") && message.Contains($"Win32 错误 {error}"));
            }
            Refused(0, 5); Refused(1, 0);
            NativeInput.AccountSend(2, 2, 0);
            Harness.Equal("only actually injected events counted", 3L, ExecutionMode.InjectedEvents);
        }

        static (string Trace, string? Error) Drive(int failDown, int failUp, bool failWait = false)
        {
            List<string> trace = [];
            try
            {
                NativeInput.Chord([(1, false), (2, true), (3, false)], 0, (vk, ext, up) =>
                {
                    string ev = $"{(up ? "up" : "down")}{vk}{(ext ? "e" : "")}";
                    trace.Add(ev);
                    if (vk == (up ? failUp : failDown)) throw new IOException(ev);
                }, ms => { trace.Add($"wait{ms}"); if (failWait) throw new OperationCanceledException("stop"); });
                return (string.Join(' ', trace), null);
            }
            catch (Exception ex) { return (string.Join(' ', trace), ex.Message); }
        }
        Harness.Check("normal chord releases extended keys in reverse", Drive(-1, -1) == ("down1 down2e down3 wait1 up3 up2e up1", null));
        Harness.Check("first refused down emits no spurious up", Drive(1, -1) == ("down1", "down1"));
        Harness.Check("partial chord releases only accepted keys and preserves first failure", Drive(3, 2) == ("down1 down2e down3 up2e up1", "down3"));
        Harness.Check("a failed up does not strand remaining modifiers", Drive(-1, 3) == ("down1 down2e down3 wait1 up3 up2e up1", "up3"));
        Harness.Check("cancellation during hold still releases all accepted keys", Drive(-1, -1, true) == ("down1 down2e down3 wait1 up3 up2e up1", "stop"));

        int next = 0, observed = 0;
        var chain = new NativeHooks.HookDelivery((code, message, data) => { next++; return (IntPtr)31; });
        var callback = chain.Observe((_, _) => { observed++; throw new IOException("capture failed"); });
        Harness.Check("negative hook code passes straight through", callback(-1, 0, 0) == 31 && observed == 0 && next == 1);
        Harness.Check("failed hook observer cannot break the native chain", callback(0, 0, 0) == 31 && next == 2);
        Harness.Check("pending failure forwards subsequent events without calling the broken observer", callback(0, 0, 0) == 31 && observed == 1 && next == 3);
        Harness.Throws<IOException>("managed message pump observes original failure for safe cleanup", chain.ThrowIfFailed);
    }
}
