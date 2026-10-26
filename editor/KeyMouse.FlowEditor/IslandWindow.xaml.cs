using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 录制期间的悬浮停止按钮：红底圆 + 白色方块，可拖动。
///
/// 三处实测换来的做法：
/// * **圆靠窗口区域**（SetWindowRgn）而不是画图元素——区域裁剪后嵌套的形状元素不渲染，
///   只有根 Background 画得出来，所以方块用文字（■）画；
/// * **拖动**用指针事件直接移动 AppWindow；位移超过阈值就不当成点击；
/// * 录制器按**窗口句柄**忽略它（不是矩形）：拖到哪儿都不会被录进流程。
/// </summary>
public sealed partial class IslandWindow : Window
{
    internal event Action? StopRequested;

    private bool _fired;
    private bool _dragging;
    private Windows.Foundation.Point _grabbedAt;
    private PointInt32 _originAt;

    internal IslandWindow()
    {
        InitializeComponent();

        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());   // 无边框 + 置顶
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Title = "KeyMouse 录制中";

        Root.PointerPressed += OnPressed;
        Root.PointerMoved += OnMoved;
        Root.PointerReleased += OnReleased;
        Root.PointerCaptureLost += (_, _) => _dragging = false;
    }

    internal IntPtr Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    /// <summary>摆到屏幕右侧、垂直居中，并把窗口裁成圆。尺寸按窗口 DPI 换算。</summary>
    internal RectInt32 PlaceRightCenter(int logicalSize, int marginRight)
    {
        int dpi = GetDpiForWindow(Handle);
        double scale = dpi <= 0 ? 1.0 : dpi / 96.0;
        int size = (int)Math.Round(logicalSize * scale);
        int margin = (int)Math.Round(marginRight * scale);

        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 work = area.WorkArea;
        int x = work.X + work.Width - size - margin;
        int y = work.Y + (work.Height - size) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, size, size));

        IntPtr region = CreateRoundRectRgn(0, 0, size + 1, size + 1, size, size);
        if (region != IntPtr.Zero && SetWindowRgn(Handle, region, true) == 0) DeleteObject(region);

        return new RectInt32(x, y, size, size);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_fired) return;
        _grabbedAt = e.GetCurrentPoint(Root).Position;
        _originAt = AppWindow.Position;
        _dragging = true;
        Root.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || _fired) return;

        double scale = Root.XamlRoot?.RasterizationScale ?? 1.0;
        Windows.Foundation.Point now = e.GetCurrentPoint(Root).Position;
        int dx = (int)Math.Round((now.X - _grabbedAt.X) * scale);
        int dy = (int)Math.Round((now.Y - _grabbedAt.Y) * scale);
        AppWindow.Move(new PointInt32(_originAt.X + dx, _originAt.Y + dy));
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Root.ReleasePointerCapture(e.Pointer);

        // 位移超过几像素就是"拖动"，不是"点击"——否则拖完一圈就顺手把录制停了。
        Windows.Foundation.Point now = e.GetCurrentPoint(Root).Position;
        double moved = Math.Abs(now.X - _grabbedAt.X) + Math.Abs(now.Y - _grabbedAt.Y);
        if (moved > 4 || _fired) return;

        _fired = true;
        try { AppWindow.Hide(); } catch (Exception) { }
        StopRequested?.Invoke();
    }

    [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hwnd);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
}
