using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage.Pickers;
using KeyMouse;
using KeyMouse.Runner;

namespace KeyMouse.FlowEditor;

/// <summary>步骤与检查器：调色板、插入、重排、四处拾取（窗口/区域/像素），以及输出面板的开关。</summary>
public sealed partial class MainWindow
{
    // ---------------------------------------------------------------- 运行输出：有内容才占地方

    private bool _logShown;

    /// <summary>
    /// 这次运行的计划（循环展开、子流程内联后的线性序列）。它只用来回答一个界面问题：
    /// Runner 报的"第 N 个执行项"对应界面上哪一行。
    /// </summary>
    private FlowPlan? _plan;

    /// <summary>没输出时整块收起——一个空的输出面板占着半屏，是最没道理的浪费。</summary>
    public Visibility OutputVisibility => Ui.Show(_logShown);

    /// <summary>面板收起来时露出的那一小块「输出」。</summary>
    public Visibility OutputChipVisibility => Ui.Show(!_logShown);

    /// <summary>把输出面板叫回来（默认高度：窗口高度的三成，夹在 140–260 之间）。</summary>
    private void OnShowOutput(object sender, RoutedEventArgs e) => OpenLog();

    public Visibility LogSplitterVisibility => Ui.Show(_logShown);

    private void OnClearLog(object sender, RoutedEventArgs e)
    {
        LogBox.Text = "";
        _logShown = false;
        LogRow.Height = GridLength.Auto;
        RunSummary.Visibility = Visibility.Collapsed;
        NotifyOutput();
    }

    /// <summary>
    /// 工具栏左边是"往流程里加一步"——这是这个编辑器最常做的事，所以常用类型直接是一排按钮，
    /// 其余在"更多"里。打开/保存这类文件操作不常做，收进了"文件"菜单。
    /// </summary>
    // 顶部快捷栏只放**最常用**的四个：切换窗口/长按这些留在「更多」里，不占位置。
    private static readonly string[] CommonSteps = ["click", "type", "wait-text", "click-text"];

    private void FillPalette()
    {
        PaletteHost.Children.Clear();
        _paletteTypes = CommonSteps;
        int take = _paletteShown < 0 ? CommonSteps.Length : Math.Max(1, _paletteShown);
        foreach (string type in CommonSteps.Take(take))
        {
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["PaletteButton"],

                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new FontIcon { Glyph = StepCatalog.GlyphOf(type), FontSize = 14 },
                        new TextBlock { Text = StepCatalog.NameOf(type), VerticalAlignment = VerticalAlignment.Center },
                    },
                },
            };
            ToolTipService.SetToolTip(button, DescribeStep(type));
            button.Click += (_, _) => AddStep(type);
            PaletteHost.Children.Add(button);
        }
    }

    /// <summary>按钮上的一句话提示来自契约（每种类型自己的摘要），不在这里另写一份。</summary>
    private static string DescribeStep(string type) => FlowSchema.TryGetStep(type, out FlowStepContract contract)
        ? $"{contract.SummaryZh}（{type}）"
        : type;

    /// <summary>
    /// 加一步：加在选中那一步之后；选中的是组内子步骤，就加进那个组里（所见即所得）。
    /// 返回值是显示行里真实的那个 VM，选中它而不是"下一个位置"。
    /// </summary>
    private void AddStep(string type)
    {
        StepVm added = Model.Add(type, SelectedStep);
        StepList.SelectedItem = added;
    }

    /// <summary>系统是否允许动画。关掉时一切过渡都免了。</summary>
    private static bool AnimationsEnabled =>
        new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

    private static async Task FadeInAsync(UIElement element)
    {
        element.Opacity = 0;
        for (int step = 1; step <= 6; step++)
        {
            element.Opacity = step / 6.0;
            await Task.Delay(20);
        }
    }

    /// <summary>Delete 键删掉选中的那一步（右键菜单里也有同一条）。</summary>
    private void OnStepListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Delete) return;
        OnDelete(sender, new RoutedEventArgs());
        e.Handled = true;
    }

    /// <summary>点检查器里的证据图 = 用系统看图工具打开原图。</summary>
    private void OnInspectorShotTapped(object sender, TappedRoutedEventArgs e)
    {
        if (SelectedStep?.ShotPath is not { Length: > 0 } path || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Show("打不开截图", ex.Message, InfoBarSeverity.Error); }
    }

    /// <summary>
    /// 常用步骤按可用宽度收放：窗口窄了就少放几个（其余仍在「更多」里），
    /// 绝不让某个按钮被裁成半个——那是最像"没做完"的一种残缺。
    /// </summary>
    private void OnPaletteSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_paletteTypes is null) return;
        double available = e.NewSize.Width;
        if (available <= 0) return;

        int fit = 0;
        double used = 0;
        foreach (string type in _paletteTypes)
        {
            double need = StepCatalog.NameOf(type).Length * 13 + 72;   // 图标 + 边距的粗略估算
            if (used + need > available && fit > 0) break;
            used += need;
            fit++;
        }
        if (fit == _paletteShown) return;
        _paletteShown = fit;
        FillPalette();
    }

    /// <summary>
    /// 窗口变小以后，日志区如果还占着原来的高度就会把编辑区挤没——这时只**往下压到上限**，
    /// 不去"恢复默认高度"：用户自己拉出来的尺寸，只有在真的放不下时才动它。
    /// </summary>
    private void CapLogHeight(double windowHeight)
    {
        if (!_logShown || windowHeight <= 0) return;

        double cap = Math.Clamp(windowHeight * 0.55, 160, 640);
        if (!LogRow.Height.IsAbsolute || LogRow.Height.Value <= cap) return;
        LogRow.Height = new GridLength(cap);
    }

    /// <summary>夹紧到最小尺寸。两位对手的最低标准：任何尺寸下不出现半个字、动作一屏可达。</summary>
    private void EnforceMinimumSize()
    {
        const int minWidth = 1360;    // 物理像素（150% 缩放下约合 907 逻辑像素）
        const int minHeight = 960;
        var size = AppWindow.Size;
        if (size.Width >= minWidth && size.Height >= minHeight) return;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Max(size.Width, minWidth), Math.Max(size.Height, minHeight)));
    }

    private void BuildInspector()
    {
        InspectorHost.Children.Clear();
        if (SelectedStep is not { } step) return;

        // 子流程预览行是只读的：这里不渲染契约字段（那会让人以为改了会生效），
        // 只说清楚它来自哪个文件、该去哪儿改。
        if (step.IsPreview)
        {
            InspectorHost.Children.Add(new TextBlock
            {
                Text = step.PreviewNote,
                Style = (Style)Application.Current.Resources["FieldCaption"],
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        new InspectorBuilder(InspectorHost, step, path => PickInto(step, path, isRegion: false), path => PickInto(step, path, isRegion: true)).Build();
    }

    /// <summary>取一次区域或点，写进契约给的那条路径（`region` / `at` / `when.region` 都可能）。</summary>
    private async void PickInto(StepVm step, string path, bool isRegion)
    {
        try
        {
            RunnerClient? client = await EnsureClientAsync();
            if (client is null) return;

            if (isRegion)
                Show("取区域", "用鼠标框一块区域：拖拽框选，单击选整个客户区，ESC 取消。", InfoBarSeverity.Informational);
            else
                Show("取点", "在屏幕上点一下要操作的位置，ESC 或右键取消。", InfoBarSeverity.Informational);

            PickedRegion? picked = isRegion
                ? await KeyMouseBridge.PickRegionAsync(client)
                : await KeyMouseBridge.PickPointAsync(client);
            BringToFront();
            if (picked is null)
            {
                Show(isRegion ? "取区域" : "取点", "取消了，没有改动。", InfoBarSeverity.Warning);
                return;
            }

            if (isRegion)
            {
                step.SetRegion(path, new FlowRegion
                {
                    Space = FlowSpace.Client, X = picked.X, Y = picked.Y, Width = picked.Width, Height = picked.Height,
                });
            }
            else
            {
                // 没有指定窗口的步骤：存**屏幕坐标**，就到那个位置发事件，跟谁在前台无关。
                bool anchored = step.GetText("target.process") is { Length: > 0 }
                                || step.GetText("target.class") is { Length: > 0 };
                step.SetPoint(path, anchored
                    ? new FlowPoint { Space = picked.Space == "screen" ? FlowSpace.Screen : FlowSpace.Client, X = picked.X, Y = picked.Y }
                    : new FlowPoint { Space = FlowSpace.Screen, X = picked.ScreenX, Y = picked.ScreenY });
            }

            // 只有本来就有窗口的步骤才回填窗口；没窗口的步骤不该因为取了个点就被绑上窗口。
            bool hasWindow = step.GetText("target.process") is { Length: > 0 }
                             || step.GetText("target.class") is { Length: > 0 };
            string prefix = path.Contains('.') ? path[..path.LastIndexOf('.')] : "target";
            if (hasWindow)
            {
                if (picked.Process is { Length: > 0 }) step.SetText($"{prefix}.process", picked.Process);
                if (picked.Class is { Length: > 0 }) step.SetText($"{prefix}.class", picked.Class);
                if (picked.Title is { Length: > 0 }) step.SetText($"{prefix}.title", picked.Title);
            }

            // 数值是**程序写进去**的：输入框不会自己知道，必须重建面板，
            // 否则取了点以后 X/Y 还显示 0（实测）。
            BuildInspector();

            Show(isRegion ? "取区域" : "取点",
                isRegion
                    ? $"记下 {picked.Width}x{picked.Height} @ {picked.X},{picked.Y}，窗口 {picked.Process ?? picked.Class ?? "（无）"}"
                    : $"记下点 {picked.X},{picked.Y}（{(picked.Space == "screen" ? "屏幕" : "窗口内")}坐标），窗口 {picked.Process ?? picked.Class ?? "（无）"}",
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show(isRegion ? "取区域失败" : "取点失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnPickRegion(object sender, RoutedEventArgs e)
    {
        if (SelectedStep is not { } step) return;
        try
        {
            RunnerClient? client = await EnsureClientAsync();
            if (client is null) return;

            Show("取区域", "用鼠标框一块区域：拖拽框选，单击选整个客户区，ESC 取消。", InfoBarSeverity.Informational);
            PickedRegion? picked = await KeyMouseBridge.PickRegionAsync(client);
            if (picked is null)
            {
                Show("取区域", "取消了，没有改动。", InfoBarSeverity.Warning);
                return;
            }

            // 写进契约里的参数名：这一步用 region 就写 region，否则写 at（点）。不按类型写死字段，
            // 而是问契约这一步有没有这个参数。
            bool wantsRegion = step.Fields.Any(f => f.Name == "region" && f.Kind == "region");
            if (wantsRegion)
            {
                step.SetRegion("region", new FlowRegion
                {
                    Space = FlowSpace.Client, X = picked.X, Y = picked.Y, Width = picked.Width, Height = picked.Height,
                });
            }
            else
            {
                step.SetPoint("at", new FlowPoint
                {
                    Space = picked.Space == "screen" ? FlowSpace.Screen : FlowSpace.Client,
                    X = picked.X, Y = picked.Y,
                });
            }
            step.SetText("target.process", picked.Process);
            step.SetText("target.class", picked.Class ?? "");
            step.SetText("target.title", picked.Title);

            Show("取区域", $"记下 {picked.Width}x{picked.Height} @ {picked.X},{picked.Y}，" +
                           $"窗口 {picked.Process ?? picked.Class ?? "（无）"}" +
                           (picked.Note is { Length: > 0 } note ? $"；{note}" : ""),
                InfoBarSeverity.Success);
            BringToFront();
        }
        catch (Exception ex)
        {
            Show("取区域失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    /// <summary>Brings this window back to the front after the overlay took the foreground.</summary>
    private void BringToFront()
    {
        try { Activate(); }
        catch (Exception) { /* the window may already be closing */ }
    }

}
