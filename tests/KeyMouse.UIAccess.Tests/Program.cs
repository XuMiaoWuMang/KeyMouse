using System.Diagnostics;
using System.Text.Json;

if (args is ["--job-parent"])
{
    KeyMouse.Cli.UiAccessJob.Install();
    using var descendant = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false, ArgumentList = { "--job-child" },
    })!;
    Console.WriteLine(descendant.Id);
    Console.Out.Flush();
    Thread.Sleep(Timeout.Infinite);
    return;
}
if (args is ["--job-child"]) { Thread.Sleep(Timeout.Infinite); return; }
string cli = Path.GetFullPath(args.Single());
int passed = 0;
Check("rejects relative helper before launch", ["--uiaccess-run", "relative.exe", "--version"], 2, "absolute");
Check("rejects untrusted absolute helper before launch", ["--uiaccess-run", cli, "--version"], 2, "Program Files");
Check("rejects malformed internal handshake", ["--uiaccess-helper", "wrong", "nonce", "1"], 2, "handshake");
var diagnostic = Run(["--capabilities"]);
if (diagnostic.Code != 0) throw new Exception("Token diagnostic failed: " + diagnostic.Error);
using (JsonDocument doc = JsonDocument.Parse(diagnostic.Output))
{
    var root = doc.RootElement;
    if (root.GetProperty("processId").GetInt32() <= 0 || root.GetProperty("sessionId").GetInt32() < 0 ||
        root.GetProperty("integrityRid").GetInt32() < 0 || root.GetProperty("integrityLevel").GetString() is null ||
        root.TryGetProperty("UserSid", out _) || root.TryGetProperty("userSid", out _))
        throw new Exception("Invalid or privacy-leaking capability diagnostic");
    Pass("reports real token capability without user identity");
    if (!root.GetProperty("uiAccess").GetBoolean())
        Check("never falls back when TokenUIAccess is zero",
            ["--uiaccess-helper", "KeyMouse.UIAccess." + new string('A', 48), new string('B', 64), Environment.ProcessId.ToString()], 5, "TokenUIAccess is 0");
}
Check("ordinary CLI remains operational", ["--version"], 0, "KeyMouse", stdout: true);
// Exercise the production framing over actual Windows pipes without requesting a UIAccess token.
string pipeName = "KeyMouse.UIAccess.Test." + Guid.NewGuid().ToString("N");
using (var server = KeyMouse.Cli.UiAccessCommand.CreateUserPipe(pipeName))
using (var client = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous))
{
    var security = System.IO.Pipes.PipesAclExtensions.GetAccessControl(server);
    var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
    var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
    if (!security.AreAccessRulesProtected || rules.Count != 1 ||
        rules[0] is not System.IO.Pipes.PipeAccessRule rule || !rule.IdentityReference.Equals(user) ||
        rule.AccessControlType != System.Security.AccessControl.AccessControlType.Allow ||
        (rule.PipeAccessRights & System.IO.Pipes.PipeAccessRights.ReadWrite) != System.IO.Pipes.PipeAccessRights.ReadWrite)
        throw new Exception("Pipe DACL must grant only current SID, with no inherited/default principals");
    Pass("production pipe DACL grants only current SID independent of elevation");
    Task connect = client.ConnectAsync(2000);
    await server.WaitForConnectionAsync();
    await connect;
    var sender = new KeyMouse.Cli.UiAccessCommand.Channel(server);
    var receiver = new KeyMouse.Cli.UiAccessCommand.Channel(client);
    foreach (var frame in new[] {
        new KeyMouse.Cli.UiAccessCommand.Frame("stdout", Text: "中文 output\nno newline"),
        new KeyMouse.Cli.UiAccessCommand.Frame("stderr", Text: "failure\r\n"),
        new KeyMouse.Cli.UiAccessCommand.Frame("pause", Paused: true),
        new KeyMouse.Cli.UiAccessCommand.Frame("cancel"),
        new KeyMouse.Cli.UiAccessCommand.Frame("exit", ExitCode: 1223),
    })
    {
        using var deadline = new CancellationTokenSource(2000);
        Task<KeyMouse.Cli.UiAccessCommand.Frame> received = receiver.ReadAsync(deadline.Token);
        await Task.Run(() => sender.Write(frame));
        if (await received != frame) throw new Exception("Protocol frame did not round trip");
    }
    Pass("actual same-user pipe preserves stdout stderr pause cancel and exit ordering");
    Task<KeyMouse.Cli.UiAccessCommand.Frame> oversized = receiver.ReadAsync(CancellationToken.None);
    await server.WriteAsync(BitConverter.GetBytes(1024 * 1024 + 1));
    try { await oversized; throw new Exception("Oversized frame accepted"); }
    catch (IOException) { Pass("rejects oversized incoming frames before allocation"); }
}
using (var canceled = new CancellationTokenSource())
using (var early = new KeyMouse.Cli.UiAccessCommand.BridgeControls(new StringReader("{\"type\":\"cancel\"}\n"), canceled))
{
    early.Start();
    await early.Completion;
    using var payload = new MemoryStream();
    try { early.SendJob(new KeyMouse.Cli.UiAccessCommand.Channel(payload), new KeyMouse.Cli.UiAccessCommand.Frame("job")); throw new Exception("Early canceled job dispatched"); }
    catch (OperationCanceledException)
    {
        if (payload.Length != 0) throw new Exception("Early cancel wrote job bytes");
        Pass("startup cancel prevents job dispatch entirely");
    }
}
var jobInfo = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, ArgumentList = { "--job-parent" } };
using (var jobParent = Process.Start(jobInfo)!)
{
    string? childLine = await jobParent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
    if (!int.TryParse(childLine, out int childPid)) throw new Exception("Job child PID unavailable");
    using Process jobChild = Process.GetProcessById(childPid);
    jobParent.Kill();
    if (!jobParent.WaitForExit(5000) || !jobChild.WaitForExit(5000))
    {
        try { jobChild.Kill(); } catch (InvalidOperationException) { }
        throw new Exception("Kill-on-close job did not terminate descendant");
    }
    Pass("helper lifetime job kills descendant when its process exits abruptly");
}
Console.WriteLine($"{passed} UIAccess regressions passed.");

void Check(string name, string[] arguments, int code, string text, bool stdout = false)
{
    var result = Run(arguments);
    if (result.Code != code || !(stdout ? result.Output : result.Error).Contains(text, StringComparison.OrdinalIgnoreCase))
        throw new Exception($"{name}: expected {code}/{text}, got {result.Code}: {result.Output} {result.Error}");
    Pass(name);
}
void Pass(string name) { passed++; Console.WriteLine("PASS " + name); }
(int Code, string Output, string Error) Run(string[] arguments)
{
    var info = new ProcessStartInfo(cli)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    info.Environment["KEYMOUSE_OUTPUT_ENCODING"] = "utf-8";
    foreach (string argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!;
    Task<string> output = process.StandardOutput.ReadToEndAsync();
    Task<string> error = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(10000)) { process.Kill(); throw new Exception("CLI regression timed out"); }
    return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
}
