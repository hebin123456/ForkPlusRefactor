using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Graph;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M2 + M3 提交面板：带提交图（DAG 线条 + ref 装饰列）的提交列表 + "查看变更（M3）"按钮。
///
/// <para>
/// 之前是内联在 <see cref="MainWindow"/> 里的 ListBox + DockPanel。抽出来的目的：
/// </para>
/// <list type="bullet">
///   <item>把 M2（提交列表输出）和 M3（提交 diff 触发器）打包到一个面板 —— 它们共享同一个
///         CommitsList，强行拆开会让人在两个面板里互相 FindControl 对方的 ListBox，更糟</item>
///   <item>MainWindow 进一步瘦身到只剩 M1 仓库 / 分支 + 装配 M2+M3 / M4 面板</item>
///   <item>未来 M5（staging 按钮） / M6（commit 按钮）也只需要在面板里加控件 + 暴露事件</item>
/// </list>
///
/// <para>
/// 与 <see cref="MainWindow"/> 的接缝：
/// </para>
/// <list type="bullet">
///   <item><see cref="LoadCommits"/>：外部（MainWindow 选分支后）把 <c>GitCommit[]</c> 喂进来；
///         内部走 <see cref="CommitGraphLayout.Compute"/> 摊成 <see cref="CommitGraphRow"/> 行
///         再喂给 ListBox（ListBox 的 DataTemplate 按 <c>CommitGraphRow</c> 渲染）</item>
///   <item><see cref="DiffRequested"/>：用户点"查看变更" / 双击列表行时面板发出事件，
///         参数是选中的 <see cref="GitCommit"/> 或 <c>null</c>（未选中时也冒泡，
///         MainWindow 据此给"请先选中"提示）</item>
///   <item><see cref="SelectionChangedHint"/>：选中变化时面板把描述写到状态栏</item>
/// </list>
///
/// <para><b>实现说明</b>：ListBox.SelectedItem 现在是 <see cref="CommitGraphRow"/>（不再是
/// <see cref="GitCommit"/>），因为 DataTemplate 的 data type 是 row。MainWindow 调
/// <see cref="SelectedCommit"/> 时由面板负责 unwrap 拿到 row.Commit。
/// </para>
/// </summary>
public partial class CommitDiffPanel : UserControl
{
    private ListBox? _commitsList;
    private Button? _viewDiffButton;

    /// <summary>用户请求查看某次提交的 diff 时触发（点按钮 / 双击列表行）。参数是选中的 GitCommit，
    /// 若用户未选中任何行，参数为 <c>null</c>（MainWindow 据此给"请先选中"提示）。</summary>
    public event EventHandler<GitCommit?>? DiffRequested;

    /// <summary>选中变化时触发（MainWindow 把这个写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    public CommitDiffPanel()
    {
        InitializeComponent();
        _commitsList = this.FindControl<ListBox>("CommitsList");
        _viewDiffButton = this.FindControl<Button>("ViewDiffButton");

        if (_commitsList != null)
        {
            _commitsList.SelectionChanged += (_, _) => OnSelectionChanged();
            _commitsList.DoubleTapped += (_, _) => RaiseDiffRequested();
        }
        if (_viewDiffButton != null)
        {
            _viewDiffButton.Click += (_, _) => RaiseDiffRequested();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入当前分支的提交列表（M2）。内部走 CommitGraphLayout 摊成行数据。</summary>
    public void LoadCommits(IReadOnlyList<GitCommit> commits)
    {
        if (_commitsList == null) return;
        IReadOnlyList<CommitGraphRow> rows = CommitGraphLayout.Compute(commits);
        _commitsList.ItemsSource = rows;
    }

    /// <summary>当前选中的 commit（MainWindow 弹 DiffWindow 时取 SHA）。从 row 解出 GitCommit。</summary>
    public GitCommit? SelectedCommit
    {
        get
        {
            if (_commitsList?.SelectedItem is CommitGraphRow row) return row.Commit;
            return null;
        }
    }

    private void OnSelectionChanged()
    {
        GitCommit? c = SelectedCommit;
        if (c != null)
        {
            SelectionChangedHint?.Invoke(this,
                $"已选中 {c.ShortSha}：{c.Subject}。点 \"查看变更（M3）\" 或双击列表行打开 diff。");
        }
    }

    private void RaiseDiffRequested()
    {
        // 总是触发 DiffRequested，让 MainWindow 决定怎么处理（无选择 / 无仓库 → 弹提示）。
        DiffRequested?.Invoke(this, SelectedCommit!);
    }
}
