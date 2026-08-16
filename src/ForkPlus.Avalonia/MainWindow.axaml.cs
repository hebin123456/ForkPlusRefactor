using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Panels;
using ForkPlus.Services;

namespace ForkPlus.Avalonia;

/// <summary>
/// ForkPlus 主窗口（跨平台 Avalonia 版本）。
///
/// <para>
/// Avalonia 12 的 XamlX 编译 IL 不会为 <c>x:Name</c> 自动生成字段访问器，
/// 因此代码隐藏统一通过 <see cref="Window.FindControl{T}(string)"/> 拿控件。
/// 这样在交互运行、CI headless 测试等不同上下文中都能稳定取到控件。
/// </para>
///
/// <para>
/// 主窗口本身只承担"装配 + 弹窗"职责：
/// </para>
/// <list type="bullet">
///   <item>M1+M2 仓库/分支/提交加载：<see cref="RepoOpHandler"/>（已抽到 RepoOpHandler.cs）</item>
///   <item>M2+M3 提交列表 / diff：<see cref="CommitDiffPanel"/> 面板（已抽出到 Panels/CommitDiffPanel.xaml）</item>
///   <item>M4 工作区改动：<see cref="WorkingTreePanel"/> 面板（已抽出到 Panels/WorkingTreePanel.xaml）</item>
///   <item>M5 文件树 + 文件预览：<see cref="FileTreePanel"/> 面板（已抽出到 Panels/FileTreePanel.xaml）</item>
///   <item>M6 贮藏 (stash) 列表 + apply/pop/drop：<see cref="StashPanel"/> 面板（已抽出到 Panels/StashPanel.xaml）</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    // M1+M2：仓库/分支/提交加载操作处理器
    private RepoOpHandler? _repoOps;

    // 缓存常用控件的引用，避免每次事件处理都做 FindControl 反射。
    private TextBlock? _servicesText;
    private TextBlock? _statusText;
    private TextBox? _repoPathBox;
    private ListBox? _branchesList;
    // M2+M3 面板（内部已含 ListBox + Button + 描述）
    private CommitDiffPanel? _commitDiffPanel;
    // M4 面板（内部已含 ListBox/Button/TextBlock）
    private WorkingTreePanel? _workingTreePanel;
    // M5 面板（内部已含 TreeView + Button + TextBlock）
    private FileTreePanel? _fileTreePanel;
    // M5 文件预览
    private TextBlock? _filePreviewTitle;
    private TextBlock? _filePreviewText;
    // M6 面板（内部已含 ListBox + Apply/Pop/Drop/ShowDiff 按钮）
    private StashPanel? _stashPanel;

    public MainWindow()
    {
        InitializeComponent();

        _servicesText = this.FindControl<TextBlock>("ServicesText");
        _statusText = this.FindControl<TextBlock>("StatusText");
        _repoPathBox = this.FindControl<TextBox>("RepoPathBox");
        _branchesList = this.FindControl<ListBox>("BranchesList");
        // M2+M3 面板
        _commitDiffPanel = this.FindControl<CommitDiffPanel>("CommitDiffPanel");
        if (_commitDiffPanel != null)
        {
            _commitDiffPanel.DiffRequested += OnCommitDiffRequested;
            _commitDiffPanel.SelectionChangedHint += (_, hint) =>
            {
                if (_statusText != null) _statusText.Text = hint;
            };
        }
        // M4 面板
        _workingTreePanel = this.FindControl<WorkingTreePanel>("WorkingTreePanel");
        if (_workingTreePanel != null)
        {
            _workingTreePanel.DiffRequested += OnWorkingTreeDiffRequested;
            _workingTreePanel.SelectionChangedHint += (_, hint) =>
            {
                if (_statusText != null) _statusText.Text = hint;
            };
        }
        // M5 面板
        _fileTreePanel = this.FindControl<FileTreePanel>("FileTreePanel");
        _filePreviewTitle = this.FindControl<TextBlock>("FilePreviewTitle");
        _filePreviewText = this.FindControl<TextBlock>("FilePreviewText");
        if (_fileTreePanel != null)
        {
            _fileTreePanel.FileOpenRequested += OnFileOpenRequested;
            _fileTreePanel.SelectionChangedHint += (_, hint) =>
            {
                if (_statusText != null) _statusText.Text = hint;
            };
        }
        // M6 面板
        _stashPanel = this.FindControl<StashPanel>("StashPanel");
        if (_stashPanel != null)
        {
            _stashPanel.ApplyRequested += OnStashApplyRequested;
            _stashPanel.PopRequested += OnStashPopRequested;
            _stashPanel.DropRequested += OnStashDropRequested;
            _stashPanel.ShowDiffRequested += OnStashShowDiffRequested;
            _stashPanel.SelectionChangedHint += (_, hint) =>
            {
                if (_statusText != null) _statusText.Text = hint;
            };
        }
        // M1+M2 操作处理器：装配好之后才能订阅分支变化
        _repoOps = new RepoOpHandler(_branchesList, _commitDiffPanel, _workingTreePanel, _statusText, _fileTreePanel, _stashPanel);

        var ac = ServiceLocator.AppContext;
        if (_servicesText != null)
        {
            _servicesText.Text =
                "已接入的跨平台服务（ServiceLocator）：\n" +
                $"  IAppContext.AppDataDirectory     = {ac.AppDataDirectory}\n" +
                $"  IAppContext.ForkDataDirectoryPath = {ac.ForkDataDirectoryPath}\n" +
                $"  IAppContext.RepositoriesFilePath  = {ac.RepositoriesFilePath}\n" +
                $"  IAppContext.OSVersion            = {ac.OSVersion}\n" +
                "  IDispatcher / IClipboardService / ITimerService /\n" +
                "  IToastNotificationService / IWindowManagerService / IDesignModeService 均已注册";
        }

        if (this.FindControl<Button>("CopyButton") is { } copyBtn)
            copyBtn.Click += OnCopyClicked;
        if (this.FindControl<Button>("ToastButton") is { } toastBtn)
            toastBtn.Click += OnToastClicked;
        if (this.FindControl<Button>("DiffButton") is { } diffBtn)
            diffBtn.Click += OnDiffClicked;
        if (this.FindControl<Button>("PlatformButton") is { } platformBtn)
            platformBtn.Click += OnPlatformClicked;
        if (this.FindControl<Button>("AiMarkdownButton") is { } aiBtn)
            aiBtn.Click += OnAiMarkdownClicked;
        if (this.FindControl<Button>("OpenRepoButton") is { } openBtn)
            openBtn.Click += OnOpenRepoClicked;

        if (_branchesList != null)
            _branchesList.SelectionChanged += OnBranchSelectionChanged;

        // M2+M3：CommitDiffPanel 内部已 self-wire 自己的 ListBox/Button 事件，
        // MainWindow 只通过 DiffRequested 事件接收"用户要看 diff"的意图。
        // M4：WorkingTreePanel 内部已 self-wire 自己的 ListBox/Button 事件，
        // MainWindow 只通过 DiffRequested 事件接收"用户要看 diff"的意图。
    }

    public void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCopyClicked(object? sender, RoutedEventArgs e)
    {
        ServiceLocator.Clipboard.SetText("Hello from Avalonia ForkPlus!");
        if (_statusText != null)
            _statusText.Text = "已通过 IClipboardService 写入剪贴板。";
    }

    private void OnToastClicked(object? sender, RoutedEventArgs e)
    {
        ServiceLocator.Toast?.Show(
            "<toast><visual><binding template=\"ToastGeneric\">" +
            "<text>ForkPlus</text>" +
            "<text>Avalonia 通知服务已接入（WinRT Toast XML 被解析显示）</text>" +
            "</binding></visual></toast>");
        if (_statusText != null)
            _statusText.Text = "已通过 IToastNotificationService 显示 in-app 浮层。";
    }

    private void OnDiffClicked(object? sender, RoutedEventArgs e)
    {
        const string oldText =
@"public int Add(int a, int b)
{
    return a + b;
}

public void Log(string message)
{
    Console.WriteLine(message);
}";
        const string newText =
@"public int Add(int a, int b)
{
    // 支持更大的数值范围
    return checked(a + b);
}

public void Log(string message)
{
    Logger.Info(message);
}

public void Reset()
{
    Logger.Clear();
}";
        var result = LineDiff.Compute(oldText, newText, "a/Calculator.cs", "b/Calculator.cs");
        new DiffWindow(result).Show();
        if (_statusText != null)
            _statusText.Text = $"已打开差异对比：{result.Lines.Count} 行（绿=新增，红=删除）。";
    }

    private void OnPlatformClicked(object? sender, RoutedEventArgs e)
    {
        new PlatformServicesDemoWindow().Show();
        if (_statusText != null)
            _statusText.Text = "已打开平台服务 Demo (P3)：主题检测 / 凭据管理 / 文件对话框。";
    }

    private void OnAiMarkdownClicked(object? sender, RoutedEventArgs e)
    {
        new AiMarkdownDemoWindow().Show();
        if (_statusText != null)
            _statusText.Text = "已打开 AI Markdown 渲染 Demo (P4)。";
    }

    private void OnOpenRepoClicked(object? sender, RoutedEventArgs e) => OpenRepository(_repoPathBox?.Text);

    /// <summary>
    /// M1：打开仓库并列出引用。失败时通过 <see cref="_statusText"/> 反馈。
    /// 已委托给 <see cref="RepoOpHandler.Open"/>，保留此公开方法作为 headless 测试入口和兼容 API。
    /// </summary>
    public void OpenRepository(string? path)
    {
        _repoOps?.Open(path);
    }

    /// <summary>
    /// M2：分支被选中后，通过 biturbo 列该分支最新 50 条提交，喂给 <see cref="CommitDiffPanel"/>。
    /// 已委托给 <see cref="RepoOpHandler.SelectBranch"/>。
    /// </summary>
    private void OnBranchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_branchesList?.SelectedItem is string selected)
        {
            _repoOps?.SelectBranch(selected);
        }
    }

    /// <summary>
    /// M3：处理 <see cref="CommitDiffPanel.DiffRequested"/> 事件 —— 弹 <see cref="DiffWindow"/>。
    /// 弹窗策略归 MainWindow（嵌入 vs 独立窗口 vs 多窗口），面板只负责"用户要看 diff"这一个意图。
    /// </summary>
    private void OnCommitDiffRequested(object? sender, GitCommit? c)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (c == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一个 commit。";
            return;
        }
        try
        {
            DiffResult diff = _repoOps.CurrentRepo.GetCommitDiff(c.Sha);
            var w = new DiffWindow(diff);
            w.Show();
            if (_statusText != null)
            {
                _statusText.Text = $"已打开 {c.ShortSha} 的 diff：{diff.Lines.Count} 行"
                    + (diff.Lines.Count > 0 ? "（按 Added/Removed/Unchanged 着色）" : "（该次提交无 tree diff）");
            }
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开 {c.ShortSha} diff 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M3：旧 API 兼容（被 headless 测试 <c>M3DiffWindowUiTests.OpenRepo_SelectCommit_ViewDiff_...</c>
    /// 直接调来弹窗）。走"面板选中的项 → MainWindow 弹窗"路径，行为与点按钮一致。
    /// </summary>
    public DiffWindow? OpenSelectedCommitDiff()
    {
        var repo = _repoOps?.CurrentRepo;
        var c = _commitDiffPanel?.SelectedCommit;
        if (repo == null || c == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = repo == null
                    ? "请先打开一个仓库（M1）。"
                    : "请先选中一个 commit。";
            }
            return null;
        }
        try
        {
            DiffResult diff = repo.GetCommitDiff(c.Sha);
            var w = new DiffWindow(diff);
            w.Show();
            if (_statusText != null)
            {
                _statusText.Text = $"已打开 {c.ShortSha} 的 diff：{diff.Lines.Count} 行"
                    + (diff.Lines.Count > 0 ? "（按 Added/Removed/Unchanged 着色）" : "（该次提交无 tree diff）");
            }
            return w;
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开 {c.ShortSha} diff 失败：{ex.Message}";
            return null;
        }
    }

    // ============== M4: 本地改动 (delegated to WorkingTreePanel) ==============

    /// <summary>
    /// M4：把当前仓库传给 <see cref="WorkingTreePanel"/> 让它自己刷。
    /// 委托给 <see cref="RepoOpHandler.Open"/> 之后这一步已自动发生，保留为公开兼容 API。
    /// </summary>
    public void LoadWorkingTreeChanges()
    {
        if (_repoOps?.CurrentRepo != null)
        {
            _workingTreePanel?.Load(_repoOps.CurrentRepo);
        }
    }

    /// <summary>
    /// M4：处理 <see cref="WorkingTreePanel.DiffRequested"/> 事件 —— 弹 <see cref="DiffWindow"/>。
    /// 弹窗策略归 MainWindow（嵌入 vs 独立窗口 vs 多窗口），面板只负责"用户要看 diff"这一个意图。
    /// </summary>
    private void OnWorkingTreeDiffRequested(object? sender, WorkingTreeChange? c)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (c == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一个工作区改动（M4）。";
            return;
        }
        try
        {
            DiffResult diff = _repoOps.CurrentRepo.GetWorkingTreeDiff(c.Path);
            var w = new DiffWindow(diff);
            w.Show();
            if (_statusText != null)
            {
                _statusText.Text = $"已打开工作区改动 {c.Path} 的 diff：{diff.Lines.Count} 行（{c.Kind}）。";
            }
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开 {c.Path} diff 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M4：旧 API 兼容（被 headless 测试 <c>M4WorkingTreeVisualTests.M4_OpenRepo_...</c>
    /// 直接调来弹窗）。走"面板选中的项 → MainWindow 弹窗"路径，行为与点按钮一致。
    /// </summary>
    public DiffWindow? OpenSelectedWorkingTreeDiff()
    {
        var c = _workingTreePanel?.SelectedChange;
        var repo = _repoOps?.CurrentRepo;
        if (repo == null || c == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = repo == null
                    ? "请先打开一个仓库（M1）。"
                    : "请先选中一个工作区改动（M4）。";
            }
            return null;
        }
        try
        {
            DiffResult diff = repo.GetWorkingTreeDiff(c.Path);
            var w = new DiffWindow(diff);
            w.Show();
            if (_statusText != null)
            {
                _statusText.Text = $"已打开工作区改动 {c.Path} 的 diff：{diff.Lines.Count} 行（{c.Kind}）。";
            }
            return w;
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开 {c.Path} diff 失败：{ex.Message}";
            return null;
        }
    }

    // ============== M5: 文件树 + 文件预览 ==============

    /// <summary>
    /// M5：处理 <see cref="FileTreePanel.FileOpenRequested"/> 事件 ——
    /// 调 <see cref="GitRepository.GetFileContent"/> 拿原始内容并显示在主窗口的预览框。
    /// </summary>
    private void OnFileOpenRequested(object? sender, FileTreeNode node)
    {
        var repo = _repoOps?.CurrentRepo;
        if (repo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        try
        {
            string content = repo.GetFileContent(_fileTreePanel?.CurrentRef ?? "HEAD", node.FullPath);
            if (_filePreviewTitle != null) _filePreviewTitle.Text = $"文件内容预览（M5 · {node.FullPath}）";
            if (_filePreviewText != null) _filePreviewText.Text = content;
            if (_statusText != null) _statusText.Text = $"已显示 {node.FullPath}（{content.Length} 字符）";
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"读取 {node.FullPath} 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M5：旧 API 兼容（被 headless 测试直接调来弹窗）。从面板当前选中项拿 FileTreeNode。
    /// </summary>
    public string? OpenSelectedFileContent()
    {
        var repo = _repoOps?.CurrentRepo;
        var node = _fileTreePanel?.SelectedNode;
        if (repo == null || node == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = repo == null
                    ? "请先打开一个仓库（M1）。"
                    : "请先在文件树中选中一个文件（M5）。";
            }
            return null;
        }
        try
        {
            string content = repo.GetFileContent(_fileTreePanel.CurrentRef, node.FullPath);
            if (_filePreviewTitle != null) _filePreviewTitle.Text = $"文件内容预览（M5 · {node.FullPath}）";
            if (_filePreviewText != null) _filePreviewText.Text = content;
            if (_statusText != null) _statusText.Text = $"已显示 {node.FullPath}（{content.Length} 字符）";
            return content;
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"读取 {node.FullPath} 失败：{ex.Message}";
            return null;
        }
    }

    // ============== M6: 贮藏 (delegated to StashPanel) ==============

    /// <summary>
    /// M6：处理 <see cref="StashPanel.ApplyRequested"/> —— 调
    /// <see cref="GitRepository.StashApply"/>，然后 reload 本面板（stash 列表不变，但
    /// working tree 变了所以顺便 reload M4 工作区）。
    /// </summary>
    private void OnStashApplyRequested(object? sender, GitStash? s)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (s == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一条 stash（M6）。";
            return;
        }
        try
        {
            _repoOps.CurrentRepo.StashApply(s.ReflogSelector);
            // 重新加载面板 + 工作区（M4 会变）
            _stashPanel?.Reload();
            _workingTreePanel?.Load(_repoOps.CurrentRepo);
            if (_statusText != null) _statusText.Text = $"已 apply {s.ReflogSelector}（{s.DisplayLine}）。";
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"apply {s.ReflogSelector} 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M6：处理 <see cref="StashPanel.PopRequested"/> —— 调 <see cref="GitRepository.StashPop"/>，
    /// 然后 reload（stash 列表本身会少一条 → 重新拉）。
    /// </summary>
    private void OnStashPopRequested(object? sender, GitStash? s)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (s == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一条 stash（M6）。";
            return;
        }
        try
        {
            _repoOps.CurrentRepo.StashPop(s.ReflogSelector);
            // stash 列表会少一条 + working tree 变 → 双重 reload
            _stashPanel?.Reload();
            _workingTreePanel?.Load(_repoOps.CurrentRepo);
            if (_statusText != null) _statusText.Text = $"已 pop {s.ReflogSelector}（{s.DisplayLine}）。";
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"pop {s.ReflogSelector} 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M6：处理 <see cref="StashPanel.DropRequested"/> —— 调 <see cref="GitRepository.StashDrop"/>，
    /// 然后 reload（stash 列表会少一条）。
    /// </summary>
    private void OnStashDropRequested(object? sender, GitStash? s)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (s == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一条 stash（M6）。";
            return;
        }
        try
        {
            _repoOps.CurrentRepo.StashDrop(s.ReflogSelector);
            // 重新拉 stash 列表（少一条）+ working tree 不变
            _stashPanel?.Reload();
            if (_statusText != null) _statusText.Text = $"已 drop {s.ReflogSelector}（{s.DisplayLine}）。";
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"drop {s.ReflogSelector} 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M6：处理 <see cref="StashPanel.ShowDiffRequested"/> —— 调
    /// <see cref="GitRepository.GetStashDiff"/> 拿 unified diff 并弹 <see cref="DiffWindow"/>。
    /// </summary>
    private void OnStashShowDiffRequested(object? sender, GitStash? s)
    {
        if (_repoOps?.CurrentRepo == null)
        {
            if (_statusText != null) _statusText.Text = "请先打开一个仓库（M1）。";
            return;
        }
        if (s == null)
        {
            if (_statusText != null) _statusText.Text = "请先选中一条 stash（M6）。";
            return;
        }
        try
        {
            DiffResult diff = _repoOps.CurrentRepo.GetStashDiff(s.ReflogSelector);
            var w = new DiffWindow(diff);
            w.Show();
            if (_statusText != null)
            {
                _statusText.Text = $"已打开 {s.ReflogSelector} 的 diff：{diff.Lines.Count} 行（{s.DisplayLine}）。";
            }
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"打开 {s.ReflogSelector} diff 失败：{ex.Message}";
        }
    }
}

internal static class WorkingTreeChangeExt
{
    /// <summary>判断是否是 untracked（不在 git 索引里）。</summary>
    public static bool Untracked(this WorkingTreeChange c) => c.Kind == WorkingTreeStatusKind.Untracked;
}
