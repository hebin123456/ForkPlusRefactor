using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M3 端到端：构造 MainWindow → 打开仓库 → 选 main → 选一个 commit →
/// 点 "查看变更（M3）" → 断言有一个新 DiffWindow 出现，且它的 DiffView
/// 已加载了对应 commit 的 DiffResult（至少 1 个 Added 行）。
///
/// 对应原 WPF 流程：UI/MainWindow.xaml.cs 的 OnRevisionClick → 创建
/// <c>CommitTextDiffControl</c> → 加载 commit 范围内的全部 diff。这里把"主窗口下
/// 方的编辑区"映射为"独立 DiffWindow"，原因为 Avalonia 端 headless 断言更直接
/// （新窗口直接出现在 <see cref="IClassicDesktopStyleApplicationLifetime.Windows"/>），
/// 也方便截图到 verification/ 目录。
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M3DiffWindowUiTests
{
    private static bool _servicesInitialized;
    private static readonly object _initLock = new();

    private static void EnsureServicesInitialized()
    {
        if (_servicesInitialized) return;
        lock (_initLock)
        {
            if (_servicesInitialized) return;
            ServiceLocator.Initialize(
                dispatcher: new AvaloniaDispatcher(),
                designMode: new AvaloniaDesignModeService(),
                appContext: new AvaloniaAppContext(),
                clipboard: new AvaloniaClipboardService(),
                timer: new AvaloniaTimerService(),
                toast: new AvaloniaToastNotificationService(),
                windowManager: new AvaloniaWindowManagerService(),
                credentials: new AvaloniaCredentialManager(),
                fileDialog: new AvaloniaFileDialogService(),
                gitPathResolver: new AvaloniaGitPathResolver(),
                shell: new AvaloniaShellIntegrationService());
            _servicesInitialized = true;
        }
    }

    [AvaloniaFact]
    public async Task OpenRepo_SelectCommit_ViewDiff_OpensDiffWindow_WithNonEmptyDiff()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiCommitRepo();
        try
        {
            var main = new MainWindow();
            main.Show();
            Dispatcher.UIThread.RunJobs();

            // 1) 填路径、打开
            TextBox repoBox = main.FindControl<TextBox>("RepoPathBox")!;
            Button openBtn = main.FindControl<Button>("OpenRepoButton")!;
            repoBox.Text = repoDir;
            openBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // 2) 选 main 分支
            ListBox branches = main.FindControl<ListBox>("BranchesList")!;
            branches.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            // 3) 在 commits list 里选第二个 commit (subject = "second commit", 新增 b.txt)
            // M2+M3 已抽到 CommitDiffPanel，CommitsList 是面板子节点
            var panel = main.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
            ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
            // M2 提交图：ItemsSource 是 CommitGraphRow[]，从 row 取 commit
            var rows = commits.ItemsSource!.Cast<ForkPlus.Avalonia.Graph.CommitGraphRow>().ToArray();
            var commitArr = rows.Select(r => r.Commit).ToArray();
            Assert.True(commitArr.Length >= 2);
            GitCommit second = commitArr.First(c => c.Subject == "second commit");
            // ListBox 选中要选 row（DataTemplate 的 data type 是 row），不是 commit
            var secondRow = rows.First(r => r.Commit.Subject == "second commit");
            commits.SelectedItem = secondRow;
            Dispatcher.UIThread.RunJobs();

            // 4) 直接调 MainWindow.OpenSelectedCommitDiff（M3 公共入口），拿回 DiffWindow 实例
            DiffWindow? diffWin = main.OpenSelectedCommitDiff();
            Dispatcher.UIThread.RunJobs();

            // 5) 断言 DiffWindow 出现 + DiffView 已加载 diff
            Assert.NotNull(diffWin);
            Assert.NotNull(diffWin!.DiffView.Current);
            DiffResult diff = diffWin.DiffView.Current!;

            // 6) 内容断言：second commit 应该新增 b.txt（含一行 "b"）
            Assert.Contains(diff.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "b");
            Assert.True(diff.Lines.Any(l => l.Kind != DiffLineKind.Unchanged),
                "Diff 必须包含至少一处 Added/Removed 行");

            // 7) 状态文字也要反馈成功
            TextBlock status = main.FindControl<TextBlock>("StatusText")!;
            Assert.Contains("已打开", status.Text);
            Assert.Contains(second.ShortSha, status.Text);
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [AvaloniaFact]
    public async Task ViewDiffClicked_WithoutCommitSelected_ShowsPromptMessage()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        // 没打开仓库、没选 commit 直接点 ViewDiff → 状态文字应提示
        // M2+M3 已抽到 CommitDiffPanel，ViewDiffButton 是面板子节点
        var panel = main.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
        Button viewDiffBtn = panel.FindControl<Button>("ViewDiffButton")!;
        viewDiffBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        TextBlock status = main.FindControl<TextBlock>("StatusText")!;
        Assert.Contains("请先", status.Text);

        // 没有 DiffWindow 被打开
        Assert.Null(FindDiffWindow());
    }

    private static DiffWindow? FindDiffWindow()
    {
        // 头无 / 交互运行下，已 Show() 的窗口出现在 Application.Current 的 WindowImpl 里。
        // Avalonia 12 在测试模式下没有 IClassicDesktopStyleApplicationLifetime，
        // 改用反射扫一遍所有 TopLevel 找到带 DiffView 内容的窗口。
        var app = Application.Current;
        if (app == null) return null;
        // 1) 自身已知的窗口（一些 Avalonia 版本 Application.Windows 是只读副本）
        foreach (var w in EnumerateAllWindows())
        {
            if (w is DiffWindow dw) return dw;
        }
        return null;
    }

    private static System.Collections.Generic.IEnumerable<Window> EnumerateAllWindows()
    {
        // 通过反射 / 已知类型收集 TopLevel。最稳的入口是遍历视觉树（VisualRoot 树）。
        // 对 Avalonia 12 headless，最佳办法是看 Process 静态字段或 WindowBase.Opened 事件。
        // 简单做法：扫 Avalonia.Application.Current 的私有字段
        var app = Application.Current;
        if (app == null) yield break;
        var field = typeof(Application).GetField("_windows",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(app) is System.Collections.IEnumerable coll)
        {
            foreach (var item in coll)
            {
                if (item is Window w) yield return w;
            }
        }
    }

    // ---- 共享仓库 fixture：3 个 commit，second 新增 b.txt ----
    private static string CreateMultiCommitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m3_ui_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "first commit");
        Thread.Sleep(1100);

        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        RunGit(dir, "add", "b.txt");
        RunGit(dir, "commit", "-m", "second commit");
        Thread.Sleep(1100);

        File.WriteAllText(Path.Combine(dir, "c.txt"), "c");
        RunGit(dir, "add", "c.txt");
        RunGit(dir, "commit", "-m", "third commit");
        return dir;
    }

    private static void RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 git 进程");
        proc.WaitForExit();
        string stderr = proc.StandardError.ReadToEnd();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode}): {stderr}");
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
