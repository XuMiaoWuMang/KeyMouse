namespace KeyMouse.Tests;

/// <summary>
/// Entry point. Named TestEntry rather than Program so that inside this namespace the name
/// "Program" still resolves to KeyMouse.Program, which the tests call into.
/// </summary>
internal static class TestEntry
{
    private static int Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* no console attached */ }

        Console.WriteLine("KeyMouse tests (no desktop required)");
        Harness.Section("parsing", ParsingTests.Run);
        Harness.Section("windows", WindowTests.Run);

        return Harness.Summary();
    }
}
