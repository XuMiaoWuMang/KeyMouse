using KeyMouse;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        _host.Children.Add(panel);
    }

    /// <summary>
    /// 前提条件：一个开关 + 唯一必填的那格（读到什么才算满足）。窗口与区域默认就是这一步自己的，
    /// 所以收在"换一个"里，不重复渲染同一个东西。
    /// </summary>
    private void AddWhen(FlowStepContract contract)
    {
        if (!contract.All.Any(f => f.Name == "when")) return;

        var panel = new StackPanel { Spacing = 12 };

        // 自绘开关：系统的 ToggleSwitch 开态是框架强调色，压不动，而这个界面上不借它。
        var toggleLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var toggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["KpSwitch"],
            IsChecked = _step.GetText("when.text") is { Length: > 0 },
            Content = toggleLabel,
        };
        void SyncToggleText() =>
            toggleLabel.Text = toggle.IsChecked == true ? "满足才执行" : "每次都执行";
        SyncToggleText();
        panel.Children.Add(toggle);

        var body = new StackPanel { Spacing = 12, Visibility = Ui.Show(toggle.IsChecked == true) };
        panel.Children.Add(body);

        var text = new TextBox { PlaceholderText = "读到这段文字才执行，例如：登录成功" };
        text.Text = _step.GetText("when.text") ?? "";
        text.TextChanged += (_, _) => _step.SetText("when.text", text.Text);
        body.Children.Add(Label("读到什么才算满足", text,
            "在开始这一步之前先读一次；读到就执行，读不到就按下面的设置跳过或失败。", required: true));

        // 进阶分区：标题按钮 + 可收起的块。不用 Expander——它会再画一张卡片，而这里只有纸和线。
        var advancedBody = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
        advancedBody.Children.Add(Advanced());

        var chevron = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["KpInk3Brush"],
        };
        var advancedHead = new Button
        {
            Style = (Style)Application.Current.Resources["ToolButton"],
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        var headGrid = new Grid { ColumnSpacing = 8 };
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headGrid.Children.Add(new TextBlock
        {
            Text = "前提要读的窗口与区域（默认就是这一步的）",
            Style = (Style)Application.Current.Resources["FieldLabel"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(chevron, 1);
        headGrid.Children.Add(chevron);
        advancedHead.Content = headGrid;
        advancedHead.Click += (_, _) =>
        {
            bool open = advancedBody.Visibility != Visibility.Visible;
            advancedBody.Visibility = Ui.Show(open);
            chevron.Glyph = open ? "\uE70E" : "\uE70D";
        };

        body.Children.Add(advancedHead);
        body.Children.Add(advancedBody);

        toggle.Checked += (_, _) =>
        {
            SyncToggleText();
            body.Visibility = Visibility.Visible;
        };
        toggle.Unchecked += (_, _) =>
        {
            SyncToggleText();
            body.Visibility = Visibility.Collapsed;
            _step.SetText("when.text", null);   // 关掉 = 没有前提，不留半成品
            text.Text = "";
        };

        _host.Children.Add(Section("只在满足条件时执行", required: false, panel));
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
        string label = field.LabelZh;

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

        var fieldBlock = Label(label, control, field.HelpZh, required);
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
        // 规格表是一列：字段名必须落在同一条竖线上，两列会把左栏劈成两段。
        const int columns = 1;
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

        return Section(field.LabelZh, required, body);
    }

    /// <summary>固定值：不渲染输入控件，只在缺失时写进 JSON，并显示成一句说明。</summary>
    private UIElement Fixed(FlowField field, string path)
    {
        if (field.Constant is { Length: > 0 } value && _step.GetText(path) != value) _step.SetText(path, value);
        return Note($"{field.LabelZh}：{field.Constant}（固定）");
    }

    /// <summary>
    /// 一个字段 = 一行标签（+ⓘ，右侧在需要时标「必填」）+ 控件。
    ///
    /// 「必填」不再拼进标签的文字里：拼进去以后每条标签长短不一，左边那张"标签列"就没有一条竖线可看，
    /// 而这条竖线正是表单能被一眼扫完的原因。它现在是一个独立的、右对齐的弱标记。
    /// </summary>
    private static StackPanel Label(string label, UIElement control, string? help, bool required = false)
    {
        // 规格表：左边 108 是一列右对齐的字段名，右边是值。字段名右对齐，值左对齐，
        // 中间那条空白就是"表"的竖线——它不必真的画出来。
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(108) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var gutter = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };

        var marks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };

        var head = new Grid { ColumnSpacing = 4 };
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
            marks.Children.Add(info);
        }

        if (required)
        {
            marks.Children.Add(new TextBlock
            {
                Text = "必填",
                Style = (Style)Application.Current.Resources["RequiredMark"],
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        head.Children.Add(new TextBlock
        {
            Text = label,
            Style = (Style)Application.Current.Resources["FieldLabel"],
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        });

        gutter.Children.Add(head);
        if (marks.Children.Count > 0) gutter.Children.Add(marks);

        row.Children.Add(gutter);
        Grid.SetColumn(control as FrameworkElement, 1);
        row.Children.Add(control);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(row);
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

    /// <summary>
    /// 分区：一行标题（需要时右侧标「必填」）+ 一条通栏 1px 线 + 内容。
    /// 层级由字重与线说清楚，所以不再套一层灰底卡片——容器不必套容器。
    /// </summary>
    private static UIElement Section(string title, bool required, UIElement body)
    {
        var head = new Grid { ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Header(title));

        if (required)
        {
            var mark = new TextBlock
            {
                Text = "必填",
                Style = (Style)Application.Current.Resources["RequiredMark"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(mark, 2);
            head.Children.Add(mark);
        }

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(head);
        stack.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)Application.Current.Resources["KpRuleBrush"],
            Margin = new Thickness(0, 2, 0, 6),
        });
        stack.Children.Add(body);
        return stack;
    }
}
