namespace KeyMouse.Tests;

/// <summary>
/// Entry point. Named TestEntry rather than Program so that inside this namespace the name
/// "Program" still resolves to KeyMouse.Program, which the tests call into.
/// </summary>
internal static class TestEntry
{
    private static int Main(string[] args)
    {
        ConsoleText.ConfigureOutputEncoding();

        if (args.Length > 0 && args[0] == "bench")
        {
            ParseBench.Run();
            return 0;
        }

        Console.WriteLine("KeyMouse tests (no desktop required)");
        Harness.Section("parsing", ParsingTests.Run);
        Harness.Section("windows", WindowTests.Run);
        Harness.Section("probe", ProbeTests.Run);
        Harness.Section("region", RegionTests.Run);
        Harness.Section("flow", FlowTests.Run);
        Harness.Section("runner", RunnerTests.Run);

        return Harness.Summary();
    }
}
