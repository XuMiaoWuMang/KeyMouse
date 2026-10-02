using System.Windows.Forms;

namespace KeyMouse.SmokeTarget;

/// <summary>
/// The window that tests/smoke.ps1 drives.
///
/// The smoke test used to borrow Notepad, which went wrong in two ways: it had to kill
/// every Notepad process first to be sure which window it owned (destroying whatever the
/// user had open), and Windows 11 Notepad restores previous tabs, so "the Notepad window"
/// could end up being two, turning every selector into an ambiguity error. A window the
/// test starts and stops itself has neither problem, and its whole client area is one text
/// box - no toolbar band to accidentally click.
/// </summary>
internal static class Program
{
    public const string WindowTitle = "KeyMouse 冒烟靶子";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var text = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 12f),
            ScrollBars = ScrollBars.Vertical,
            WordWrap = false
        };

        using var window = new Form
        {
            Text = WindowTitle,
            Width = 900,
            Height = 620,
            StartPosition = FormStartPosition.CenterScreen
        };
        window.Controls.Add(text);
        window.Shown += (_, _) => text.Focus();

        Application.Run(window);
    }
}
