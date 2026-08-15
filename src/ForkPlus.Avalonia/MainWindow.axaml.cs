using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
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
/// </summary>
public partial class MainWindow : Window
{
    private GitRepository? _repo;

    // 缓存常用控件的引用，避免每次事件处理都做 FindControl 反射。
    private TextBlock? _servicesText;
    private TextBlock? _statusText;
    private TextBox? _repoPathBox;
    private ListBox? _branchesList;
    private ListBox? _commitsList;

    public MainWindow()
    {
        InitializeComponent();

        _servicesText = this.FindControl<TextBlock>("ServicesText");
        _statusText = this.FindControl<TextBlock>("StatusText");
        _repoPathBox = this.FindControl<TextBox>("RepoPathBox");
        _branchesList = this.FindControl<ListBox>("BranchesList");
        _commitsList = this.FindControl<ListBox>("CommitsList");

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
            if (_commitsList != null)
                _commitsList.ItemsSource = null;
            int local = branches.Count(b => b.StartsWith("refs/heads/"));
            if (_statusText != null)
                _statusText.Text = $"已打开 {path}：共 {branches.Length} 个引用，其中本地分支 {local} 个。点击分支以加载提交（M2）。";
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"打开仓库失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M2：分支被选中后，通过 biturbo 列该分支最新 50 条提交。
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
            if (_commitsList != null)
                _commitsList.ItemsSource = commits;
            if (_statusText != null)
            {
                _statusText.Text = commits.Length == 0
                    ? $"分支 {selected} 暂无提交。"
                    : $"已加载 {selected} 的最新 {commits.Length} 条提交（首条：{commits[0].DisplayLine}）。";
            }
        }
        catch (Exception ex)
        {
            if (_commitsList != null)
                _commitsList.ItemsSource = null;
            if (_statusText != null)
                _statusText.Text = $"加载 {selected} 提交失败：{ex.Message}";
        }
    }
}
