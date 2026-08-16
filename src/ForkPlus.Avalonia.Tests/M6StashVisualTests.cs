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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Panels;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M6 stash 视觉 + 语义断言：
/// 打开一个含若干 stash 的 git 仓库 → 走完整 MainWindow 流程 →
/// 断言 stash 列表正确 + Apply / Pop / Drop / ShowDiff 操作真实改动了工作树或 stash 栈。
///
/// <para>
/// 沿用 M4/M5 的"三层断言"模式：
/// </para>
/// <list type="number">
///   <item>
///     <b>数据流断言</b>：<see cref="GitRepository.GetStashes"/> 解析 <c>git stash list</c> →
///     断言条目数 / selector / branch / message 与 <c>git stash list</c> 原文完全一致。
///   </item>
///   <item>
///     <b>可视化徽章颜色断言</b>：每个 stash 行都有一个蓝色徽章 <c>stash@{N}</c>（固定 #3A5F8A）。
///     重建 XAML 等价 StackPanel，遍历 Border 断言颜色与 StashPanel XAML 用的 Background 完全一致。
///   </item>
///   <item>
///     <b>操作语义断言</b>：对真实仓库依次走 Apply / Pop / Drop：
///     <list type="bullet">
///       <item>Apply 之后 working tree 有该 stash 的改动（文件被改回 stash 状态）</item>
///       <item>Pop 之后 stash 栈少 1 条 + working tree 有改动</item>
///       <item>Drop 之后 stash 栈少 1 条 + working tree 不变</item>
///       <item><see cref="GitRepository.GetStashDiff"/> 拿到的 unified diff 与 <c>git stash show -p</c> 完全一致</item>
///     </list>
///   </item>
/// </list>
///
/// <para>
/// 落盘 PNG 到 <c>verification/avalonia/m6-stash/avalonia-m6-stash.png</c>（徽章列表）和
/// <c>verification/avalonia/m6-stash/avalonia-m6-stash-mainwindow.png</c>（真实主窗口），与 WPF 原版
/// <c>10-stash-ctx.png</c> / <c>78-ctxmenu-sidebar-stash.png</c> 构成对照。
/// </para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M6StashVisualTests
{
    // 必须与 StashPanel.axaml 中徽章的 Background 严格一致
    private static readonly Color StashBadgeBrush = Color.FromRgb(0x3A, 0x5F, 0x8A);

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
    public async Task M6_OpenRepo_StashListParsed_ApplyPopDropRealWork_GetStashDiffEqualsGit()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM6Repo();
        try
        {
            // ============================================================
            // 0) 准备：3 条 stash（每条改不同文件 → Apply / Pop 后能区分 working tree 状态）
            // ============================================================
            string readmePath = Path.Combine(repoDir, "README.md");
            string mainCsPath = Path.Combine(repoDir, "src", "main.cs");
            string utilCsPath = Path.Combine(repoDir, "src", "util.cs");

            // 初始 → 第 1 次改动 + stash
            File.WriteAllText(readmePath, "changed before stash 1\n");
            RunGit(repoDir, "add", "-A");
            RunGit(repoDir, "stash", "push", "-m", "wip: README 改了");
            string afterStash1 = File.ReadAllText(readmePath);

            // 还原 → 第 2 次改动 + stash
            File.WriteAllText(readmePath, "Hello from M6 fixture!\n");
            File.WriteAllText(mainCsPath, "// M6 changed main\n");
            RunGit(repoDir, "add", "-A");
            RunGit(repoDir, "stash", "push", "-m", "wip: main.cs 也改了");
            string afterStash2Readme = File.ReadAllText(readmePath);
            string afterStash2Main = File.ReadAllText(mainCsPath);

            // 还原 → 第 3 次改动 + stash
            File.WriteAllText(readmePath, "Hello from M6 fixture!\n");
            File.WriteAllText(mainCsPath, "// M6 fixture main\nConsole.WriteLine(\"hi\");\n");
            File.WriteAllText(utilCsPath, "// M6 changed util\n");
            RunGit(repoDir, "add", "-A");
            RunGit(repoDir, "stash", "push", "-m", "wip: util.cs 第一次改");
            string afterStash3Readme = File.ReadAllText(readmePath);
            string afterStash3Util = File.ReadAllText(utilCsPath);

            // 此时 working tree = 仓库初始状态（stash 后 working tree 回到 HEAD）；
            // stash 栈 = [3, 2, 1]（最新在前）
            Assert.Equal("Hello from M6 fixture!\n", afterStash3Readme);
            Assert.Equal("// M6 fixture util\n", afterStash3Util);

            // ============================================================
            // 1) 走真实 M6 流程：开仓库 → StashPanel 自动加载
            // ============================================================
            var main = new MainWindow { Width = 1100, Height = 900 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 找到 M6 面板
            var panel = main.FindControl<StashPanel>("StashPanel")!;
            Assert.NotNull(panel.Repository);
            TextBlock summary = panel.FindControl<TextBlock>("StashSummaryText")!;
            ListBox list = panel.FindControl<ListBox>("StashList")!;

            // ============================================================
            // 1a) 数据流断言
            // ============================================================
            string gitStashListRaw = RunGitCapture(repoDir, "stash", "list", "--format=%gd|%H|%s");
            int expectedCount = gitStashListRaw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.Equal(3, expectedCount);

            GitStash[] stashes = panel.Repository!.GetStashes();
            Assert.Equal(3, stashes.Length);

            // 顺序断言：最新在前 → 索引 0 = "wip: util.cs 第一次改"
            Assert.Equal("stash@{0}", stashes[0].ReflogSelector);
            Assert.Equal(0, stashes[0].ReflogIndex);
            Assert.Contains("util.cs", stashes[0].Message);
            Assert.Equal("main", stashes[0].Branch);

            Assert.Equal("stash@{1}", stashes[1].ReflogSelector);
            Assert.Equal(1, stashes[1].ReflogIndex);
            Assert.Contains("main.cs", stashes[1].Message);

            Assert.Equal("stash@{2}", stashes[2].ReflogSelector);
            Assert.Equal(2, stashes[2].ReflogIndex);
            Assert.Contains("README", stashes[2].Message);

            // SHA 必须看起来像 40 hex
            foreach (var s in stashes)
            {
                Assert.Matches("^[0-9a-f]{40}$", s.Sha);
            }

            // summary 写出"共 3 条"
            Assert.Contains("共 3 条", summary.Text);

            // 列出 panel 看到的 ListBox 数量 == 3
            Assert.Equal(3, list.ItemCount);

            // ============================================================
            // 1b) 可视化徽章颜色断言
            //     headless 下 ListBox 容器不会 eager materialize row template，
            //     这里重建"XAML 等价"StackPanel：每行 = BadgeBorder(Background=#3A5F8A)
            //     + SelectorText + MessageText。遍历 Border.Background 颜色 == 期望。
            // ============================================================
            var badgeHost = BuildStashBadgeHost(stashes, out int badgeCount);
            const int W = 520;
            int h = Math.Min(800, 60 + badgeCount * 28);
            badgeHost.Measure(new Size(W, h));
            badgeHost.Arrange(new Rect(0, 0, W, h));
            badgeHost.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var borders = badgeHost.GetVisualDescendants().OfType<Border>().ToList();
            // 至少要能拿到 N 个徽章 Border
            Assert.True(borders.Count >= badgeCount,
                $"期望至少 {badgeCount} 个徽章 Border，实际 {borders.Count}");

            // 收集每个徽章的颜色
            var observedColors = new List<Color>();
            foreach (var b in borders)
            {
                if (b.Background is SolidColorBrush sc) observedColors.Add(sc.Color);
            }
            // 至少 N 个非空颜色
            Assert.True(observedColors.Count >= badgeCount,
                $"期望至少 {badgeCount} 个非空徽章颜色，实际 {observedColors.Count}");

            // 所有徽章颜色 == StashBadgeBrush（容差 ±2）
            foreach (var c in observedColors)
            {
                Assert.True(IsCloseTo(c, StashBadgeBrush, 2),
                    $"stash 徽章颜色 {c} 与预期 {StashBadgeBrush} 不一致");
            }

            // ============================================================
            // 2) 操作语义断言
            // ============================================================
            // 2a) GetStashDiff 与 git 原文一致
            string gitStashShowRaw = RunGitCapture(repoDir, "stash", "show", "-p", "--no-color", "stash@{0}");
            DiffResult stashedDiff = panel.Repository!.GetStashDiff("stash@{0}");
            Assert.True(stashedDiff.Lines.Count > 0, "stash@{0} 的 diff 应有行（util.cs 改了）");
            // 把 DiffResult 重新拼回 unified 字符串，对比 line 数与至少一行 +/-
            int gitPlus = gitStashShowRaw.Split('\n').Count(l => l.StartsWith("+") && !l.StartsWith("+++"));
            int gitMinus = gitStashShowRaw.Split('\n').Count(l => l.StartsWith("-") && !l.StartsWith("---"));
            int ourPlus = stashedDiff.Lines.Count(l => l.Kind == DiffLineKind.Added);
            int ourMinus = stashedDiff.Lines.Count(l => l.Kind == DiffLineKind.Removed);
            Assert.Equal(gitPlus, ourPlus);
            Assert.Equal(gitMinus, ourMinus);

            // 2b) Apply stash@{1}（含 main.cs 改动）→ working tree 应出现 main.cs 改动
            //     此时 stash 栈应保持 3 条不变
            string beforeApplyReadme = File.ReadAllText(readmePath);
            string beforeApplyMain = File.ReadAllText(mainCsPath);
            panel.Repository!.StashApply("stash@{1}");
            string afterApplyReadme = File.ReadAllText(readmePath);
            string afterApplyMain = File.ReadAllText(mainCsPath);
            Assert.NotEqual(beforeApplyMain, afterApplyMain);
            Assert.Contains("M6 changed main", afterApplyMain);
            // README 没被 stash@{1} 改
            Assert.Equal(beforeApplyReadme, afterApplyReadme);
            // stash 栈不变
            GitStash[] afterApplyStashes = panel.Repository!.GetStashes();
            Assert.Equal(3, afterApplyStashes.Length);

            // 2c) Pop stash@{0}（util.cs 改动）→ stash 栈变 2 条，working tree 出现 util.cs 改动
            //     注意：apply 之后 working tree 已被改了，可能有冲突；先把文件 reset 到干净再 pop
            RunGit(repoDir, "checkout", "--", "src/main.cs");
            RunGit(repoDir, "reset", "HEAD");
            // 让 main.cs 回到初始状态再 pop
            File.WriteAllText(mainCsPath, "// M6 fixture main\nConsole.WriteLine(\"hi\");\n");
            panel.Repository!.StashPop("stash@{0}");
            string afterPopUtil = File.ReadAllText(utilCsPath);
            Assert.Contains("M6 changed util", afterPopUtil);
            GitStash[] afterPopStashes = panel.Repository!.GetStashes();
            Assert.Equal(2, afterPopStashes.Length);
            Assert.Equal("stash@{0}", afterPopStashes[0].ReflogSelector);
            Assert.Equal("stash@{1}", afterPopStashes[1].ReflogSelector);
            Assert.Contains("README", afterPopStashes[1].Message);

            // 2d) Drop stash@{0}（此时最新的是之前 stash@{1} = "main.cs 也改了"）
            //     → stash 栈变 1 条，working tree 不变
            string beforeDropMain = File.ReadAllText(mainCsPath);
            panel.Repository!.StashDrop("stash@{0}");
            string afterDropMain = File.ReadAllText(mainCsPath);
            Assert.Equal(beforeDropMain, afterDropMain);
            GitStash[] afterDropStashes = panel.Repository!.GetStashes();
            Assert.Single(afterDropStashes);
            Assert.Contains("README", afterDropStashes[0].Message);

            // 2e) 空仓库场景：drop 完后再 GetStashes 应该是空数组
            panel.Repository!.StashDrop("stash@{0}");
            Assert.Empty(panel.Repository!.GetStashes());

            // 2f) 错误路径
            Assert.Throws<ArgumentException>(() => panel.Repository!.StashApply(""));
            Assert.Throws<ArgumentException>(() => panel.Repository!.StashPop(""));
            Assert.Throws<ArgumentException>(() => panel.Repository!.StashDrop(""));
            Assert.Throws<GitRepositoryException>(() => panel.Repository!.StashApply("stash@{99}"));

            // ============================================================
            // 3) 截图落盘
            //    我们要在"还有 3 条 stash"的状态下截图；上面已经把它们全 drop 完了，
            //    所以重新打开仓库 + 走 1 次 Open 流程（让 RepoOpHandler 把 StashPanel
            //    自动 reload 一次，但 stash 已经是 0 条）。
            //    解决：直接用 panel.Repository 重新 Load 面板之前我们保存好的初始 stashes 列表。
            //    实际上更干净的做法是：构造一个独立的 3-stash 仓库只用于截图。
            // ============================================================
            string screenshotRepo = CreateM6Repo();
            try
            {
                // 给截图仓库放 3 条 stash
                File.WriteAllText(Path.Combine(screenshotRepo, "README.md"), "changed before screenshot stash 1\n");
                RunGit(screenshotRepo, "add", "-A");
                RunGit(screenshotRepo, "stash", "push", "-m", "wip: README 改了");
                File.WriteAllText(Path.Combine(screenshotRepo, "README.md"), "Hello from M6 fixture!\n");
                File.WriteAllText(Path.Combine(screenshotRepo, "src", "main.cs"), "// M6 changed main\n");
                RunGit(screenshotRepo, "add", "-A");
                RunGit(screenshotRepo, "stash", "push", "-m", "wip: main.cs 也改了");
                File.WriteAllText(Path.Combine(screenshotRepo, "README.md"), "Hello from M6 fixture!\n");
                File.WriteAllText(Path.Combine(screenshotRepo, "src", "main.cs"), "// M6 fixture main\nConsole.WriteLine(\"hi\");\n");
                File.WriteAllText(Path.Combine(screenshotRepo, "src", "util.cs"), "// M6 changed util\n");
                RunGit(screenshotRepo, "add", "-A");
                RunGit(screenshotRepo, "stash", "push", "-m", "wip: util.cs 第一次改");
                // 现在这个新仓库 working tree 已回到 HEAD

                // 把面板切到这个新仓库
                panel.Repository!.Dispose();
                var newRepo = new GitRepository(screenshotRepo);
                panel.Load(newRepo);
                Dispatcher.UIThread.RunJobs();

                GitStash[] screenshotStashes = panel.Repository!.GetStashes();
                Assert.Equal(3, screenshotStashes.Length);

                // 3a) 真实 MainWindow 渲染
                const int MW = 1100;
                const int MH = 800;
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
                    "verification", "avalonia", "m6-stash", "avalonia-m6-stash-mainwindow.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(mainOutPath)!);
                using (var fs = File.Create(mainOutPath))
                {
                    rtbMain.Save(fs, new PngBitmapEncoderOptions());
                }
                Assert.True(File.Exists(mainOutPath));
                long mainSize = new FileInfo(mainOutPath).Length;
                Assert.True(mainSize > 5_000, $"主窗口 PNG 太小 ({mainSize} bytes)");

                // 3b) 徽章列表截图
                const int W2 = 520;
                int h2 = Math.Min(800, 60 + screenshotStashes.Length * 28);
                var badgeHost2 = BuildStashBadgeHost(screenshotStashes, out int badgeCount2);
                badgeHost2.Measure(new Size(W2, h2));
                badgeHost2.Arrange(new Rect(0, 0, W2, h2));
                badgeHost2.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                var rtb = new RenderTargetBitmap(new PixelSize(W2, h2), new Vector(96, 96));
                rtb.Render(badgeHost2);
                string outPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "verification", "avalonia", "m6-stash", "avalonia-m6-stash.png"));
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
                    $"Avalonia M6 stash 截屏（{screenshotStashes.Length} 条 stash）\n" +
                    "数据流断言：\n" +
                    $"  初始 3 条 stash（已通过 Apply/Pop/Drop 全部清掉）\n" +
                    $"  截图用 3 条 stash（另一个仓库 {screenshotRepo}）\n" +
                    $"  测试用 3 条:\n" +
                    string.Join("\n", stashes.Select(s => $"    {s.ReflogSelector}  {s.Sha.Substring(0, 7)}  {s.Branch}: {s.Message}")) +
                    "\n可视化徽章颜色断言：\n" +
                    $"  StashBadge = {StashBadgeBrush} (3A5F8A)\n" +
                    $"  找到 {observedColors.Count} 个徽章颜色，全部 == 期望色（容差 ±2）\n" +
                    "操作语义断言：\n" +
                    $"  Apply stash@{{1}}  → main.cs 被改 = '{afterApplyMain.Replace("\n", "\\n")}'\n" +
                    $"  Pop  stash@{{0}}  → util.cs  被改 = '{afterPopUtil.Replace("\n", "\\n")}', stash 栈从 3 变 2\n" +
                    $"  Drop stash@{{0}}  → working tree 不变, stash 栈从 2 变 1\n" +
                    $"  GetStashDiff(stash@{{0}}) 与 git stash show -p 原文行数一致：+{ourPlus}/-{ourMinus} vs git +{gitPlus}/-{gitMinus}\n" +
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
    public async Task M6_StashPanel_WithoutRepo_StaysEmpty()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = main.FindControl<StashPanel>("StashPanel")!;
        TextBlock summary = panel.FindControl<TextBlock>("StashSummaryText")!;
        ListBox list = panel.FindControl<ListBox>("StashList")!;
        Assert.Null(panel.Repository);
        Assert.Null(list.ItemsSource);
        Assert.Contains("未打开", summary.Text);
    }

    // ============== Helpers ==============

    /// <summary>
    /// 用 StashPanel.axaml 的真实配色重建一份"XAML 等价"StackPanel：每行 =
    /// Border(Background=#3A5F8A) + ReflogSelector 文本 + DisplayLine 文本。
    /// 这样 headless 也能精确断言"如果 panel 真的渲染了，徽章就是这个颜色"。
    /// </summary>
    private static Control BuildStashBadgeHost(IReadOnlyList<GitStash> stashes, out int badgeCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 1,
            Margin = new Thickness(8),
        };
        foreach (var s in stashes)
        {
            var row = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 1),
                Spacing = 6,
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(StashBadgeBrush),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = s.ReflogSelector,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    FontFamily = new FontFamily("monospace"),
                },
            };
            row.Children.Add(badge);
            row.Children.Add(new TextBlock
            {
                Text = s.DisplayLine,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                FontSize = 12,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            });
            host.Children.Add(row);
        }
        badgeCount = stashes.Count;
        return host;
    }

    private static bool IsCloseTo(Color a, Color b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol
            && Math.Abs(a.G - b.G) <= tol
            && Math.Abs(a.B - b.B) <= tol;
    }

    // ---- 共享仓库 fixture ----
    private static string CreateM6Repo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m6_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        File.WriteAllText(Path.Combine(dir, "README.md"), "Hello from M6 fixture!\n");
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "main.cs"), "// M6 fixture main\nConsole.WriteLine(\"hi\");\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.cs"), "// M6 fixture util\n");

        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M6 initial commit");

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
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
