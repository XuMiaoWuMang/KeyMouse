using Microsoft.UI.Xaml;

namespace KeyMouse.FlowEditor;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        UnhandledException += OnUnhandled;
        InitializeComponent();
    }

    /// <summary>
    /// 任何没被接住的异常都在这里落地：写进 %LOCALAPPDATA%\KeyMouse\editor-crash.log，并且**不让进程死掉**。
    ///
    /// 理由很实际：这个界面里点一下按钮就可能走到异步路径上，异常一旦逃到 UI 线程，用户看到的就是
    /// "点了之后自己退了"——什么线索都不留。留下文件，问题就还能查。
    /// </summary>
    private static void OnUnhandled(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMouse");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "editor-crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 连写文件都失败时，也别让兜底本身把进程带走。
        }

        e.Handled = true;   // 窗口留着，用户还能继续用；原因在文件里。
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        // A flow file on the command line opens straight away, so `KeyMouse flow edit x.json` works.
        string[] commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Length > 1 && File.Exists(commandLine[1])) _window.OpenFile(commandLine[1]);
    }
}
