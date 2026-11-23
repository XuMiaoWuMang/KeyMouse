using System.Diagnostics;

namespace KeyMouse.Tests;

/// <summary>Full JSON load, validation, dry execution and reporting; no input, sleep or window selector.</summary>
internal static class FlowRunnerBenchmark
{
    internal static int Run()
    {
        string path = Path.Combine(Path.GetTempPath(), $"km-bench-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, FlowRun.Flow("[" + string.Join(',', Enumerable.Repeat("""{"type":"keydown","combo":"a"}""", 100)) + "]"));
            using var quiet = CommandOutput.Redirect(TextWriter.Null, TextWriter.Null);
            string[] args = ["run", path, "--dry-run", "--delay", "0"];
            const int warmup = 10, samples = 51;
            var elapsed = new double[samples];
            var bytes = new long[samples];
            for (int i = -warmup; i < samples; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                if (Commands.Execute(args) != 0) return 1;
                if (i < 0) continue;
                elapsed[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Array.Sort(elapsed); Array.Sort(bytes);
            Console.WriteLine($"100 steps, {warmup} warmups, {samples} samples: median={elapsed[samples / 2]:F3}ms p95={elapsed[48]:F3}ms allocated median={bytes[samples / 2]} bytes");
            return 0;
        }
        finally { File.Delete(path); }
    }
}
