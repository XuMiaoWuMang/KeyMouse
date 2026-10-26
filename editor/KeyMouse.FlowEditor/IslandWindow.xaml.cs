using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 录制期间的悬浮条（"灵动岛"）。
///
/// 主窗口在录制开始后会隐藏（否则用户只能点界面上的按钮，那一下点击会被录进流程）。
/// 这个窗口是那条路上的**唯一退路**：一个始终置顶的小条，上面有「停止」。
/// 它自己的点击不进流程——编辑器把它的屏幕矩形作为 --ignore-rect 交给录制器。
/// </summary>
public sealed partial class IslandWindow : Window
{
    internal event Action? StopRequested;

    internal IslandWindow()
    {
        InitializeComponent();

        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());   // 无边框 + 置顶
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Title = "KeyMouse 录制中";
    }

    /// <summary>摆到屏幕工作区底部居中，并把窗口矩形返回给录制器（物理像素）。</summary>
    internal RectInt32 PlaceBottomCenter(int width, int height)
    {
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 work = area.WorkArea;
        int x = work.X + (work.Width - width) / 2;
        int y = work.Y + work.Height - height - 24;
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        return new RectInt32(x, y, width, height);
    }

    private void OnStop(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
}
