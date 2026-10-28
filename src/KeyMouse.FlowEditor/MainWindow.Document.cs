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

/// <summary>文档：新建、打开、保存、最近文件、脏标记，以及编辑区里那几条增删改。</summary>
public sealed partial class MainWindow
{
    // ------------------------------------------------------------------ files

    public void OpenFile(string path)
    {
        try
        {
            Model.Load(path);
        _hasDocument = true;
        _settings.Remember(path);
        RefreshStart();
            StepList.SelectedIndex = Model.Rows.Count > 0 ? 0 : -1;
            Model.MarkClean();   // 检查器渲染时写的默认值不算用户的改动
            LogBox.Text = "";
            // 打开这件事走提示条，不占"运行输出"——那个面板只留给运行。
            // 打开成功不弹提示：文件路径已经在标题栏上，弹一次只是噪音。
        }
        catch (CommandFailure ex)
        {
            Show($"读不了这个流程（退出码 {ex.Code}）", ex.Message, InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            Show("读不了这个流程", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    private async void OnOpen(object sender, RoutedEventArgs e)
    {
        string? path = await Picker.OpenFlowAsync(WinRT.Interop.WindowNative.GetWindowHandle(this));
        if (path is not null) OpenFile(path);
    }

    /// <summary>
    /// 保存或运行之前先就地校验：错误的落点应该在**具体字段下方**，而不是等加载器回一句"第 N 步需要某某"。
    /// 校验读的是同一份契约，所以两边不会说两套话。
    /// </summary>
    private bool ReadyForUse()
    {
        int issues = Model.Validate();
        if (issues == 0) return true;

        BuildInspector();
        int firstBad = Model.Rows.ToList().FindIndex(s => s.HasIssues) + 1;
        Show("还差几项没填", $"第 {firstBad} 步有 {issues} 处必填项没填，已在对应字段下方标出。", InfoBarSeverity.Warning);
        return false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!ReadyForUse()) return;
        if (Model.DocumentPath is null)
        {
            OnSaveAs(sender, e);
            return;
        }
        try
        {
            Model.Save(Model.DocumentPath);
            Show("已保存", Model.DocumentPath, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("保存失败", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    private async void OnSaveAs(object sender, RoutedEventArgs e)
    {
        string suggested = Model.DocumentPath is { Length: > 0 } existing
            ? Path.GetFileNameWithoutExtension(existing)
            : "flow";
        string? path = await Picker.SaveFlowAsync(WinRT.Interop.WindowNative.GetWindowHandle(this), suggested);
        if (path is null) return;
        try
        {
            Model.Save(path);
            Show("已保存", path, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Show("保存失败", ex.Message, InfoBarSeverity.Error);
        }
        UpdateStates();
    }

    // ------------------------------------------------------------------ editing

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int offset)
    {
        if (SelectedStep is not { } step) return;
        int target = Model.Rows.IndexOf(step) + offset;
        Model.Move(step, offset);
        StepList.SelectedIndex = target;
    }

    private void OnDuplicate(object sender, RoutedEventArgs e)
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Rows.IndexOf(step);
        Model.Duplicate(step);
        StepList.SelectedIndex = index + 1;
    }

    private void OnDelete(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        if (SelectedStep is not { } step) return;
        int index = Model.Rows.IndexOf(step);
        Model.Remove(step);
        StepList.SelectedIndex = Model.Rows.Count == 0 ? -1 : Math.Min(index, Model.Rows.Count - 1);
    }


    // ---------------------------------------------------------------- 三个入口：新建、打开路径、更多步骤

    /// <summary>新建：清空当前流程（未保存时会先问一句），然后停在空状态等下一步。</summary>
    private async void OnNewFlow(object sender, RoutedEventArgs e)
    {
        if (Model.Rows.Count > 0 && !await ConfirmDiscardAsync("新建流程"))
        {
            return;
        }
        Model.Reset();
        _hasDocument = true;   // 新建之后是"空白文档"，不是"还没打开文件"
        StepList.SelectedIndex = -1;
        UpdateStates();
    }

    /// <summary>空状态里的"最近打开"用它；也顺手记住这次打开。</summary>
    internal void OpenPath(string path)
    {
        if (File.Exists(path)) OpenFile(path);
        else
        {
            _settings.Recent.Remove(path);
            RefreshStart();
            Show("文件不在了", path, InfoBarSeverity.Warning);
        }
    }

    private void OnMoreSteps(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_surfaces is null) _surfaces = new Surfaces(this);
            _surfaces.PickStep(MoreStepsButton, AddStep);
        }
        catch (Exception ex)
        {
            Show("打不开步骤选择面板", ex.Message, InfoBarSeverity.Error);
        }
    }
    private void RefreshStart() =>
        _surfaces?.FillRecent(RecentList, EmptyState, _settings);

    /// <summary>有未保存改动时问一句。工具不该在没提示的情况下丢掉别人的东西。</summary>
    private async Task<bool> ConfirmDiscardAsync(string action)
    {
        if (!Model.IsDirty) return true;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = $"{action}前先保存吗？",
            Content = "当前流程有未保存的改动。",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
        };
        // 不能在这里同步等待：ShowAsync 需要 UI 线程，同步等待会把 UI 线程锁死（点了没反应就是这个）。
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return false;
        if (result == ContentDialogResult.Primary) OnSave(this, new RoutedEventArgs());
        return true;
    }

}
