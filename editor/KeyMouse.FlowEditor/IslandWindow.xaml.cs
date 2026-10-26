using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 录制期间的悬浮停止按钮：红底 + 白色方块，可拖动。
///
/// 两处实测换来的做法：
/// * **不设窗口区域**：SetWindowRgn 之后这个窗口里任何子内容都不再渲染（Background 除外），
///   所以形状只能是圆角方形——WinUI 3 也没有逐像素透明，正圆在这个栈里实现不了；
/// * **拖动按屏幕光标位移**：XAML 指针坐标乘缩放系数会累积误差，表现为不跟手、还比鼠标慢。
///   直接取 GetCursorPos 的位移，1:1 跟手，也不会抖动。
/// </summary>
public sealed partial class IslandWindow : Window
{
    internal event Action? StopRequested;

    private bool _fired;
    private bool _dragging;
    private POINT _grabbedCursor;
    private PointInt32 _originAt;
    private RectInt32? _pinned;

    /// <summary>
    /// 显示之后再钉一次尺寸：presenter 会在首次显示时按内容重算，
    /// 把 PlaceRightCenter 设的方形又撑成横条（实测 202x66，高度对、宽度多 136）。
    /// </summary>
    internal void Pin()
    {
        if (_pinned is not RectInt32 r) return;
        AppWindow.MoveAndResize(r);
    }

    internal IslandWindow()
    {
        InitializeComponent();

        // 用会按内容自适应的 presenter：内容固定 44x44，窗口就正好是 44x44，没有多余的白底。
        // （自己配的 presenter 里 IsResizable=false 会让 MoveAndResize 的**改尺寸**被忽略，
        //   窗口保持初始大小，多出来的地方露出窗口底色——之前那条白底长条就是这么来的。）
        AppWindow.SetPresenter(OverlappedPresenter.CreateForContextMenu());
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Title = "KeyMouse 录制中";

        Root.PointerPressed += OnPressed;
        Root.PointerMoved += OnMoved;
        Root.PointerReleased += OnReleased;
        Root.PointerCaptureLost += (_, _) => _dragging = false;
    }

    internal IntPtr Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    /// <summary>摆到屏幕右侧、垂直居中。尺寸按窗口 DPI 换算。</summary>
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

        // 只移动，不设尺寸：尺寸交给内容（自适应 presenter），否则两者会互相覆盖。
        AppWindow.Move(new PointInt32(x, y));
        _pinned = new RectInt32(x, y, size, size);
        return _pinned.Value;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_fired || !GetCursorPos(out _grabbedCursor)) return;
        _originAt = AppWindow.Position;
        _dragging = true;
        Root.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || _fired) return;
        if (!GetCursorPos(out POINT now)) return;

        int dx = now.X - _grabbedCursor.X;
        int dy = now.Y - _grabbedCursor.Y;
        if (dx == 0 && dy == 0) return;
        AppWindow.Move(new PointInt32(_originAt.X + dx, _originAt.Y + dy));
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Root.ReleasePointerCapture(e.Pointer);

        // 位移超过几像素就是拖动，不是点击——拖完松手不该顺手把录制停掉。
        if (GetCursorPos(out POINT now))
        {
            int moved = Math.Abs(now.X - _grabbedCursor.X) + Math.Abs(now.Y - _grabbedCursor.Y);
            if (moved > 4) return;
        }

        if (_fired) return;
        _fired = true;
        try { AppWindow.Hide(); } catch (Exception) { }
        StopRequested?.Invoke();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern int GetDpiForWindow(IntPtr hwnd);
}
