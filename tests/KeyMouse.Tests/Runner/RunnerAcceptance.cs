using System.Diagnostics;
using System.IO.Pipes;
using KeyMouse.Runner;

namespace KeyMouse.Tests;

/// <summary>Opt-in operational checks. Never starts the product pipe, elevates or modifies an account.</summary>
internal static class RunnerAcceptance
{
    internal static async Task<int> Soak(int minutes)
    {
        if (minutes is < 1 or > 1440) return 2;
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var nonce = Guid.NewGuid();
        var name = RunnerProtocol.TestPipeName(nonce.ToString("N"));
        using var process = await RunnerFixture.Start(nonce, startup.Token);
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, FlowRun.Flow("""[{"type":"sleep","ms":0}]"""));
            var clock = Stopwatch.StartNew(); long nextSample = 0; int runs = 0;
            Console.WriteLine("elapsed_s,runs,private_bytes,handles,retained_jobs");
            while (clock.Elapsed < TimeSpan.FromMinutes(minutes))
            {
                using var client = await RunnerClient.ConnectAsync(2000, name);
                if (client == null) return 1;
                var end = await client.SendAsync("run", new RunnerParameters { Flow = path, DryRun = true }).WaitAsync(TimeSpan.FromSeconds(10));
                if (end.Kind != "finished" || end.ExitCode != 0) return 1;
                runs++;
                if (clock.ElapsedMilliseconds >= nextSample)
                {
                    await Sample(client);
                    nextSample = clock.ElapsedMilliseconds + 60000;
                }
                await Task.Delay(250);
            }
            using var last = await RunnerClient.ConnectAsync(2000, name) ?? throw new IOException("Runner disappeared");
            return await Sample(last) <= 128 ? 0 : 1;

            async Task<int> Sample(RunnerClient client)
            {
                var jobs = await client.SendAsync("list").WaitAsync(TimeSpan.FromSeconds(10));
                if (jobs.Kind != "result" || jobs.Value is not { } list) throw new IOException("Runner did not return job list");
                int retained = list.GetArrayLength();
                process.Refresh();
                Console.WriteLine($"{(int)clock.Elapsed.TotalSeconds},{runs},{process.PrivateMemorySize64},{process.HandleCount},{retained}");
                return retained;
            }
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(5000)) { process.Kill(); await process.WaitForExitAsync(); }
            File.Delete(path);
        }
    }

    // The nonce is supplied to both test shells: do NOT compute a different pipe name from each username.
    internal static async Task<int> Acl(string mode, Guid nonce)
    {
        string name = "keymouse-acl-check-" + nonce.ToString("N");
        if (mode == "listen")
        {
            using var pipe = RunnerPipe.Listen(name);
            Console.WriteLine($"READY {nonce:N} (same pipe for 120s, no command dispatch)");
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await pipe.WaitForConnectionAsync(timeout.Token); Console.WriteLine("CONNECTED"); }
            catch (OperationCanceledException) { Console.WriteLine("listener expired"); }
            return 0;
        }
        if (mode is not ("expect-denied" or "expect-allowed")) return 2;
        try
        {
            using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(3000); Console.WriteLine("CONNECTED"); return mode == "expect-allowed" ? 0 : 1;
        }
        catch (UnauthorizedAccessException) { Console.WriteLine("ACCESS_DENIED"); return mode == "expect-denied" ? 0 : 1; }
        catch (TimeoutException) { Console.WriteLine("NO LISTENER: not evidence of denial"); return 1; }
    }
}
