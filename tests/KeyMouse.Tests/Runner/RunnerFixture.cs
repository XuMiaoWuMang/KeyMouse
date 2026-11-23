using System.Diagnostics;
using KeyMouse.Runner;

namespace KeyMouse.Tests;

// Real Runner transport/host in its own process. EOF on redirected stdin means its owner
// has exited, even if killed; do not leave a test pipe listening on the user's desktop.
internal static class RunnerFixture
{
    internal static async Task<Process> Start(Guid nonce, CancellationToken stop)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardInput = true };
        start.ArgumentList.Add(typeof(RunnerFixture).Assembly.Location);
        start.ArgumentList.Add("serve-fixture");
        start.ArgumentList.Add(nonce.ToString("N"));
        var child = Process.Start(start) ?? throw new IOException("Could not start test Runner");
        try
        {
            if (await child.StandardOutput.ReadLineAsync(stop) != "READY") throw new IOException("Test Runner did not start");
            return child;
        }
        catch
        {
            child.StandardInput.Close();
            if (!child.HasExited) child.Kill();
            child.Dispose();
            throw;
        }
    }

    internal static async Task<int> Serve(Guid nonce)
    {
        using var lifetime = new CancellationTokenSource();
        var serving = new PipeServer(new RunnerHost(), lifetime, RunnerProtocol.TestPipeName(nonce.ToString("N"))).RunAsync();
        if (serving.IsCompleted) await serving;
        Console.WriteLine("READY");
        // Console.ReadLine is synchronous even behind some TextReader.ReadLineAsync implementations.
        await Task.WhenAny(serving, Task.Run(Console.ReadLine));
        lifetime.Cancel();
        await serving;
        return 0;
    }
}
