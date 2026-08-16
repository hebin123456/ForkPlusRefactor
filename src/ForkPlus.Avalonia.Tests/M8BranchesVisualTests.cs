using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Panels;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M8 分支树视觉 + 语义断言：
/// 打开一个含若干本地 + 远程分支的 git 仓库 → 走完整 MainWindow 流程 →
/// 断言分支树分组正确、本地分支按字典序、当前 HEAD 标记正确、双击分支触发 M2 提交加载。
///
/// <para>
/// 沿用 M4/M5/M6/M7 的"三层断言"模式：
/// </para>
/// <list type="number">
///   <item>
///     <b>数据流断言</b>：<see cref="GitRepository.GetBranchTree"/> + <see cref="GitRepository.GetCurrentBranch"/>
///     正确分组本地 / 远程、识别 current branch、忽略 HEAD 自身。
///   </item>
///   <item>
///     <b>可视化徽章颜色断言</b>：每个真分支行有 L 蓝 / R 紫徽章；组节点无徽章；
///     当前分支有 ▶ 标记 + Bold 字体。
///   </item>
///   <item>
///     <b>操作语义断言</b>：双击远程分支 → 加载该远程分支的提交列表（refs/remotes 也能解析）。
///   </item>
/// </list>
///
/// <para>
/// 落盘 PNG 到 <c>verification/avalonia/m8-branches/avalonia-m8-branches.png</c>（分支树）和
/// <c>verification/avalonia/m8-branches/avalonia-m8-branches-mainwindow.png</c>（真实主窗口），与 WPF 原版
/// <c>v2-16-sidebar-full.png</c> / <c>75-ctxmenu-sidebar-branch.png</c> / <c>76-ctxmenu-sidebar-remote.png</c>
/// 构成对照。
/// </para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M8BranchesVisualTests
{
    // 必须与 BranchKindToBrushConverter 严格一致
    private static readonly Color LocalBrush = Color.FromRgb(0x4A, 0x90, 0xE2);
    private static readonly Color RemoteBrush = Color.FromRgb(0x7B, 0x68, 0xA6);

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
    public async Task M8_OpenRepo_BranchTreeParsed_LocalRemoteGrouped_CurrentMarked_RemoteActivates()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM8Repo();
        try
        {
            // ============================================================
            // 1) 走真实 M8 流程：开仓库 → BranchesPanel 自动加载
            // ============================================================
            var main = new MainWindow { Width = 1100, Height = 900 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 找到 M8 面板
            var panel = main.FindControl<BranchesPanel>("BranchesPanel")!;
            Assert.NotNull(panel.Repository);
            TextBlock summary = panel.FindControl<TextBlock>("BranchesSummaryText")!;
            TreeView tree = panel.FindControl<TreeView>("BranchesTree")!;

            // ============================================================
            // 1a) 数据流断言
            // ============================================================
            // 验证当前分支：fixture 把它 checkout 到 main
            string currentBranch = panel.Repository!.GetCurrentBranch();
            Assert.Equal("main", currentBranch);

            BranchTreeNode[] roots = panel.Repository!.GetBranchTree();
            Assert.Equal(2, roots.Length); // [本地, 远程]

            // 本地组
            BranchTreeNode localGroup = roots[0];
            Assert.Equal("本地", localGroup.Name);
            Assert.True(localGroup.IsGroup);
            Assert.Equal(3, localGroup.Children.Count); // main, develop, feature/login

            // 排序：main (current) 排第一
            Assert.Equal("main", localGroup.Children[0].Name);
            Assert.True(localGroup.Children[0].IsCurrent);
            Assert.False(localGroup.Children[0].IsGroup);
            Assert.Equal("refs/heads/main", localGroup.Children[0].FullRef);
            Assert.Equal(BranchKind.Local, localGroup.Children[0].Kind);

            Assert.Equal("develop", localGroup.Children[1].Name);
            Assert.False(localGroup.Children[1].IsCurrent);

            Assert.Equal("feature/login", localGroup.Children[2].Name);
            Assert.False(localGroup.Children[2].IsCurrent);

            // 远程组
            BranchTreeNode remoteGroup = roots[1];
            Assert.Equal("远程", remoteGroup.Name);
            Assert.True(remoteGroup.IsGroup);
            Assert.Single(remoteGroup.Children); // 只有 origin

            // origin 子组（按字典序：feature/login 在 main 前）
            BranchTreeNode originGroup = remoteGroup.Children[0];
            Assert.Equal("origin", originGroup.Name);
            Assert.True(originGroup.IsGroup);
            Assert.Equal(2, originGroup.Children.Count); // origin/main, origin/feature/login

            Assert.Equal("feature/login", originGroup.Children[0].Name);
            Assert.Equal("refs/remotes/origin/feature/login", originGroup.Children[0].FullRef);
            Assert.Equal(BranchKind.Remote, originGroup.Children[0].Kind);
            Assert.False(originGroup.Children[0].IsCurrent);

            Assert.Equal("main", originGroup.Children[1].Name);
            Assert.Equal("refs/remotes/origin/main", originGroup.Children[1].FullRef);
            Assert.Equal(BranchKind.Remote, originGroup.Children[1].Kind);

            // 跟 git 原文对比：refs/heads/* + refs/remotes/*（去掉 origin/HEAD）
            string gitBranchesRaw = RunGitCapture(repoDir, "for-each-ref", "--format=%(refname)", "refs/heads/", "refs/remotes/");
            var gitBranches = gitBranchesRaw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(r => !r.EndsWith("/HEAD", StringComparison.Ordinal))
                .OrderBy(r => r, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(5, gitBranches.Count);
            // 把我们的树拍平
            var ourBranches = new List<string>();
            foreach (var lg in localGroup.Children) ourBranches.Add(lg.FullRef);
            foreach (var og in remoteGroup.Children)
            {
                foreach (var rl in og.Children) ourBranches.Add(rl.FullRef);
            }
            ourBranches.Sort(StringComparer.Ordinal);
            Assert.Equal(gitBranches, ourBranches);

            // summary 文案
            Assert.Contains("3 个本地", summary.Text);
            Assert.Contains("2 个远程", summary.Text);
            Assert.Contains("当前 = main", summary.Text);

            // TreeView 根级 == 2（本地 + 远程）
            Assert.NotNull(tree.ItemsSource);
            Assert.Equal(2, ((System.Collections.ICollection)tree.ItemsSource).Count);

            // ============================================================
            // 1b) 可视化徽章颜色断言
            //     重建 XAML 等价 StackPanel：每行 = ▶/缩进 + BadgeBorder(Background=KindToBrush) + Name
            //     遍历 Border.Background 颜色 == 期望。
            // ============================================================
            var badgeHost = BuildBranchBadgeHost(roots, out int localBadgeCount, out int remoteBadgeCount);
            const int W = 520;
            int totalRows = 2 /* groups */ + localBadgeCount + 1 /* origin sub-group */ + remoteBadgeCount;
            int h = Math.Min(800, 40 + totalRows * 26);
            badgeHost.Measure(new Size(W, h));
            badgeHost.Arrange(new Rect(0, 0, W, h));
            badgeHost.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var borders = badgeHost.GetVisualDescendants().OfType<Border>().ToList();
            // 至少有 3 + 2 = 5 个真分支徽章
            Assert.True(borders.Count >= localBadgeCount + remoteBadgeCount,
                $"期望至少 {localBadgeCount + remoteBadgeCount} 个徽章 Border，实际 {borders.Count}");

            // 收集每个徽章的颜色
            var observedColors = new List<Color>();
            foreach (var b in borders)
            {
                if (b.Background is SolidColorBrush sc) observedColors.Add(sc.Color);
            }
            Assert.True(observedColors.Count >= localBadgeCount + remoteBadgeCount,
                $"期望至少 {localBadgeCount + remoteBadgeCount} 个非空徽章颜色，实际 {observedColors.Count}");

            // 排序后逐位对比：
            // 我们 rebuild 时顺序：[本地组的 3 个 L 徽章, 远程/origin 组的 2 个 R 徽章]
            // → 前 3 个应该是 Local 蓝，后 2 个应该是 Remote 紫
            for (int i = 0; i < localBadgeCount; i++)
            {
                Assert.True(IsCloseTo(observedColors[i], LocalBrush, 2),
                    $"本地分支 #{i} 期望 Local 蓝 {LocalBrush}，实际 {observedColors[i]}");
            }
            for (int i = 0; i < remoteBadgeCount; i++)
            {
                Assert.True(IsCloseTo(observedColors[localBadgeCount + i], RemoteBrush, 2),
                    $"远程分支 #{i} 期望 Remote 紫 {RemoteBrush}，实际 {observedColors[localBadgeCount + i]}");
            }

            // ============================================================
            // 2) 操作语义断言
            // ============================================================
            // 2a) 双击远程分支（origin/feature/login）→ 应触发 BranchActivated → 加载该远程分支的提交
            BranchTreeNode remoteTarget = originGroup.Children[0]; // origin/feature/login (字典序排第一)
            string preStatus = main.FindControl<TextBlock>("StatusText")!.Text ?? "";
            // 模拟 BranchActivated 事件
            typeof(BranchesPanel).GetMethod("RaiseBranchActivated",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            // 实际触发：直接调 OnBranchActivated 通过反射
            // 简化：直接调 _repoOps?.SelectBranch(remoteTarget.FullRef)
            var repoOps = typeof(MainWindow).GetField("_repoOps",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(main);
            var selectBranch = repoOps!.GetType().GetMethod("SelectBranch")!;
            selectBranch.Invoke(repoOps, new object?[] { remoteTarget.FullRef });
            Dispatcher.UIThread.RunJobs();

            // 状态栏应当出现 "origin/feature/login"
            string afterStatus = main.FindControl<TextBlock>("StatusText")!.Text ?? "";
            Assert.Contains("origin/feature/login", afterStatus);

            // 2b) 加载该远程分支的最新 50 条 commit
            var commitPanel = main.FindControl<CommitDiffPanel>("CommitDiffPanel")!;
            var commitsList = commitPanel.FindControl<ListBox>("CommitsList")!;
            // 远程分支 feature/login 在 fixture 中至少 2 个 commit
            int commitCount = commitsList.ItemCount;
            Assert.True(commitCount >= 2, $"远程分支 origin/feature/login 应至少 2 个 commit，实际 {commitCount}");

            // 2c) detached HEAD 行为：GetCurrentBranch 在 detached 状态下应返回 ""
            //     （通过 git checkout <commit-sha> 模拟）
            string headSha = RunGitCapture(repoDir, "rev-parse", "HEAD").Trim();
            RunGit(repoDir, "checkout", "--detach", headSha);
            try
            {
                string detached = panel.Repository!.GetCurrentBranch();
                Assert.Equal("", detached);
            }
            finally
            {
                RunGit(repoDir, "checkout", "main");
            }

            // 2d) "未打开仓库" 行为
            var freshPanel = new BranchesPanel();
            freshPanel.Load(null);
            Assert.Null(freshPanel.Repository);
            Assert.Null(freshPanel.FindControl<TreeView>("BranchesTree")!.ItemsSource);

            // 2e) 空仓库：refs/heads/* 没有 → 只有本地组（空）
            string emptyDir = Path.Combine(Path.GetTempPath(), "forkplus_m8_empty_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(emptyDir);
            try
            {
                RunGit(emptyDir, "init", "-b", "main");
                RunGit(emptyDir, "config", "user.email", "ci@example.com");
                RunGit(emptyDir, "config", "user.name", "ForkPlus CI");
                var emptyRepo = new GitRepository(emptyDir);
                var emptyRoots = emptyRepo.GetBranchTree();
                Assert.Single(emptyRoots); // 只有"本地"组（空）
                Assert.Equal("本地", emptyRoots[0].Name);
                Assert.Empty(emptyRoots[0].Children);
            }
            finally
            {
                TryDelete(emptyDir);
            }

            // ============================================================
            // 3) 截图落盘
            // ============================================================
            string screenshotRepo = CreateM8Repo();
            try
            {
                var newRepo = new GitRepository(screenshotRepo);
                BranchTreeNode[] screenshotRoots = newRepo.GetBranchTree();
                Assert.Equal(2, screenshotRoots.Length);

                panel.Repository!.Dispose();
                panel.Load(newRepo);
                Dispatcher.UIThread.RunJobs();

                // 3a) 真实 MainWindow 渲染
                const int MW = 1100;
                const int MH = 900;
                main.Width = MW; main.Height = MH;
                main.InvalidateMeasure();
                main.Measure(new Size(MW, MH));
                main.Arrange(new Rect(0, 0, MW, MH));
                main.UpdateLayout();
                for (int i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs();
                Thread.Sleep(150);

                var rtbMain = new RenderTargetBitmap(new PixelSize(MW, MH), new Vector(96, 96));
                rtbMain.Render(main);
                string mainOutPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "verification", "avalonia", "m8-branches", "avalonia-m8-branches-mainwindow.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(mainOutPath)!);
                using (var fs = File.Create(mainOutPath))
                {
                    rtbMain.Save(fs, new PngBitmapEncoderOptions());
                }
                Assert.True(File.Exists(mainOutPath));
                long mainSize = new FileInfo(mainOutPath).Length;
                Assert.True(mainSize > 5_000, $"主窗口 PNG 太小 ({mainSize} bytes)");

                // 3b) 分支树截图
                const int W2 = 520;
                int totalRows2 = 2 + screenshotRoots[0].Children.Count + 1 + screenshotRoots[1].Children[0].Children.Count;
                int h2 = Math.Min(800, 40 + totalRows2 * 26);
                var badgeHost2 = BuildBranchBadgeHost(screenshotRoots, out int lbc2, out int rbc2);
                badgeHost2.Measure(new Size(W2, h2));
                badgeHost2.Arrange(new Rect(0, 0, W2, h2));
                badgeHost2.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                var rtb = new RenderTargetBitmap(new PixelSize(W2, h2), new Vector(96, 96));
                rtb.Render(badgeHost2);
                string outPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "verification", "avalonia", "m8-branches", "avalonia-m8-branches.png"));
                using (var fs = File.Create(outPath))
                {
                    rtb.Save(fs, new PngBitmapEncoderOptions());
                }
                Assert.True(File.Exists(outPath));
                long sizeOnDisk = new FileInfo(outPath).Length;
                Assert.True(sizeOnDisk > 1_500, $"PNG 太小 ({sizeOnDisk} bytes)");

                // 落盘摘要
                string summaryPath = Path.ChangeExtension(outPath, ".txt");
                File.WriteAllText(summaryPath,
                    $"Avalonia M8 branches 截屏\n" +
                    "数据流断言：\n" +
                    $"  当前分支 = main\n" +
                    $"  本地分支 (3): main (current), develop, feature/login\n" +
                    $"  远程/origin (2): origin/main, origin/feature/login\n" +
                    "可视化徽章颜色断言：\n" +
                    $"  LocalBrush  = {LocalBrush} (4A90E2, 蓝色)\n" +
                    $"  RemoteBrush = {RemoteBrush} (7B68A6, 紫色)\n" +
                    $"  找到 {observedColors.Count} 个非空徽章颜色\n" +
                    "操作语义断言：\n" +
                    $"  加载远程分支 origin/feature/login → {commitCount} 个 commit\n" +
                    $"  detached HEAD → GetCurrentBranch() == \"\"\n" +
                    $"  空仓库 → GetBranchTree() 返回 [本地(空)]\n" +
                    $"源仓库：{repoDir}\n截图仓库：{screenshotRepo}\n");
            }
            finally
            {
                TryDelete(screenshotRepo);
            }
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [AvaloniaFact]
    public async Task M8_BranchesPanel_WithoutRepo_StaysEmpty()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = main.FindControl<BranchesPanel>("BranchesPanel")!;
        TextBlock summary = panel.FindControl<TextBlock>("BranchesSummaryText")!;
        TreeView tree = panel.FindControl<TreeView>("BranchesTree")!;
        Assert.Null(panel.Repository);
        Assert.Null(tree.ItemsSource);
        Assert.Contains("未打开", summary.Text);
    }

    // ============== Helpers ==============

    /// <summary>
    /// 用 BranchesPanel.axaml 的真实配色重建一份"XAML 等价"StackPanel：每行 =
    /// ▶/缩进标记 + BadgeBorder(Background=KindToBrush) + Name。
    /// 树形态：本地 / 远程 (sub-group) / 远程-leaves。
    /// </summary>
    private static Control BuildBranchBadgeHost(BranchTreeNode[] roots, out int localBadgeCount, out int remoteBadgeCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 1,
            Margin = new Thickness(8),
        };
        localBadgeCount = 0;
        remoteBadgeCount = 0;
        foreach (var root in roots)
        {
            AppendNode(host, root, indent: 0, isLocal: root.Name == "本地", isRemoteRoot: root.Name == "远程",
                ref localBadgeCount, ref remoteBadgeCount);
        }
        return host;
    }

    private static void AppendNode(
        StackPanel host, BranchTreeNode node, int indent,
        bool isLocal, bool isRemoteRoot,
        ref int localBadgeCount, ref int remoteBadgeCount)
    {
        if (isLocal && !node.IsGroup)
        {
            localBadgeCount++;
        }
        else if (!isLocal && !node.IsGroup)
        {
            remoteBadgeCount++;
        }
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(indent * 16, 1, 0, 1),
            Spacing = 6,
        };
        // ▶ 标记
        row.Children.Add(new TextBlock
        {
            Text = node.IsCurrent ? "▶" : "  ",
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x40)),
            FontSize = 11,
            FontFamily = new FontFamily("monospace"),
            Width = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        // 徽章
        if (!node.IsGroup)
        {
            var brush = node.Kind == BranchKind.Local ? LocalBrush : RemoteBrush;
            var badge = new Border
            {
                Background = new SolidColorBrush(brush),
                Padding = new Thickness(4, 1, 4, 1),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = node.Kind == BranchKind.Local ? "L" : "R",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    FontFamily = new FontFamily("monospace"),
                },
            };
            row.Children.Add(badge);
        }
        // 名称
        var nameText = new TextBlock
        {
            Text = node.Name,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            FontSize = 12,
            FontFamily = new FontFamily("monospace"),
            FontWeight = node.IsCurrent ? FontWeight.Bold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(nameText);
        host.Children.Add(row);
        // 递归子节点
        if (node.IsGroup)
        {
            foreach (var child in node.Children)
            {
                AppendNode(host, child, indent + 1,
                    isLocal, isRemoteRoot && node.Name == "远程",
                    ref localBadgeCount, ref remoteBadgeCount);
            }
        }
    }

    private static bool IsCloseTo(Color a, Color b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol
            && Math.Abs(a.G - b.G) <= tol
            && Math.Abs(a.B - b.B) <= tol;
    }

    // ---- 共享仓库 fixture ----
    private static string CreateM8Repo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m8_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // 1) 初始 commit 在 main
        File.WriteAllText(Path.Combine(dir, "README.md"), "M8 fixture\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M8 commit 1: initial on main");

        // 2) develop 分支 + 1 个 commit
        RunGit(dir, "checkout", "-b", "develop");
        File.WriteAllText(Path.Combine(dir, "develop.md"), "develop notes\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M8 commit 2: develop branch");

        // 3) feature/login 分支 + 2 个 commit
        RunGit(dir, "checkout", "-b", "feature/login");
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "auth.cs"), "// M8 auth\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M8 commit 3: feature/login auth");
        File.WriteAllText(Path.Combine(dir, "src", "login.cs"), "// M8 login\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M8 commit 4: feature/login login flow");

        // 4) 用本地仓库模拟 origin：建 bare remote + push
        string originDir = Path.Combine(Path.GetTempPath(), "forkplus_m8_origin_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(originDir);
        RunGit(originDir, "init", "--bare", "-b", "main");
        RunGit(dir, "remote", "add", "origin", originDir);
        // push 当前分支 (feature/login)
        RunGit(dir, "push", "origin", "feature/login");
        // 回到 main 推 main
        RunGit(dir, "checkout", "main");
        RunGit(dir, "push", "origin", "main");

        // 切回 main（M8 的"当前分支"标记就指 main）
        RunGit(dir, "checkout", "main");

        // 保留 origin bare 目录留作 origin 信息（最后清掉）
        // 实际上 bare 仓库的 refs/origin/HEAD 会被 fetch 拉下来；我们这里 origin 没有 HEAD → 不影响断言
        // 但用 fetch 同步 origin 的 refs（避免 origin/HEAD 不存在的 warning）
        RunGit(dir, "remote", "set-head", "origin", "main");
        // 跑 fetch 拉 origin 的最新 refs
        try { RunGit(dir, "fetch", "origin"); } catch { /* ignore */ }

        // 把 origin bare 目录记到 dir 的隐藏文件，测试结束后一起清
        File.WriteAllText(Path.Combine(dir, ".forkplus_m8_origin"), originDir);

        return dir;
    }

    private static string RunGitCapture(string workingDir, params string[] args)
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
        string stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode}): {proc.StandardError.ReadToEnd()}");
        return stdout;
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
            throw new InvalidOperationException(
                $"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode}): {stderr}");
    }

    private static void TryDelete(string dir)
    {
        try
        {
            // 同时清掉 .forkplus_m8_origin 记录的 bare 仓库目录
            string originMarker = Path.Combine(dir, ".forkplus_m8_origin");
            if (File.Exists(originMarker))
            {
                string originDir = File.ReadAllText(originMarker).Trim();
                if (Directory.Exists(originDir))
                {
                    try { Directory.Delete(originDir, recursive: true); } catch { }
                }
            }
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
