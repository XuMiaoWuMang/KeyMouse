using Microsoft.UI.Xaml;

namespace KeyMouse.FlowEditor;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        UnhandledException += OnUnhandled;

        // UI 线程之外的两条路，XAML 的处理器看不到：管道读线程、后台 Task。
        // 不记录的话，表现就是"程序直接没了、日志里什么都没有"。
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Write(args.ExceptionObject as Exception, "非 UI 线程");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write(args.Exception, "未观察的 Task");
            args.SetObserved();
        };

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
        CrashLog.Write(e.Exception, "UI 线程");

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
