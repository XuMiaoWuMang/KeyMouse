using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 一条可以拖的分隔条。WinUI 3 没有 GridSplitter，而"两栏能不能宽一点"是每天都要用手做的事。
///
/// 派生自 <see cref="Grid"/>：Button 为了点击语义会自己捕获指针，紧跟其后的 PointerCaptureLost
/// 立刻把拖动掐掉（实测：光标变成左右箭头但拖不动）；Border 是密封类，派生不了。
/// Grid 有 Background 就能命中测试，而且不抢事件——那条看得见的线是它的一个子 Border。
/// </summary>
public sealed partial class Splitter : Grid
{
    private readonly Border _line = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Stretch,
        Width = 1,
        IsHitTestVisible = false,
    };

    private bool _dragging;
    private double _start;
    private double _origin;
    private Brush? _idle;

    /// <summary>线平时的颜色。主题切换时重取一次——不暴露成 XAML 属性，那种赋值在运行时会失败（实测）。</summary>
    private Brush IdleBrush => (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];

    public Splitter()
    {
        // 8px 的可拖区域，里面画一条 1px 的线：看得见才拖得动。
        Width = 8;
        Height = 8;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Thickness(0);
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Children.Add(_line);
        _line.Background = IdleBrush;

        PointerEntered += (_, _) =>
        {
            SetCursor(Horizontal ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
            _line.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        };
        PointerExited += (_, _) =>
        {
            SetCursor(InputSystemCursorShape.Arrow);
            _line.Background = IdleBrush;
        };
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => End();
    }

    /// <summary>拖动结束时通知宿主，宿主据此判断"面板是不是已经小到该收起来了"。</summary>
    public event Action? Changed;

    /// <summary>正在拖？宿主用它区分"拖动中"和"松手了"。</summary>
    public bool IsDragging => _dragging;

    /// <summary>要调整的列（Direction=Horizontal）或行（Direction=Vertical）。</summary>
    public DependencyObject? Target { get; set; }

    public string Direction { get; set; } = "Horizontal";

    private string _edge = "Left";

    /// <summary>那条看得见的线画在哪一侧（Left / Top）。</summary>
    public string Edge
    {
        get => _edge;
        set
        {
            _edge = value;
            bool top = string.Equals(value, "Top", StringComparison.OrdinalIgnoreCase);
            _line.Width = top ? double.NaN : 1;
            _line.Height = top ? 1 : double.NaN;
            _line.HorizontalAlignment = HorizontalAlignment.Stretch;
            _line.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Stretch;
        }
    }

    /// <summary>Vertical 时用它：向上拖是变高还是变矮。Horizontal 时用于"目标在分隔条右侧"。</summary>
    public bool Invert { get; set; }

    public double MinTarget { get; set; } = 120;

    public double MaxTarget { get; set; } = 1200;

    private bool Horizontal => !string.Equals(Direction, "Vertical", StringComparison.OrdinalIgnoreCase);

    private double TargetSize
    {
        get => Target switch
        {
            ColumnDefinition column => column.ActualWidth,
            RowDefinition row => row.ActualHeight,
            _ => 0,
        };
        set
        {
            double size = Math.Clamp(value, MinTarget, MaxTarget);
            switch (Target)
            {
                case ColumnDefinition column:
                    column.Width = new GridLength(size);
                    break;
                case RowDefinition row:
                    row.Height = new GridLength(size);
                    break;
            }
        }
    }

    /// <summary>
    /// 让**根元素**替我们跟住指针。实测：在这个 Grid 上 CapturePointer 之后立刻会收到
    /// PointerCaptureLost，拖动当场结束（光标变成左右箭头却拖不动就是这个原因）。
    /// 根元素本来就能收到整窗的指针移动，用它跟踪比依赖捕获可靠。
    /// </summary>
    public void Track(FrameworkElement host)
    {
        host.PointerMoved += (sender, e) => OnMoved(sender, e);
        host.PointerReleased += (sender, e) =>
        {
            if (!_dragging) return;
            ReleasePointerCapture(e.Pointer);
            End();
        };
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Target is null) return;
        _dragging = true;
        _start = Horizontal ? e.GetCurrentPoint(null).Position.X : e.GetCurrentPoint(null).Position.Y;
        _origin = TargetSize;

        // 拖的时候看得见：整条变成强调色，松手恢复。
        _line.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        Opacity = 0.35;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        double now = Horizontal ? e.GetCurrentPoint(null).Position.X : e.GetCurrentPoint(null).Position.Y;
        double delta = now - _start;
        TargetSize = _origin + (Invert ? -delta : delta);
        Changed?.Invoke();
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCapture(e.Pointer);
        End();
        e.Handled = true;
    }

    private void End()
    {
        if (!_dragging) return;
        _dragging = false;
        Opacity = 1;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _line.Background = IdleBrush;
        Changed?.Invoke();
    }

    private void SetCursor(InputSystemCursorShape shape) => ProtectedCursor = InputSystemCursor.Create(shape);
}
