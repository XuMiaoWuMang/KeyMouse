using System.Text.Json;
using KeyMouse;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace KeyMouse.FlowEditor;

/// <summary>
/// 空状态与"添加步骤"这两处的组织方式。
///
/// 两处都是**回答用户的问题**，不是把组件摆开：
/// 空状态回答"从哪儿开始"（最近文件通常就是答案），步骤选择回答"这一步要哪个类型"（按用途分组，
/// 每组一句话说明它干什么）。所以文案来自契约（每种步骤自己的摘要），不在这里另编一份。
/// </summary>
internal sealed partial class Surfaces
{
    private readonly MainWindow _window;

    internal Surfaces(MainWindow window) => _window = window;

    // ------------------------------------------------------------------ 空状态：最近打开

    /// <summary>步骤按用途分组：15 个类型拉成一列是"更长的列表"，分组才是能扫的列表。</summary>
    internal static readonly (string Title, string[] Types)[] Groups =
    [
        ("窗口", ["focus", "wait-window"]),
        ("输入", ["click", "type", "key", "drag", "move", "wheel"]),
        ("看屏幕", ["wait-text", "click-text", "read-text"]),
        ("流程", ["sleep", "repeat", "foreach", "call"]),
    ];

    internal void FillRecent(StackPanel host, Panel emptyState, EditorSettings settings)
    {
        host.Children.Clear();
        var block = (StackPanel)((StackPanel)emptyState).Children
            .First(child => child is StackPanel { Name: "RecentBlock" });
        block.Visibility = Ui.Show(settings.Recent.Count > 0);

        foreach (string path in settings.Recent)
        {
            var row = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 12, 6),
                CornerRadius = new CornerRadius(6),
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = Path.GetFileName(path) },
                        new TextBlock
                        {
                            Text = path,
                            Style = (Style)Application.Current.Resources["FieldCaption"],
                            TextTrimming = TextTrimming.CharacterEllipsis,
                        },
                    },
                },
            };
            row.Click += (_, _) => _window.OpenPath(path);
            host.Children.Add(row);
        }
    }

    // ------------------------------------------------------------------ 添加步骤：分组选择

    /// <summary>
    /// 选择步骤的面板：按用途分四组，每项一行（图标 + 名字 + 契约给的一句话）。
    /// 不做下拉里一长串平铺，也不给每项加状态点（那是 AI 味装饰）。
    /// </summary>
    internal void PickStep(FrameworkElement anchor, Action<string> add)
    {
        var content = new StackPanel { Spacing = 16, Width = 440 };
        foreach ((string title, string[] types) in Groups)
        {
            content.Children.Add(new TextBlock
            {
                Text = title,
                Style = (Style)Application.Current.Resources["MicroLabel"],
            });
            content.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)Application.Current.Resources["KpRuleBrush"],
            });

            var group = new StackPanel { Spacing = 0 };
            for (int i = 0; i < types.Length; i++)
            {
                group.Children.Add(Row(types[i], add));
                // 行间一条从图标右侧起画的细线：一整列选项因此读成一张表，而不是一堆按钮。
                if (i < types.Length - 1)
                {
                    group.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(30, 0, 0, 0),
                        Background = (Brush)Application.Current.Resources["KpRuleBrush"],
                    });
                }
            }
            content.Children.Add(group);
        }

        // 用 Flyout 而不是 ContentDialog：弹出面板该"点外面就关"，而对话框天生是模态的，
        // 点外面没有任何反应（实测反馈）。Flyout 的轻点关闭是内建行为，Esc 也认。
        var flyout = new Flyout
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft,
            FlyoutPresenterStyle = (Style)Application.Current.Resources["KpPopoverPresenter"],
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 16,
                    Width = 440,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "加一步",
                            Style = (Style)Application.Current.Resources["PaneTitle"],
                        },
                        content,
                        new TextBlock
                        {
                            Text = "可以连着加，点面板外面关闭",
                            Style = (Style)Application.Current.Resources["FieldCaption"],
                        },
                    },
                },
                MaxHeight = 470,
            },
        };

        // 面板**不**在选中后关闭：一次要加好几步是常态，每加一步都得重新打开太碎。
        // 关闭只留给"点面板外面"——那本来就是 Flyout 的内建行为。
        flyout.ShowAt(anchor);
    }

    private UIElement Row(string type, Action<string> add)
    {
        var row = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 8, 8, 8),
            CornerRadius = new CornerRadius(4),
        };

        var line = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new FontIcon
                {
                    Glyph = StepCatalog.GlyphOf(type),
                    FontSize = 13,
                    Width = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["KpInk2Brush"],
                },
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = StepCatalog.NameOf(type),
                            Style = (Style)Application.Current.Resources["SectionHeader"],
                        },
                        new TextBlock
                        {
                            Text = Describe(type),
                            Style = (Style)Application.Current.Resources["FieldCaption"],
                        },
                    },
                },
            },
        };
        row.Content = line;
        row.Click += (_, _) => add(type);
        return row;
    }

    /// <summary>一句话说明来自契约（每种步骤自己的摘要），不在这里另写一份。</summary>
    private static string Describe(string type) =>
        FlowSchema.TryGetStep(type, out FlowStepContract contract) && contract.SummaryZh.Length > 0
            ? contract.SummaryZh
            : type;
}
