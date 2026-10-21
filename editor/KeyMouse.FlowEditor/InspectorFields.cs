using KeyMouse;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace KeyMouse.FlowEditor;

/// <summary>检查器里每种字段用什么控件、清空是什么意思。这里只有"渲染"，判断来自契约。</summary>
internal sealed partial class InspectorFields
{
    private readonly StepVm _step;

    internal InspectorFields(StepVm step) => _step = step;

    internal UIElement TextBox(FlowField field, string path)
    {
        var box = new TextBox
        {
            Text = _step.GetText(path) ?? "",
            PlaceholderText = Placeholder(field),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        box.TextChanged += (_, _) => _step.SetText(path, box.Text);
        return box;
    }

    internal UIElement TextArea(FlowField field, string path)
    {
        // 先把 AcceptsReturn 打开，再赋 Text：赋值时它还是单行框的话，WinUI 会把换行直接吃掉
        // （实测：多行文本切走再切回来只剩第一行）。
        var box = new TextBox
        {
            PlaceholderText = Placeholder(field),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 84,
        };
        box.Text = _step.GetText(path) ?? "";
        box.TextChanged += (_, _) => _step.SetText(path, box.Text);
        return box;
    }

    /// <summary>
    /// 数字框：可选字段清空 = 不写这个参数（用契约里的默认值）；必填字段**不允许清空**。
    ///
    /// 理由是实测出来的：数字字段表示不了"没填"。清空区域里的 `y` 之后，模型反序列化会把它变回 0，
    /// 而 0 是合法坐标，于是"没填"和"填了 0"再也分不出来，校验也就永远发现不了。与其加一层
    /// "到底填了没有"的影子状态，不如让必填的数字框还原上一个值。
    /// </summary>
    /// <summary>
    /// 数字框是**我们自己画的**：一个等宽右对齐的文本框，没有系统的步进钮。
    ///
    /// 两个理由。一是排版：数字该沿一条竖线对齐、按等宽排，那是这一页里"机器内容"的声音。
    /// 二是颜色：系统的 NumberBox 会在边框与按钮上借用框架强调色，那不在这个世界里。
    /// 加减用 ↑ ↓（每次 1，撞到契约的上下限就停），连点比拖那两颗小箭头快。
    /// </summary>
    internal UIElement NumberBox(FlowField field, string path)
    {
        int? current = int.TryParse(_step.GetText(path), out int parsed) ? parsed : null;
        var box = new TextBox
        {
            Text = current?.ToString() ?? "",
            PlaceholderText = Placeholder(field),
            FontFamily = (FontFamily)Application.Current.Resources["KpMonoFont"],
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        bool busy = false;
        void Apply(bool final)
        {
            if (busy) return;
            busy = true;
            try
            {
                string raw = box.Text.Trim();
                if (raw.Length == 0)
                {
                    if (!field.Required) _step.SetNumber(path, null);
                    else if (final) box.Text = current?.ToString() ?? "";
                    return;
                }

                if (!int.TryParse(raw, out int value))
                {
                    // 半截输入（"-"、"1x"）先不动模型；离开输入框时才还原成模型里的值。
                    if (final) box.Text = _step.GetText(path) ?? "";
                    return;
                }

                if (final)
                {
                    if (field.Min is int low && value < low) value = low;
                    if (field.Max is int high && value > high) value = high;
                    string shown = value.ToString();
                    if (shown != raw) box.Text = shown;
                }

                _step.SetNumber(path, value);
            }
            finally
            {
                busy = false;
            }
        }

        box.TextChanged += (_, _) => Apply(final: false);
        box.LostFocus += (_, _) => Apply(final: true);
        box.PreviewKeyDown += (_, e) =>
        {
            int step = e.Key switch
            {
                Windows.System.VirtualKey.Up => 1,
                Windows.System.VirtualKey.Down => -1,
                _ => 0,
            };
            if (step == 0) return;

            int value = (int.TryParse(box.Text.Trim(), out int parsedValue) ? parsedValue : field.Min ?? 0) + step;
            if (field.Min is int low && value < low) value = low;
            if (field.Max is int high && value > high) value = high;
            box.Text = value.ToString();
            box.SelectionStart = box.Text.Length;
            e.Handled = true;
        };

        return box;
    }

    /// <summary>开关也用自绘的那一颗：系统的 ToggleSwitch 开态是框架强调色。</summary>
    internal UIElement Toggle(FlowField field, string path) =>
        Switch(
            _step.GetBool(path),
            on => _step.SetBool(path, on),
            on => on ? "是" : "否");

    /// <summary>
    /// 一颗开关。**不用 ControlTemplate + VisualState**：实测那种写法点一次变色、点回来不变——
    /// ObjectAnimation 是保持的，"关"状态里漏写一个属性，那个属性就永远留着上一次的值。
    /// 这里是直接改属性：点下去，颜色、珠子、标签同一帧一起换，没有状态机可以不同步。
    /// </summary>
    internal static UIElement Switch(bool initial, Action<bool> changed, Func<bool, string> label)
    {
        var track = new Border
        {
            Width = 34,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
        };
        var knob = new Ellipse { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center };

        var host = new Grid { Width = 34, Height = 18 };
        host.Children.Add(track);
        host.Children.Add(knob);

        var caption = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var root = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        root.Children.Add(host);
        root.Children.Add(caption);

        bool on = initial;

        void Paint()
        {
            track.Background = Brush(on ? "KpSignalBrush" : "KpFieldBrush");
            track.BorderBrush = Brush(on ? "KpSignalBrush" : "KpInk3Brush");
            knob.Fill = Brush(on ? "KpOnSignalBrush" : "KpInk3Brush");
            knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            knob.Margin = on ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0);
            caption.Text = label(on);
        }

        root.Tapped += (_, e) =>
        {
            on = !on;
            Paint();
            changed(on);
            e.Handled = true;
        };

        Paint();
        return root;
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>选项少就摆一排（省一次点击，一眼看全），多了才用下拉框。</summary>
    internal UIElement Choices(FlowField field, string path)
    {
        string? current = _step.GetText(path);
        string preset = field.Default ?? "";
        string auto = preset.Length > 0 ? $"默认（{preset}）" : "不设置";

        if (field.Values.Length is > 0 and <= 4)
        {
            // 两列网格而不是一排：四个中文选项在窄面板里会被裁掉一个半。
            var row = new StackPanel { Spacing = 4 };
            var buttons = new List<(RadioButton Button, string? Value)>();
            string group = $"enum-{path}";

            void Add(string label, string? value)
            {
                var radio = new RadioButton { Content = label, GroupName = group };
                radio.Checked += (_, _) =>
                {
                    if (radio.IsChecked == true) _step.SetText(path, value);
                };
                row.Children.Add(radio);
                buttons.Add((radio, value));
            }

            Add(auto, null);
            foreach (string value in field.Values)
            {
                Add(field.ValueLabels.TryGetValue(value, out string? zh) ? zh : value, value);
            }
            foreach ((RadioButton button, string? value) in buttons)
            {
                button.IsChecked = string.Equals(value, current, StringComparison.Ordinal);
            }
            return row;
        }

        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = Placeholder(field) };
        combo.Items.Add(new ComboBoxItem { Content = auto });
        foreach (string value in field.Values) combo.Items.Add(new ComboBoxItem { Content = field.ValueLabels.TryGetValue(value, out string? zh) ? zh : value });
        combo.SelectedIndex = current is { Length: > 0 } ? Math.Max(0, Array.IndexOf(field.Values, current) + 1) : 0;
        combo.SelectionChanged += (_, _) => _step.SetText(path, combo.SelectedIndex <= 0 ? null : field.Values[combo.SelectedIndex - 1]);
        return combo;
    }

    /// <summary>占位符只放"留空会用什么"：短、可扫。说明文字在 ⓘ 提示里。</summary>
    internal static string Placeholder(FlowField field) =>
        field.Default is { Length: > 0 } value ? $"默认 {value}" : "";
}
