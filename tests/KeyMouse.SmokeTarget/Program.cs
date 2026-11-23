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
    private static void Main(string[] args)
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
        // The opt-in replay test compares this non-editable client region. It mirrors the real
        // TextBox without its blinking caret, selection or input-method adornments.
        if (args is ["--replay-evidence"])
        {
            var mirror = new Label { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8),
                Text = "EMPTY", Font = text.Font, BackColor = Color.White, ForeColor = Color.Black };
            text.TextChanged += (_, _) => mirror.Text = text.TextLength == 0 ? "EMPTY" : text.Text;
            window.Controls.Add(mirror);
        }
        window.Shown += (_, _) => text.Focus();

        Application.Run(window);
    }
}
