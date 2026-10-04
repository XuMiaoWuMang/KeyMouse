using KeyMouse;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 按契约（`flow.schema.json`）给一步生成检查器。
///
/// 它只做排版：哪种步骤有哪些参数、哪些必填、取值与范围是什么、清空是什么意思，全部问契约。
/// 控件本身在 <see cref="InspectorFields"/> 里。排版上的三条规矩：默认值写进输入框（占位符）、
/// 说明收进 ⓘ 提示、选项少就摆一排；分组用留白与一条细竖线，不套灰底卡片。
/// </summary>
internal sealed class InspectorBuilder
{
    private readonly Panel _host;
    private readonly StepVm _step;
    private readonly InspectorFields _fields;
    private readonly Action<string> _pickPoint;
    private readonly Action<string> _pickRegion;

    internal InspectorBuilder(Panel host, StepVm step, Action<string> pickPoint, Action<string> pickRegion)
    {
        _host = host;
        _step = step;
        _fields = new InspectorFields(step);
        _pickPoint = pickPoint;
        _pickRegion = pickRegion;
    }

    internal void Build()
    {
        _host.Children.Clear();
        if (!FlowSchema.TryGetStep(_step.Step.Type, out FlowStepContract contract)) return;

        // 前提条件单独处理：它是一段"要不要"的开关，不是普通字段。
        AddSection(contract.Own.Where(f => f.Name != "when").ToList(), markRequired: true);
        AddWhen(contract);
        AddSection(contract.All.Where(f => f.Name == "note").ToList(), markRequired: false);
    }

    private void AddSection(IReadOnlyList<FlowField> fields, bool markRequired)
    {
        if (fields.Count == 0) return;
        var panel = new StackPanel { Spacing = 16 };   // 分区之间
        foreach (FlowField field in fields)
        {
            panel.Children.Add(Field(field, field.Name, markRequired && field.Required));
        }
        _host.Children.Add(Card(panel));
    }

    /// <summary>
    /// 前提条件：一个开关 + 唯一必填的那格（读到什么才算满足）。窗口与区域默认就是这一步自己的，
    /// 所以收在"换一个"里，不重复渲染同一个东西。
    /// </summary>
    private void AddWhen(FlowStepContract contract)
    {
        if (!contract.All.Any(f => f.Name == "when")) return;

        var panel = new StackPanel { Spacing = 12 };
        var toggle = new ToggleSwitch
        {
            Header = "只在满足条件时执行",
            OnContent = "满足才执行",
            OffContent = "每次都执行",
            IsOn = _step.GetText("when.text") is { Length: > 0 },
        };
        panel.Children.Add(toggle);

        var body = new StackPanel { Spacing = 12, Visibility = Ui.Show(toggle.IsOn) };
        panel.Children.Add(body);

        var text = new TextBox { PlaceholderText = "读到这段文字才执行，例如：登录成功" };
        text.Text = _step.GetText("when.text") ?? "";
        text.TextChanged += (_, _) => _step.SetText("when.text", text.Text);
        body.Children.Add(Label("读到什么才算满足（必填）", text,
            "在开始这一步之前先读一次；读到就执行，读不到就按下面的设置跳过或失败。"));

        body.Children.Add(new Expander
        {
            Header = "前提要读的窗口与区域（默认就是这一步的）",
            IsExpanded = false,
            Content = Advanced(),
        });

        toggle.Toggled += (_, _) =>
        {
            body.Visibility = Ui.Show(toggle.IsOn);
            if (toggle.IsOn) return;
            _step.SetText("when.text", null);   // 关掉 = 没有前提，不留半成品
            text.Text = "";
        };

        _host.Children.Add(Card(panel));
    }

    private UIElement Advanced()
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (FlowField field in FlowSchema.ShapeFields("when"))
        {
            if (field.Name == "text") continue;
            panel.Children.Add(Field(field, $"when.{field.Name}", required: false));
        }
        return panel;
    }

    private UIElement Field(FlowField field, string path, bool required)
    {
        string label = $"{field.LabelZh}{(required ? "  必填" : "")}";

        if (field.Kind is "steps" or "vars" or "stringList")
        {
            var block = new StackPanel { Spacing = 8 };
            block.Children.Add(Header(label));
            block.Children.Add(Note(field.Kind switch
            {
                "steps" => "子步骤在下面「这一步的 JSON」里编辑。",
                "vars" => "变量表在下面「这一步的 JSON」里编辑。",
                _ => "名字数组在下面「这一步的 JSON」里编辑。",
            }));
            return block;
        }

        UIElement control = field.Kind switch
        {
            "int" => _fields.NumberBox(field, path),
            "bool" => _fields.Toggle(field, path),
            "enum" => _fields.Choices(field, path),
            "multiline" => _fields.TextArea(field, path),
            "const" => Fixed(field, path),
            _ when FlowSchema.ShapeFields(field.Kind).Count > 0 => Shape(field, path, required),
            _ => _fields.TextBox(field, path),
        };

        // const 与嵌套结构自带标题：直接返回，否则同一个名字会出现两遍。
        if (field.Kind == "const" || FlowSchema.ShapeFields(field.Kind).Count > 0) return control;

        var fieldBlock = Label(label, control, field.HelpZh);
        if (_step.IssueFor(path) is { Length: > 0 } complaint)
        {
            // 规则 4.6：错误文字放在输入框下方，而不是只弹一个会消失的提示。
            fieldBlock.Children.Add(new TextBlock
            {
                Text = complaint,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["FieldCaption"],
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            });
        }
        return fieldBlock;
    }

    private UIElement Shape(FlowField field, string prefix, bool required)
    {
        IReadOnlyList<FlowField> fields = FlowSchema.ShapeFields(field.Kind);

        // 格式把坐标空间钉死时（拖拽只能客户区），别让人选一个会被加载器拒掉的值。
        if (field.FixedSpace is { Length: > 0 } pinned)
        {
            if (_step.GetText($"{prefix}.space") != pinned) _step.SetText($"{prefix}.space", pinned);
            fields = fields.Where(f => f.Name != "space").ToList();
        }

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        int columns = fields.Count <= 1 ? 1 : 2;
        for (int i = 0; i < columns; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        for (int i = 0; i < fields.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // 按"内容需要多宽"重排，而不是一律两列：数字与开关成对，文字/枚举这类长值占整行。
        // 这是结构性自适应——窄面板不会把进程名、窗口类名、标题截掉，也不会因此少看见参数。
        int row = 0, column = 0;
        foreach (FlowField inner in fields)
        {
            UIElement cell = Field(inner, $"{prefix}.{inner.Name}", required: false);
            bool wide = columns == 2 && inner.Kind is not ("int" or "bool");
            Grid.SetColumn((FrameworkElement)cell, column);
            Grid.SetRow((FrameworkElement)cell, row);
            if (wide && column == 0) Grid.SetColumnSpan((FrameworkElement)cell, 2);
            grid.Children.Add(cell);

            if (wide) { row++; column = 0; }
            else if (++column == columns) { column = 0; row++; }
        }

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(grid);
        if (field.Kind is "point" or "region")
        {
            var pick = new Button { Content = "从屏幕取一块…", HorizontalAlignment = HorizontalAlignment.Left };
            pick.Click += (_, _) =>
            {
                if (field.Kind == "region") _pickRegion(prefix);
                else _pickPoint(prefix);
            };
            body.Children.Add(pick);
        }

        var block = new StackPanel { Spacing = 8 };
        block.Children.Add(Header($"{field.LabelZh}{(required ? "（必填）" : "")}"));
        block.Children.Add(new Border
        {
            // 分组靠留白与一条细竖线，不再套一层灰底卡片：层级要看得懂，容器不必套容器。
            Margin = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(14, 2, 0, 2),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            Child = body,
        });
        return block;
    }

    /// <summary>固定值：不渲染输入控件，只在缺失时写进 JSON，并显示成一句说明。</summary>
    private UIElement Fixed(FlowField field, string path)
    {
        if (field.Constant is { Length: > 0 } value && _step.GetText(path) != value) _step.SetText(path, value);
        return Note($"{field.LabelZh}：{field.Constant}（固定）");
    }

    /// <summary>标签 + 一行 ⓘ（说明收在提示里，字段本身只占一行）。</summary>
    private static StackPanel Label(string label, UIElement control, string? help)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        head.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)Application.Current.Resources["FieldCaption"],
        });
        if (help is { Length: > 0 })
        {
            var info = new FontIcon
            {
                Glyph = "\uE946",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            };
            ToolTipService.SetToolTip(info, help);
            head.Children.Add(info);
        }

        var panel = new StackPanel { Spacing = 4 };   // 标签到控件
        panel.Children.Add(head);
        panel.Children.Add(control);
        return panel;
    }

    private static TextBlock Header(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["SectionHeader"],
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources["FieldCaption"],
    };

    private static Border Card(UIElement child) => new()
    {
        Style = (Style)Application.Current.Resources["CardBorder"],
        Child = child,
    };
}
