using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M4 工作区面板：从 <see cref="GitRepository"/> 拉 working tree status + diff，
/// 把"未提交改动列表" + "查看变更"按钮 + summary 文字封装在一个 UserControl 里。
///
/// <para>
/// 之前是内联在 <see cref="MainWindow"/> 里的一个 Border/StackPanel。抽出来的目的：
/// </para>
/// <list type="bullet">
///   <item>MainWindow.axaml 从 ~95 行降到 ~70 行，关注点只剩"装配面板"</item>
///   <item>M4 的状态/事件/格式自包含，加 M5 (staging) / M6 (commit 按钮) 时不会让 MainWindow 继续膨胀</item>
///   <item>headless 测试可以单独加载这个面板，不开 MainWindow，断言更聚焦</item>
/// </list>
///
/// <para>
/// 与 <see cref="MainWindow"/> 的接缝只有两个：
/// </para>
/// <list type="bullet">
///   <item><see cref="Load"/>：外部（MainWindow 打开仓库后）把 <see cref="GitRepository"/> 喂进来</item>
///   <item><see cref="DiffRequested"/>：用户点"查看变更" / 双击行时面板发出事件，
///         MainWindow 收到后调 <see cref="GitRepository.GetWorkingTreeDiff"/> 弹 <c>DiffWindow</c>。
///         这里面板不直接弹窗是为了让"弹窗策略"（嵌入主窗口 / 独立窗口 / 多窗口）由 MainWindow 决定</item>
/// </list>
/// </summary>
public partial class WorkingTreePanel : UserControl
{
    private ListBox? _list;
    private Button? _viewButton;
    private TextBlock? _summaryText;

    /// <summary>当前绑定的仓库。null 表示"未打开仓库"状态。</summary>
    public GitRepository? Repository { get; private set; }

    /// <summary>用户请求查看某条改动的 diff 时触发（点按钮 / 双击列表行）。参数是要查看的 WorkingTreeChange，
    /// 若用户未选中任何行，参数为 <c>null</c>（MainWindow 据此给"请先选中"提示）。</summary>
    public event EventHandler<WorkingTreeChange?>? DiffRequested;

    public WorkingTreePanel()
    {
        InitializeComponent();
        _list = this.FindControl<ListBox>("WorkingTreeList");
        _viewButton = this.FindControl<Button>("ViewWorkingTreeDiffButton");
        _summaryText = this.FindControl<TextBlock>("WorkingTreeSummaryText");

        if (_summaryText != null)
        {
            _summaryText.Text = "未打开仓库。";
        }
        if (_list != null)
        {
            _list.SelectionChanged += (_, _) => OnSelectionChanged();
            _list.DoubleTapped += (_, _) => RaiseDiffRequested();
        }
        if (_viewButton != null)
        {
            _viewButton.Click += (_, _) => RaiseDiffRequested();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入仓库（开新仓库 / 重新加载时调用）。</summary>
    public void Load(GitRepository? repo)
    {
        Repository = repo;
        if (repo == null)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "未打开仓库。";
            return;
        }
        try
        {
            WorkingTreeChange[] changes = repo.GetWorkingTreeChanges();
            if (_list != null) _list.ItemsSource = changes;
            int staged = changes.Count(c => c.Staged);
            int unstaged = changes.Count(c => c.Unstaged && !c.Untracked());
            int untracked = changes.Count(c => c.Kind == WorkingTreeStatusKind.Untracked);
            if (_summaryText != null)
            {
                _summaryText.Text = changes.Length == 0
                    ? "工作区干净（相对 HEAD 无改动）。"
                    : $"共 {changes.Length} 项：已暂存 {staged}，未暂存 {unstaged}，未跟踪 {untracked}。";
            }
        }
        catch (Exception ex)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "加载工作区失败：" + ex.Message;
        }
    }

    /// <summary>当前选中的 working tree 改动（用于 MainWindow 弹 DiffWindow 时取 path）。</summary>
    public WorkingTreeChange? SelectedChange => _list?.SelectedItem as WorkingTreeChange;

    private void OnSelectionChanged()
    {
        // 选择变化时仅更新状态文字反馈，具体查看由 ViewDiffButton / DoubleTapped 触发
        if (SelectedChange is { } c)
        {
            // 通过 RoutedEvent 链让 MainWindow 监听（panel 自身不弹窗）
            SelectionChangedHint?.Invoke(this, $"{c.Path} ({c.Kind}, staged={c.Staged}, unstaged={c.Unstaged})");
        }
    }

    /// <summary>状态文字提示（MainWindow 把这个写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    private void RaiseDiffRequested()
    {
        // 总是触发 DiffRequested，让 MainWindow 决定怎么处理（无选择 / 无仓库 → 弹提示）。
        // 传 null 表示"用户点了查看但没选行"，与"选了一行然后点"是两种不同意图，
        // MainWindow 在事件处理里通过 SelectedChange is null / _repo == null 来给状态文字。
        DiffRequested?.Invoke(this, SelectedChange!);
    }
}

internal static class WorkingTreeChangeExt
{
    /// <summary>判断是否是 untracked（不在 git 索引里）。</summary>
    public static bool Untracked(this WorkingTreeChange c) => c.Kind == WorkingTreeStatusKind.Untracked;
}
