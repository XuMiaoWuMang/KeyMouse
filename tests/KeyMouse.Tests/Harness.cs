namespace KeyMouse.Tests;

/// <summary>Minimal assertion harness: counts checks, prints them, returns an exit code.</summary>
internal static class Harness
{
    private static int _passed;
    private static int _failed;

    public static void Group(string name) => Console.WriteLine($"\n== {name} ==");

    /// <summary>Runs one test area, turning an unexpected exception into a failure
    /// instead of aborting the whole run.</summary>
    public static void Section(string name, Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"\n!! {name} crashed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void Check(string what, bool ok, string? detail = null)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine($"  ok   {what}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL {what}{(detail is null ? "" : "  -> " + detail)}");
        }
    }

    public static void Equal<T>(string what, T expected, T actual) =>
        Check(what, EqualityComparer<T>.Default.Equals(expected, actual), $"expected [{expected}], got [{actual}]");

    public static void Sequence(string what, IEnumerable<string> expected, IEnumerable<string> actual)
    {
        string e = string.Join(" | ", expected);
        string a = string.Join(" | ", actual);
        Check(what, e == a, $"expected [{e}], got [{a}]");
    }

    public static void Throws<T>(string what, Action action) where T : Exception
    {
        try
        {
            action();
            Check(what, false, $"nothing was thrown, expected {typeof(T).Name}");
        }
        catch (T)
        {
            Check(what, true);
        }
        catch (Exception ex)
        {
            Check(what, false, $"expected {typeof(T).Name}, got {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static int Summary()
    {
        Console.WriteLine($"\n{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }
}
