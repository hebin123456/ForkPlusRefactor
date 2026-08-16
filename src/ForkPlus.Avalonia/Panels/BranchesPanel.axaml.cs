using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M8 分支树面板：把 <see cref="GitRepository.GetBranches"/> 的扁平 ref 列表
/// 转成 WPF 原版风格的"本地 / 远程"分组树，▶ 标记当前 HEAD 指向的本地分支。
///
/// <para>
/// 沿用 M4/M5/M6/M7 的"面板只关心自己的状态，事件意图交给 MainWindow"模式：
/// </para>
/// <list type="bullet">
///   <item><see cref="Load"/>：外部（MainWindow 打开仓库后）把 <see cref="GitRepository"/> 喂进来</item>
///   <item><see cref="BranchActivated"/>：用户点树里一个真分支（不是组节点） → 触发 M2 加载该分支的提交列表</item>
///   <item><see cref="SelectionChangedHint"/>：选行时把分支名 + 当前分支写到主窗口状态栏</item>
/// </list>
///
/// <para>
/// M8 不做 checkout（切分支） / new branch / merge —— 后续阶段加上。先把侧栏的"展示分组 + 当前标记"做对，
/// 与 WPF 原版 <c>v2-16-sidebar-full.png</c> 一致。
/// </para>
/// </summary>
public partial class BranchesPanel : UserControl
{
    private TreeView? _tree;
    private TextBlock? _summaryText;
    private Button? _refreshButton;

    /// <summary>当前绑定的仓库。null 表示"未打开仓库"状态。</summary>
    public GitRepository? Repository { get; private set; }

    /// <summary>用户激活（双击/Enter）一个真分支（参数是分支节点；组节点不会触发）。</summary>
    public event EventHandler<BranchTreeNode>? BranchActivated;

    /// <summary>状态文字提示（MainWindow 把这个写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    public BranchesPanel()
    {
        InitializeComponent();
        _tree = this.FindControl<TreeView>("BranchesTree");
        _summaryText = this.FindControl<TextBlock>("BranchesSummaryText");
        _refreshButton = this.FindControl<Button>("RefreshBranchesButton");

        if (_summaryText != null) _summaryText.Text = "未打开仓库。";
        if (_tree != null)
        {
            _tree.DoubleTapped += (_, _) => RaiseBranchActivated();
            // TreeView 选中项改变时更新状态栏
            _tree.SelectionChanged += (_, _) => OnSelectionChanged();
        }
        if (_refreshButton != null) _refreshButton.Click += (_, _) => Reload();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入仓库（开新仓库时调用）。</summary>
    public void Load(GitRepository? repo)
    {
        Repository = repo;
        if (repo == null)
        {
            if (_tree != null) _tree.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "未打开仓库。";
            return;
        }
        Reload();
    }

    /// <summary>用当前 Repository 重新拉一次分支树。</summary>
    public void Reload()
    {
        if (Repository == null)
        {
            if (_tree != null) _tree.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "未打开仓库。";
            return;
        }
        try
        {
            BranchTreeNode[] roots = Repository.GetBranchTree();
            if (_tree != null) _tree.ItemsSource = roots;
            if (_summaryText != null)
            {
                if (roots.Length == 0)
                {
                    _summaryText.Text = "无任何引用（空仓库或未初始化）。";
                }
                else
                {
                    int localCount = 0;
                    int remoteCount = 0;
                    string current = Repository.GetCurrentBranch();
                    foreach (var r in roots)
                    {
                        if (r.Name == "本地")
                        {
                            localCount = r.Children.Count;
                        }
                        else if (r.Name == "远程")
                        {
                            foreach (var remoteGroup in r.Children)
                            {
                                remoteCount += remoteGroup.Children.Count;
                            }
                        }
                    }
                    string curLabel = string.IsNullOrEmpty(current) ? "" : $"，当前 = {current}";
                    _summaryText.Text = $"共 {localCount} 个本地 + {remoteCount} 个远程分支{curLabel}。";
                }
            }
        }
        catch (Exception ex)
        {
            if (_tree != null) _tree.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "加载分支树失败：" + ex.Message;
        }
    }

    /// <summary>当前选中的分支节点（仅叶节点有意义；组节点不触发 M2）。</summary>
    public BranchTreeNode? SelectedBranch => _tree?.SelectedItem as BranchTreeNode;

    private void RaiseBranchActivated()
    {
        if (SelectedBranch is { } b && !b.IsGroup)
        {
            BranchActivated?.Invoke(this, b);
        }
    }

    private void OnSelectionChanged()
    {
        if (SelectedBranch is { } b)
        {
            string label = b.IsGroup
                ? $"{b.Name}（{b.Children.Count} 个子项）"
                : $"{(b.IsCurrent ? "★ " : "")}{b.FullRef}  →  {b.DisplayLine}";
            SelectionChangedHint?.Invoke(this, label);
        }
    }
}
