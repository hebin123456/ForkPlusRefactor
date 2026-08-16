using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M5 文件树面板：显示 HEAD 的递归文件树，双击叶子文件时发出
/// <see cref="FileOpenRequested"/> 让 MainWindow 拿原始内容展示。
///
/// <para>
/// 与 M4 <see cref="WorkingTreePanel"/> 同款"事件 + Load"接缝：MainWindow 负责
/// 喂仓库 / 响应事件，面板自身不弹窗、不持有 GitRepository 之外的逻辑。
/// </para>
/// </summary>
public partial class FileTreePanel : UserControl
{
    private TreeView? _tree;
    private TextBlock? _summaryText;
    private Button? _refreshButton;

    /// <summary>当前绑定的仓库。null = 未打开。</summary>
    public GitRepository? Repository { get; private set; }

    /// <summary>当前面板显示哪条 ref 的树（默认 HEAD）。</summary>
    public string CurrentRef { get; private set; } = "HEAD";

    /// <summary>当前选中的文件树节点（用于 MainWindow 取 path）。</summary>
    public FileTreeNode? SelectedNode => _tree?.SelectedItem as FileTreeNode;

    /// <summary>用户双击了文件行，要求显示其内容。MainWindow 在事件处理里调
    /// <see cref="GitRepository.GetFileContent"/> 并展示。</summary>
    public event EventHandler<FileTreeNode>? FileOpenRequested;

    /// <summary>状态文字提示（与 M4 同款，MainWindow 写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    public FileTreePanel()
    {
        InitializeComponent();
        _tree = this.FindControl<TreeView>("FileTreeView");
        _summaryText = this.FindControl<TextBlock>("FileTreeSummaryText");
        _refreshButton = this.FindControl<Button>("RefreshButton");

        if (_summaryText != null) _summaryText.Text = "未打开仓库。";
        if (_tree != null)
        {
            _tree.DoubleTapped += (_, _) => RaiseFileOpen();
            _tree.SelectionChanged += (_, _) => OnSelectionChanged();
        }
        if (_refreshButton != null)
        {
            _refreshButton.Click += (_, _) => Reload();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入仓库（开新仓库 / 切分支时调用）。</summary>
    public void Load(GitRepository? repo, string? refName = null)
    {
        Repository = repo;
        CurrentRef = string.IsNullOrWhiteSpace(refName) ? "HEAD" : refName!;
        Reload();
    }

    /// <summary>重读当前 ref 的文件树（仓库或 ref 变了之后调）。</summary>
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
            FileTreeNode root = Repository.GetFileTree(CurrentRef);
            // TreeView 的根是 root（虚拟根），其 Children 是顶层条目
            if (_tree != null)
            {
                _tree.ItemsSource = new[] { root };
                // 默认全部展开（按 WPF 原版行为：ls-tree 一次性拿全部，UI 默认全展开）
                ExpandAll(root);
            }
            int flat = root.FlatCount();
            int dirs = CountByKind(root, FileTreeNode.FileTreeNodeKind.Directory);
            int files = CountByKind(root, FileTreeNode.FileTreeNodeKind.File);
            int subs = CountByKind(root, FileTreeNode.FileTreeNodeKind.Submodule);
            if (_summaryText != null)
            {
                _summaryText.Text = $"ref={CurrentRef}：{flat} 节点（目录 {dirs} / 文件 {files} / 子模块 {subs}）。";
            }
        }
        catch (Exception ex)
        {
            if (_tree != null) _tree.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = $"加载文件树失败：{ex.Message}";
        }
    }

    private void ExpandAll(FileTreeNode root)
    {
        if (_tree == null) return;
        // 根（虚拟根）展开后能看到顶层；递归把可展开的容器都展开
        // Avalonia TreeView 在 ItemsSource 重新设置后容器不会自动生成，
        // 因此通过 TreeViewContainer 配合 Template 手动 Expand。
        // 这里用最稳的办法：找到根节点对应的 TreeViewItem，调用 ExpandSubTree。
        if (_tree.ContainerFromItem(root) is TreeViewItem tvi)
        {
            ExpandRecursive(tvi);
        }
    }

    private static void ExpandRecursive(TreeViewItem tvi)
    {
        tvi.IsExpanded = true;
        foreach (object? child in tvi.Items)
        {
            if (tvi.ContainerFromItem(child) is TreeViewItem childTvi)
            {
                ExpandRecursive(childTvi);
            }
        }
    }

    private static int CountByKind(FileTreeNode node, FileTreeNode.FileTreeNodeKind kind)
    {
        int n = node.ItemType == kind ? 1 : 0;
        foreach (FileTreeNode c in node.Children)
        {
            n += CountByKind(c, kind);
        }
        return n;
    }

    private void OnSelectionChanged()
    {
        FileTreeNode? sel = _tree?.SelectedItem as FileTreeNode;
        if (sel != null)
        {
            SelectionChangedHint?.Invoke(this, $"{sel.FullPath} ({sel.ItemType})");
        }
    }

    private void RaiseFileOpen()
    {
        FileTreeNode? sel = _tree?.SelectedItem as FileTreeNode;
        if (sel == null) return;
        if (sel.ItemType == FileTreeNode.FileTreeNodeKind.Directory) return; // 只对文件/子模块触发
        FileOpenRequested?.Invoke(this, sel);
    }
}
