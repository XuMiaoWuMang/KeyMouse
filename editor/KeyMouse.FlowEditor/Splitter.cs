using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 一条可以拖的分隔条。WinUI 3 没有 GridSplitter，而"配置区能不能宽一点"是每天都要用手做的事，
/// 所以这里自己做一条：按住它拖，改目标行/列的尺寸；悬停显示调整光标，拖动时整条高亮。
///
/// 派生自 <see cref="Button"/> 而不是 ContentControl：**没有模板的 ContentControl 不参与命中测试**
/// （实测：拖拽永远落到别的控件上），Button 有模板、可命中，而且能在类内部设置调整光标。
/// </summary>
public sealed partial class Splitter : Button
{
    private bool _dragging;
    private double _start;
    private double _origin;

    public Splitter()
    {
        // 8px 的可拖区域，里面画一条 1px 的线：看得见才拖得动。
        Width = 8;
        Height = 8;
        MinWidth = 0;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(2, 0, 0, 0);   // 2px：1px 落在小数像素上会被摊薄成看不见（实测差 1/255）
        CornerRadius = new CornerRadius(0);
        UseSystemFocusVisuals = false;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        PointerEntered += (_, _) =>
        {
            SetCursor(InputSystemCursorShape.SizeWestEast);
            BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        };
        PointerExited += (_, _) =>
        {
            SetCursor(InputSystemCursorShape.Arrow);
            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
        };
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => End();
    }

    /// <summary>要调整的列（Direction=Horizontal）或行（Direction=Vertical）。</summary>
    public DependencyObject? Target { get; set; }

    public string Direction { get; set; } = "Horizontal";

    /// <summary>那条看得见的线画在哪一侧（Left / Top）。</summary>
        private string _edge = "Left";

    /// <summary>那条看得见的线画在哪一侧（Left / Top）。</summary>
    public string Edge
    {
        get => _edge;
        set
        {
            _edge = value;
            BorderThickness = string.Equals(value, "Top", StringComparison.OrdinalIgnoreCase)
                ? new Thickness(0, 2, 0, 0)
                : new Thickness(2, 0, 0, 0);
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

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Target is null) return;
        _dragging = true;
        _start = Horizontal ? e.GetCurrentPoint(null).Position.X : e.GetCurrentPoint(null).Position.Y;
        _origin = TargetSize;

        // 拖的时候看得见：整条变成强调色，松手恢复。
        BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
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
        _dragging = false;
        Opacity = 1;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
    }

    private void SetCursor(InputSystemCursorShape shape) => ProtectedCursor = InputSystemCursor.Create(shape);
}