using System.Runtime.InteropServices;
using System.Text.Json;

namespace KeyMouse.BackgroundProbe;

internal sealed record Handles(long Root, long Edit, long Button);
internal sealed record Ready(Handles Target, Handles Witness);
internal sealed record State(string Text, int Clicks, int Activations, bool Minimized);
internal sealed record Snapshot(State? Target, State Witness);

internal sealed class ProbeWindow : Form
{
    private readonly TextBox edit = new() { Name = "Notes", Text = "A[]Z", Dock = DockStyle.Top };
    private readonly Button button = new() { Name = "ApplyButton", Text = "Apply", Dock = DockStyle.Bottom };
    private int clicks, activations;

    // Keep ordinary window/control styles. Suppress activation on initial Show only;
    // a provider activating this form during an operation should be caught, not masked.
    protected override bool ShowWithoutActivation => true;

    internal ProbeWindow()
    {
        Text = "KeyMouse BackgroundProbe"; // Intentionally identical titles and contents.
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        var screen = SystemInformation.VirtualScreen;
        Location = new Point(screen.Right + 200, screen.Bottom + 200);
        Controls.AddRange([edit, button]);
        button.Click += (_, _) => clicks++;
        Activated += (_, _) => activations++;
    }

    internal Handles Publish()
    {
        Show();
        if (SystemInformation.VirtualScreen.IntersectsWith(Bounds) || WindowState != FormWindowState.Normal)
            throw new InvalidOperationException("Fixture was not a normal, off-screen window");
        edit.Select(2, 0); // A[|]Z, without focusing it.
        return new(Handle.ToInt64(), edit.Handle.ToInt64(), button.Handle.ToInt64());
    }

    [DllImport("user32")] private static extern bool ShowWindow(nint hwnd, int cmd);
    [DllImport("user32")] private static extern bool IsIconic(nint hwnd);
    internal void MinimizeWithoutActivation() => ShowWindow(Handle, 7 /* SW_SHOWMINNOACTIVE */);
    internal void Prepare(string command)
    {
        switch (command)
        {
            case "select-middle": edit.Select(1, edit.Text.Length - 2); break;
            case "limit-length": edit.MaxLength = 1; break;
            case "readonly": edit.ReadOnly = true; break;
            case "password": edit.PasswordChar = '*'; break;
            case "disable-button": button.Enabled = false; break;
            case "duplicate-id": Controls.Add(new Button { Name = button.Name, Text = "Other" }); break;
        }
    }
    internal State Read() => new(edit.Text, clicks, activations, IsIconic(Handle));
}

internal static class Fixture
{
    // All ground truth is read on the UI thread, not via the API being tested.
    internal static int Run(bool minimized)
    {
        if (!Console.IsInputRedirected || !Console.IsOutputRedirected) return 2;
        Application.EnableVisualStyles();
        using var target = new ProbeWindow();
        using var witness = new ProbeWindow();
        var ready = new Ready(target.Publish(), witness.Publish());
        if (minimized) target.MinimizeWithoutActivation();
        Console.WriteLine(JsonSerializer.Serialize(ready));
        _ = Task.Run(() =>
        {
            try
            {
                while (Console.ReadLine() is string command && command != "quit")
                {
                    var snapshot = (Snapshot)witness.Invoke(() =>
                    {
                        if (command == "close-target") target.Close();
                        else if (!target.IsDisposed) target.Prepare(command);
                        return new Snapshot(target.IsDisposed ? null : target.Read(), witness.Read());
                    });
                    Console.WriteLine(JsonSerializer.Serialize(snapshot));
                }
            }
            finally { witness.BeginInvoke(Application.ExitThread); }
        });
        Application.Run();
        return 0;
    }
}
