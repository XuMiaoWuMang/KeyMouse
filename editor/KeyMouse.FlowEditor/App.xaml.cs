using Microsoft.UI.Xaml;

namespace KeyMouse.FlowEditor;

public partial class App : Application
{
    private MainWindow? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();

        // A flow file on the command line opens straight away, so `KeyMouse flow edit x.json` works.
        string[] commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Length > 1 && File.Exists(commandLine[1])) _window.OpenFile(commandLine[1]);
    }
}
