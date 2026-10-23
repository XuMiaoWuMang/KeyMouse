using System.Diagnostics;
using System.Drawing;

namespace KeyMouse.Setup;

/// <summary>
/// 一屏就够：去哪儿装、装什么、装完去哪儿点。
/// 不做"下一步/下一步/完成"三步向导——它只有一个决定要问。
/// </summary>
internal sealed class SetupForm : Form
{
    private readonly TextBox _path = new() { Dock = DockStyle.Fill, Text = "" };
    private readonly Label _status = new() { AutoSize = false, Height = 48, Dock = DockStyle.Fill };
    private readonly ProgressBar _bar = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly Button _install = new() { Text = "安装", AutoSize = true, Padding = new Padding(16, 4, 16, 4) };
    private readonly Button _cancel = new() { Text = "取消", AutoSize = true, Padding = new Padding(12, 4, 12, 4) };

    internal SetupForm(string directory)
    {
        Text = Program.ProductName + " " + Program.Version + " 安装程序";
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 210);
        BackColor = Color.FromArgb(250, 247, 242);

        _path.Text = directory;

        var title = new Label
        {
            Text = "安装到",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6),
        };

        var browse = new Button { Text = "浏览…", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { SelectedPath = _path.Text };
            if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.SelectedPath;
        };

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_path, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
        };
        buttons.Controls.Add(_install);
        buttons.Controls.Add(_cancel);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(18),
        };
        foreach (Control control in new Control[] { title, pathRow, _status, _bar, buttons })
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(control);
        }

        Controls.Add(root);

        _cancel.Click += (_, _) => Close();
        _install.Click += (_, _) => Install();
        Shown += (_, _) => Refresh();
        OnLoad(EventArgs.Empty);
        RefreshStatus();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_bar.Visible) e.Cancel = true;   // 装着的时候不给关
        base.OnFormClosing(e);
    }

    private void RefreshStatus()
    {
        string? existing = Installer.FindExistingCli(_path.Text);
        _status.ForeColor = Color.FromArgb(106, 101, 92);
        _status.Text = existing is null
            ? "未检测到 KeyMouse 命令行。安装时一并装到 安装目录\\cli\\。"
            : "已检测到 KeyMouse 命令行，安装时不会覆盖：" + existing;
    }

    private async void Install()
    {
        string directory = _path.Text.Trim();
        if (directory.Length == 0)
        {
            _status.Text = "请先选一个安装目录。";
            return;
        }

        _install.Enabled = false;
        _cancel.Enabled = false;
        _bar.Visible = true;
        _status.ForeColor = Color.FromArgb(106, 101, 92);

        var log = new Progress<string>(text => _status.Text = text);
        int code = await Task.Run(() => Installer.Run(directory, silent: false, progress: text =>
        {
            // 逐条刷到界面上：解压大文件时用户要看得见它没卡住
            if (!IsDisposed) _status.Text = text;
        }));

        _bar.Visible = false;
        _cancel.Enabled = true;
        _install.Enabled = true;

        if (code != 0) return;

        _status.ForeColor = Color.FromArgb(23, 20, 15);
        _status.Text = "装好了。" + Environment.NewLine + Path.Combine(directory, "KeyMouse.FlowEditor.exe");

        if (MessageBox.Show(this, "现在打开流程编辑器？", Program.ProductName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string exe = Path.Combine(directory, "KeyMouse.FlowEditor.exe");
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }

        Close();
    }
}
