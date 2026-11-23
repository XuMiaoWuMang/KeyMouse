using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace KeyMouse.BackgroundProbe;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--fixture"]) return Fixture.Run(false);
        if (args is ["--fixture", "--minimized"]) return Fixture.Run(true);
        if (args.Length != 0 && args is not ["--cli", _]) return 2;
        string? cli = args.Length == 2 ? Path.GetFullPath(args[1]) : null;
        try { Run(false, cli); Run(true, cli); return 0; }
        catch (DesktopChanged ex) { Console.Error.WriteLine($"INCONCLUSIVE: {ex.Message}"); return 3; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static void Run(bool minimized, string? cli)
    {
        Console.WriteLine(minimized ? "=== minimized ===" : "=== non-foreground / normal ===");
        var baseline = Desktop.Sample();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        start.ArgumentList.Add("--fixture");
        if (minimized) start.ArgumentList.Add("--minimized");
        using var fixture = Process.Start(start) ?? throw new InvalidOperationException("Cannot start fixture");
        try
        {
            // UI Automation client on an MTA thread. Bound the entire experiment, including provider calls.
            Task.Run(() => Verify(fixture, baseline, minimized, cli)).WaitAsync(TimeSpan.FromSeconds(cli == null ? 20 : 90)).GetAwaiter().GetResult();
        }
        finally
        {
            fixture.StandardInput.Close(); // EOF tells the fixture to close both windows.
            if (!fixture.WaitForExit(2000)) { fixture.Kill(); fixture.WaitForExit(); }
        }
    }

    private static void Verify(Process fixture, (nint Foreground, Point Cursor) baseline, bool minimized, string? cli)
    {
        T Read<T>() => JsonSerializer.Deserialize<T>(fixture.StandardOutput.ReadLine() ??
            throw new EndOfStreamException("Fixture disconnected")) ?? throw new InvalidDataException();
        Snapshot Ask(string command = "state") { fixture.StandardInput.WriteLine(command); return Read<Snapshot>(); }
        var ready = Read<Ready>();
        var h = ready.Target;
        void Guard()
        {
            var current = Desktop.Sample();
            if (current.Foreground == h.Root || current.Foreground == ready.Witness.Root)
                throw new InvalidOperationException("FAIL: fixture took foreground");
            if (current != baseline) throw new DesktopChanged("desktop foreground or cursor changed; rerun on a quiet desktop");
        }
        Guard();
        var witness = new State("A[]Z", 0, 0, false);
        var initial = witness with { Minimized = minimized };
        if (Ask() != new Snapshot(initial, witness)) throw new InvalidOperationException("Unexpected initial fixture state");
        var pid = fixture.Id;
        const string replacement = "KeyMouse 后台验证";

        void Check(string name, Action act, State? expected)
        {
            Guard(); act();
            var deadline = Stopwatch.StartNew();
            Snapshot actual;
            do
            {
                actual = Ask(); Guard();
                if (actual.Witness != witness || actual.Target is { } t && (t.Activations != 0 || t.Minimized != minimized))
                    throw new InvalidOperationException($"FAIL {name}: fixture activation, window state change or witness mutation: {actual}");
                if (actual.Target == expected) break;
                Thread.Sleep(20); // Invoke may return before the Click event is processed. Never resend.
            } while (deadline.ElapsedMilliseconds < 1500);
            if (actual.Target != expected) throw new InvalidOperationException($"FAIL {name}: expected {expected}; got {actual}");
            Console.WriteLine($"PASS {name}: {(expected is null ? "target closed" : $"text='{expected.Text}', clicks={expected.Clicks}")}");
        }
        if (cli != null)
        {
            CliChecks.Verify(cli, pid, ready, initial, Check, cmd => Ask(cmd));
            Console.WriteLine("PASS CLI isolation: foreground, cursor, activation counters, window states and witness");
            return;
        }
        static void Expect(string result, string expected)
        {
            if (result != expected) throw new InvalidOperationException($"Expected {expected}; got {result}");
        }
        Check("WM_CHAR inserts at caret", () =>
        {
            foreach (char c in "后台") Desktop.EditMessage(pid, h, 0x0102, c, 1);
        }, initial with { Text = "A[后台]Z" });
        Check("VK_DELETE removes next character", () =>
        {
            Desktop.EditMessage(pid, h, 0x0100, 0x2E, 0x01530001); // WM_KEYDOWN, extended Delete
            Desktop.EditMessage(pid, h, 0x0101, 0x2E, unchecked((int)0xC1530001)); // WM_KEYUP
        }, initial with { Text = "A[后台Z" });
        var client = Desktop.NoFocusClient();
        var filled = initial with { Text = replacement };
        Check("ValuePattern replaces value", () => Expect(Desktop.Pattern(client, pid, h.Root, h.Edit, replacement), "dispatched"), filled);
        var clicked = filled with { Clicks = 1 };
        Check("InvokePattern raises Click", () => Expect(Desktop.Pattern(client, pid, h.Root, h.Button), "dispatched"), clicked);
        Check("button rejects ValuePattern", () => Expect(Desktop.Pattern(client, pid, h.Root, h.Button, "unexpected"), "unsupported"), clicked);
        Check("same PID, wrong parent rejected", () =>
            Expect(Desktop.Pattern(client, pid, h.Root, ready.Witness.Button), "invalid-target"), clicked);
        Check("stale target rejected", () =>
        {
            Ask("close-target");
            Expect(Desktop.Pattern(client, pid, h.Root, h.Button), "invalid-target");
        }, null);
        Console.WriteLine("PASS isolation: same foreground and cursor at checkpoints; zero fixture activations; witness unchanged");
    }
}
