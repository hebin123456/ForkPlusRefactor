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
///   <item>M1 仓库浏览：<see cref="_branchesList"/> + 事件处理</item>
///   <item>M2+M3 提交列表 / diff：<see cref="CommitDiffPanel"/> 面板（已抽出到 Panels/CommitDiffPanel.xaml）</item>
///   <item>M4 工作区改动：<see cref="WorkingTreePanel"/> 面板（已抽出到 Panels/WorkingTreePanel.xaml）</item>
/// </list>
/// </summary>
public partial class MainWindow : Window
{
    private GitRepository? _repo;

    // 缓存常用控件的引用，避免每次事件处理都做 FindControl 反射。
    private TextBlock? _servicesText;
    private TextBlock? _statusText;
    private TextBox? _repoPathBox;
    private ListBox? _branchesList;
    // M2+M3 面板（内部已含 ListBox + Button + 描述）
    private CommitDiffPanel? _commitDiffPanel;
    // M4 面板（内部已含 ListBox/Button/TextBlock）
    private WorkingTreePanel? _workingTreePanel;

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
    /// </summary>
    public void OpenRepository(string? path)
    {
        path = path?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            if (_statusText != null)
                _statusText.Text = "请先填写仓库路径。";
            return;
        }
        try
        {
            _repo?.Dispose();
            _repo = new GitRepository(path);
            string[] branches = _repo.GetBranches();
            if (_branchesList != null)
                _branchesList.ItemsSource = branches;
            // M2：开新仓库时清空提交列表
            _commitDiffPanel?.LoadCommits(Array.Empty<GitCommit>());
            int local = branches.Count(b => b.StartsWith("refs/heads/"));
            if (_statusText != null)
                _statusText.Text = $"已打开 {path}：共 {branches.Length} 个引用，其中本地分支 {local} 个。点击分支以加载提交（M2）。";
            // M4：同步刷一次工作区改动
            LoadWorkingTreeChanges();
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开仓库失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M2：分支被选中后，通过 biturbo 列该分支最新 50 条提交，喂给 <see cref="CommitDiffPanel"/>。
    /// </summary>
    private void OnBranchSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_repo == null)
        {
            return;
        }
        if (_branchesList?.SelectedItem is not string selected)
        {
            return;
        }
        try
        {
            GitCommit[] commits = _repo.GetCommits(selected, maxCount: 50);
            _commitDiffPanel?.LoadCommits(commits);
            if (_statusText != null)
            {
                _statusText.Text = commits.Length == 0
                    ? $"分支 {selected} 暂无提交。"
                    : $"已加载 {selected} 的最新 {commits.Length} 条提交（首条：{commits[0].DisplayLine}）。";
            }
        }
        catch (Exception ex)
        {
            _commitDiffPanel?.LoadCommits(Array.Empty<GitCommit>());
            if (_statusText != null)
                _statusText.Text = $"加载 {selected} 提交失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M3：处理 <see cref="CommitDiffPanel.DiffRequested"/> 事件 —— 弹 <see cref="DiffWindow"/>。
    /// 弹窗策略归 MainWindow（嵌入 vs 独立窗口 vs 多窗口），面板只负责"用户要看 diff"这一个意图。
    /// </summary>
    private void OnCommitDiffRequested(object? sender, GitCommit? c)
    {
        if (_repo == null)
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
            DiffResult diff = _repo.GetCommitDiff(c.Sha);
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
        var c = _commitDiffPanel?.SelectedCommit;
        if (_repo == null || c == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = _repo == null
                    ? "请先打开一个仓库（M1）。"
                    : "请先选中一个 commit。";
            }
            return null;
        }
        try
        {
            DiffResult diff = _repo.GetCommitDiff(c.Sha);
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
    /// 由 <see cref="OpenRepository"/> 在拿到新 <see cref="GitRepository"/> 后调用。
    /// </summary>
    public void LoadWorkingTreeChanges()
    {
        _workingTreePanel?.Load(_repo);
    }

    /// <summary>
    /// M4：处理 <see cref="WorkingTreePanel.DiffRequested"/> 事件 —— 弹 <see cref="DiffWindow"/>。
    /// 弹窗策略归 MainWindow（嵌入 vs 独立窗口 vs 多窗口），面板只负责"用户要看 diff"这一个意图。
    /// </summary>
    private void OnWorkingTreeDiffRequested(object? sender, WorkingTreeChange? c)
    {
        if (_repo == null)
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
            DiffResult diff = _repo.GetWorkingTreeDiff(c.Path);
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
        if (_repo == null || c == null)
        {
            if (_statusText != null)
            {
                _statusText.Text = _repo == null
                    ? "请先打开一个仓库（M1）。"
                    : "请先选中一个工作区改动（M4）。";
            }
            return null;
        }
        try
        {
            DiffResult diff = _repo.GetWorkingTreeDiff(c.Path);
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
}

internal static class WorkingTreeChangeExt
{
    /// <summary>判断是否是 untracked（不在 git 索引里）。</summary>
    public static bool Untracked(this WorkingTreeChange c) => c.Kind == WorkingTreeStatusKind.Untracked;
}
