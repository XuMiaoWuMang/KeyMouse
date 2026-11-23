using System.Diagnostics;
using System.Text.Json;

namespace KeyMouse.BackgroundProbe;

internal static class CliChecks
{
    internal static void Verify(string cli, int pid, Ready ready, State initial, Action<string, Action, State?> check, Action<string> prepare)
    {
        string Run(int code, string action, params string[] tail) => RunFor(pid, code, action, tail);
        string RunFor(int targetPid, int code, string action, params string[] tail)
        {
            var info = new ProcessStartInfo(cli) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "background", action, "--pid", targetPid.ToString(), "--hwnd", ready.Target.Root.ToString() }.Concat(tail))
                info.ArgumentList.Add(arg);
            using var p = Process.Start(info)!;
            var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(15000)) { p.Kill(); throw new TimeoutException(action); }
            if (p.ExitCode != code) throw new InvalidOperationException($"{action}: expected exit {code}; got {p.ExitCode}: {stderr.Result} {stdout.Result}");
            return stdout.Result;
        }
        string edit = ready.Target.Edit.ToString(), button = ready.Target.Button.ToString();
        check("list native controls", () =>
        {
            using var doc = JsonDocument.Parse(Run(0, "controls"));
            var rows = doc.RootElement.GetProperty("controls").EnumerateArray().ToArray();
            if (rows.Length != 2 || !rows.Any(c => c.GetProperty("id").GetString() == "Notes" && c.GetProperty("control").GetString() == $"0x{ready.Target.Edit:X}") ||
                !rows.Any(c => c.GetProperty("id").GetString() == "ApplyButton" && c.GetProperty("control").GetString() == $"0x{ready.Target.Button:X}"))
                throw new InvalidOperationException("Unexpected controls: " + doc.RootElement);
        }, initial);
        check("wrong PID", () => RunFor(Environment.ProcessId, 4, "controls"), initial);
        var inserted = initial with { Text = "A[后台🙂]Z" };
        check("insert at caret by ID", () => Run(0, "insert-text", "--id", "Notes", "--text", "后台🙂"), inserted);
        var selected = inserted with { Text = "A--hwndZ" };
        check("replace selection with literal option", () => { prepare("select-middle"); Run(0, "insert-text", "--control", edit, "--text", "--hwnd"); }, selected);
        var filled = selected with { Text = "含 空格" };
        check("replace entire value", () => Run(0, "set-value", "--control", edit, "--text", filled.Text), filled);
        var empty = filled with { Text = "" };
        check("clear value", () => Run(0, "set-value", "--control", edit, "--text", ""), empty);
        var clicked = empty with { Clicks = 1 };
        check("invoke exactly once", () => Run(0, "invoke", "--id", "ApplyButton"), clicked);
        check("wrong parent", () => Run(4, "invoke", "--control", ready.Witness.Button.ToString()), clicked);
        check("wrong control kind", () => Run(4, "invoke", "--control", edit), clicked);
        check("missing id", () => Run(3, "invoke", "--id", "absent"), clicked);
        check("ambiguous id", () => { prepare("duplicate-id"); Run(3, "invoke", "--id", "ApplyButton"); }, clicked);
        check("disabled button", () => { prepare("disable-button"); Run(4, "invoke", "--control", button); }, clicked);
        var truncated = clicked with { Text = "X" };
        check("partial write reports unconfirmed, no retry", () =>
        {
            prepare("limit-length");
            Run(1, "insert-text", "--control", edit, "--text", "XYZ");
        }, truncated);
        check("read-only edit", () => { prepare("readonly"); Run(4, "insert-text", "--control", edit, "--text", "Y"); }, truncated);
        check("password edit not listed", () =>
        {
            prepare("password");
            using var doc = JsonDocument.Parse(Run(0, "controls"));
            if (doc.RootElement.GetProperty("controls").EnumerateArray().Any(c => c.GetProperty("id").GetString() == "Notes"))
                throw new InvalidOperationException("Password edit exposed");
        }, truncated);
        check("closed target", () => { prepare("close-target"); Run(4, "invoke", "--control", button); }, null);
    }
}
