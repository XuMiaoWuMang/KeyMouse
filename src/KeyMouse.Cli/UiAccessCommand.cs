using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace KeyMouse.Cli;

/// <summary>One short-lived, authenticated pipe per input job; no scripts or logs on disk.</summary>
internal static class UiAccessCommand
{
    private static readonly string[] EnvironmentKeys = ["KEYMOUSE_HUD", "KEYMOUSE_OUTPUT_ENCODING"];
    private const int Cancelled = 1223;
    private const int MaxFrameBytes = 1024 * 1024;
    private static readonly string[] Allowed = ["run", "mouse", "key", "keyboard", "window", "record", "probe", "region", "schema", "--version", "--help"];

    internal static int Diagnose()
    {
        try { Console.WriteLine(JsonSerializer.Serialize(TokenCapabilities.Read(Environment.ProcessId))); return 0; }
        catch (Exception e) { Console.Error.WriteLine("Token diagnostic failed: " + e.Message); return 5; }
    }

    internal static int Run(string[] args)
    {
        if (args.Length < 2 || !Path.IsPathFullyQualified(args[0]))
            return Fail(2, "UIAccess requires an absolute helper path and capability arguments.");
        string helper = Path.GetFullPath(args[0]);
        string trusted = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KeyMouse", "Input", "KeyMouse.Input.exe");
        if (!string.Equals(helper, trusted, StringComparison.OrdinalIgnoreCase) || !File.Exists(helper))
            return Fail(2, "UIAccess helper must be the installed Program Files\\KeyMouse\\Input\\KeyMouse.Input.exe.");
        // Windows verifies the UIAccess image signature on launch. Reject directory junctions
        // so the fixed installation path cannot redirect into a writable dependency tree.
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(helper)!); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return Fail(5, "UIAccess trusted path must not contain reparse points.");
        if ((File.GetAttributes(helper) & FileAttributes.ReparsePoint) != 0)
            return Fail(5, "UIAccess executable must not be a reparse point.");
        if (!Allowed.Contains(args[1], StringComparer.OrdinalIgnoreCase))
            return Fail(2, "UIAccess accepts capability commands only (no editor, runner, elevation or nested bridge).");
        try { return BridgeAsync(helper, args[1..]).GetAwaiter().GetResult(); }
        catch (Win32Exception e) when (e.NativeErrorCode == Cancelled) { return Fail(Cancelled, "UIAccess launch cancelled; no operation executed."); }
        catch (Exception e) { return Fail(5, "UIAccess bridge failed: " + e.Message); }
    }

    internal static NamedPipeServerStream CreateUserPipe(string name)
    {
        SecurityIdentifier user = WindowsIdentity.GetCurrent().User ?? throw new IOException("Missing current user SID.");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(user);
        security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
        // Avoid CurrentUserOnly's framework owner/elevation assumptions at this boundary.
        // A split token's owner can differ; the DACL intentionally grants TokenUser SID only.
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 4096, 4096, security);
    }

    private static async Task<int> BridgeAsync(string helper, string[] args)
    {
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string name = "KeyMouse.UIAccess." + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var pipe = CreateUserPipe(name);
        var info = new ProcessStartInfo(helper) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(helper)! };
        foreach (string arg in new[] { "--uiaccess-helper", name, nonce, Environment.ProcessId.ToString() }) info.ArgumentList.Add(arg);
        using var lifetime = new CancellationTokenSource();
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        startup.CancelAfter(TimeSpan.FromSeconds(15));
        using var controls = new BridgeControls(Console.In, lifetime);
        controls.Start();
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; lifetime.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        Process? child = null;
        try
        {
            lifetime.Token.ThrowIfCancellationRequested();
            child = Process.Start(info) ?? throw new IOException("ShellExecute did not return a helper process.");
            Task connected = pipe.WaitForConnectionAsync(startup.Token);
            Task exited = child.WaitForExitAsync();
            if (await Task.WhenAny(connected, exited) == exited)
                return Fail(child.ExitCode == 0 ? 5 : child.ExitCode, "UIAccess helper exited before handshake; verify signing, trusted installation path and TokenUIAccess.");
            await connected;
            if (!Native.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint pid) || pid != child.Id)
                throw new IOException("Unexpected pipe client PID.");
            TokenCapabilities peer = TokenCapabilities.Read(child.Id);
            TokenCapabilities self = TokenCapabilities.Read(Environment.ProcessId);
            if (!peer.UiAccess || peer.UserSid != self.UserSid || peer.SessionId != self.SessionId)
                throw new IOException("Helper must have TokenUIAccess=1 and the same user/session.");
            var channel = new Channel(pipe);
            Frame hello = await channel.ReadAsync(startup.Token);
            if (hello.Type != "hello" || hello.Nonce != nonce) throw new IOException("Invalid helper handshake.");
            var environment = new Dictionary<string, string?>();
            foreach (string key in EnvironmentKeys) environment[key] = Environment.GetEnvironmentVariable(key);
            controls.SendJob(channel, new Frame("job", Args: args, WorkingDirectory: Environment.CurrentDirectory, Environment: environment));
            using var cancel = lifetime.Token.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    try { if (!child.HasExited) child.Kill(); }
                    catch (Exception e) when (e is InvalidOperationException or Win32Exception) { /* The authenticated helper also watches pipe disconnect. */ }
                });
                _ = Task.Run(() => { try { channel.Write(new Frame("cancel")); } catch (IOException) { } });
            });
            while (true)
            {
                Frame frame;
                try { frame = await channel.ReadAsync(CancellationToken.None); }
                catch (EndOfStreamException) when (lifetime.IsCancellationRequested)
                {
                    using var cancelDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await child.WaitForExitAsync(cancelDeadline.Token);
                    return Cancelled;
                }
                switch (frame.Type)
                {
                    case "stdout": Console.Out.Write(frame.Text); Console.Out.Flush(); break;
                    case "stderr": Console.Error.Write(frame.Text); Console.Error.Flush(); break;
                    case "exit":
                        using (var exitDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                            await child.WaitForExitAsync(exitDeadline.Token);
                        return frame.ExitCode ?? 5;
                    default: throw new IOException("Unexpected helper frame.");
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return Cancelled; }
        finally
        {
            lifetime.Cancel();
            Console.CancelKeyPress -= cancelHandler;
            // Close first; helper's disconnect watchdog also handles abrupt bridge termination.
            pipe.Dispose();
            try
            {
                if (child is not null && !child.HasExited && !child.WaitForExit(2000))
                {
                    child.Kill();
                    if (!child.WaitForExit(2000)) Console.Error.WriteLine("UIAccess helper did not exit within cleanup deadline.");
                }
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                Console.Error.WriteLine("UIAccess helper cleanup: " + e.Message + "; pipe is closed and helper disconnect watchdog is active.");
            }
            child?.Dispose();
        }
    }

    internal static int Helper(string[] args)
    {
        try
        {
            if (args.Length != 3 || !args[0].StartsWith("KeyMouse.UIAccess.", StringComparison.Ordinal) ||
                args[0].Length != 66 || args[1].Length != 64 || !args[1].All(Uri.IsHexDigit) ||
                !int.TryParse(args[2], out int parentPid)) return Fail(2, "Invalid internal UIAccess handshake arguments.");
            TokenCapabilities self = TokenCapabilities.Read(Environment.ProcessId);
            if (!self.UiAccess) return Fail(5, "TokenUIAccess is 0. Sign and install the helper in a Windows trusted location; no fallback is allowed.");
            // The job's working directory may be writable. Never resolve native libraries
            // from it or PATH; native dependencies are installed beside this protected image.
            if (!Native.SetDefaultDllDirectories(0x00000200 | 0x00000800))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect(15000);
            if (!Native.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint pid) || pid != parentPid)
                return Fail(5, "Unexpected bridge PID.");
            TokenCapabilities peer = TokenCapabilities.Read(parentPid);
            if (peer.UserSid != self.UserSid || peer.SessionId != self.SessionId) return Fail(5, "Bridge user/session mismatch.");
            var channel = new Channel(pipe);
            channel.Write(new Frame("hello", Nonce: args[1]));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            Frame job = channel.ReadAsync(timeout.Token).GetAwaiter().GetResult();
            if (job.Type != "job" || job.Args is not { Length: > 0 and <= 256 } ||
                !Allowed.Contains(job.Args[0], StringComparer.OrdinalIgnoreCase) ||
                job.WorkingDirectory is null || !Path.IsPathFullyQualified(job.WorkingDirectory) || !Directory.Exists(job.WorkingDirectory))
                return Fail(2, "Invalid UIAccess capability job.");
            if (job.Environment is null || job.Environment.Keys.Any(key => !EnvironmentKeys.Contains(key)))
                return Fail(2, "Invalid UIAccess environment allowlist.");
            foreach (string key in EnvironmentKeys)
                Environment.SetEnvironmentVariable(key, job.Environment.GetValueOrDefault(key));
            UiAccessJob.Install();
            Environment.CurrentDirectory = job.WorkingDirectory;
            using PauseControl? pause = PauseControl.Install(Environment.ProcessId);
            // The main thread remains STA. Reading cancellation never waits for a running input call.
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        Frame control = await channel.ReadAsync(CancellationToken.None);
                        if (control.Type == "pause" && control.Paused is { } paused)
                        {
                            using EventWaitHandle pauseEvent = EventWaitHandle.OpenExisting(PauseControl.NameFor(Environment.ProcessId));
                            if (paused) pauseEvent.Set(); else pauseEvent.Reset();
                            continue;
                        }
                        if (control.Type != "cancel") { Environment.Exit(5); return; }
                        channel.Write(new Frame("exit", ExitCode: Cancelled));
                        break;
                    }
                }
                catch (Exception) { /* Broken parent pipe must stop input immediately. */ }
                Environment.Exit(Cancelled);
            });
            // Configure encoding before replacing writers: Commands.Execute otherwise resets stdout.
            ConsoleText.ConfigureOutputEncoding();
            // The GUI-subsystem helper has no console. Detach defensively so Core's
            // repeated console initialization sees CP=0 and keeps our managed pipe writers.
            Native.FreeConsole();
            Environment.SetEnvironmentVariable("KEYMOUSE_OUTPUT_ENCODING", null);
            Console.SetOut(new PipeWriter(channel, "stdout"));
            Console.SetError(new PipeWriter(channel, "stderr"));
            int code = Commands.Execute(job.Args);
            channel.Write(new Frame("exit", ExitCode: code));
            return code;
        }
        catch (Exception e) { return Fail(5, "UIAccess helper failed: " + e.Message); }
    }

    internal sealed class BridgeControls(TextReader input, CancellationTokenSource lifetime) : IDisposable
    {
        private readonly object gate = new();
        private Channel? channel;
        private Task? reading;
        internal void Start() => reading = ReadAsync();
        internal Task Completion => reading ?? Task.CompletedTask;
        internal void SendJob(Channel destination, Frame job)
        {
            lock (gate)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                destination.Write(job);
                channel = destination;
            }
        }
        private async Task ReadAsync()
        {
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    string? line = await input.ReadLineAsync(lifetime.Token);
                    if (line is null) return;
                    using JsonDocument control = JsonDocument.Parse(line);
                    if (!control.RootElement.TryGetProperty("type", out var type)) continue;
                    lock (gate)
                    {
                        if (type.GetString() == "cancel") { lifetime.Cancel(); return; }
                        if (type.GetString() == "pause" && control.RootElement.TryGetProperty("paused", out var paused) &&
                            paused.ValueKind is JsonValueKind.True or JsonValueKind.False)
                            channel?.Write(new Frame("pause", Paused: paused.GetBoolean()));
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or JsonException or IOException) { }
        }
        public void Dispose() { }
    }

    private static int Fail(int code, string message) { Console.Error.WriteLine(message); return code; }
    internal sealed record Frame(string Type, string? Text = null, int? ExitCode = null, string? Nonce = null,
        string[]? Args = null, string? WorkingDirectory = null, Dictionary<string, string?>? Environment = null, bool? Paused = null);

    internal sealed class Channel(Stream stream)
    {
        private readonly object gate = new();
        public void Write(Frame frame)
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(frame);
            if (data.Length > MaxFrameBytes) throw new IOException("UIAccess frame exceeds limit.");
            lock (gate) { stream.Write(BitConverter.GetBytes(data.Length)); stream.Write(data); stream.Flush(); }
        }
        public Task WriteAsync(Frame frame) { Write(frame); return Task.CompletedTask; }
        public async Task<Frame> ReadAsync(CancellationToken cancellation)
        {
            byte[] header = new byte[4];
            await stream.ReadExactlyAsync(header, cancellation);
            int length = BitConverter.ToInt32(header);
            if (length < 1 || length > MaxFrameBytes) throw new IOException("Invalid UIAccess frame length.");
            byte[] data = new byte[length];
            await stream.ReadExactlyAsync(data, cancellation);
            return JsonSerializer.Deserialize<Frame>(data) ?? throw new IOException("Missing UIAccess frame.");
        }
    }

    private sealed class PipeWriter(Channel channel, string type) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value) => Write(value.ToString());
        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            // Keep bounded frames even for large reports. JSON expands a char to at most six bytes.
            for (int offset = 0; offset < value.Length; offset += 16000)
                channel.Write(new Frame(type, value.Substring(offset, Math.Min(16000, value.Length - offset))));
        }
        public override void WriteLine(string? value) => Write(value + NewLine);
    }

    private static class Native
    {
        [DllImport("kernel32.dll")]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetDefaultDllDirectories(uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint pid);
    }
}
