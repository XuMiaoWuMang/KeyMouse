using KeyMouse;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
        var box = new TextBox
        {
            Text = _step.GetText(path) ?? "",
            PlaceholderText = Placeholder(field),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 84,
        };
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
    internal UIElement NumberBox(FlowField field, string path)
    {
        double current = double.TryParse(_step.GetText(path), out double value) ? value : double.NaN;
        var box = new NumberBox
        {
            Value = current,
            PlaceholderText = Placeholder(field),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (field.Min is int min) box.Minimum = min;
        if (field.Max is int max) box.Maximum = max;

        bool restoring = false;
        box.ValueChanged += (_, e) =>
        {
            if (restoring) return;
            if (!double.IsNaN(e.NewValue))
            {
                _step.SetNumber(path, (int)e.NewValue);
                return;
            }

            if (!field.Required)
            {
                _step.SetNumber(path, null);
                return;
            }

            restoring = true;
            box.Value = double.IsNaN(current) ? 0 : current;
            restoring = false;
        };
        return box;
    }

    internal UIElement Toggle(FlowField field, string path)
    {
        var toggle = new ToggleSwitch { IsOn = _step.GetBool(path), OnContent = "是", OffContent = "否" };
        toggle.Toggled += (_, _) => _step.SetBool(path, toggle.IsOn);
        return toggle;
    }

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
