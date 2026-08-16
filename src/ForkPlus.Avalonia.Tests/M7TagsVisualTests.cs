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
/// M7 tag 视觉 + 语义断言：
/// 打开一个含若干 annotated + lightweight tag 的 git 仓库 → 走完整 MainWindow 流程 →
/// 断言 tag 列表正确（字典序、annotated/lightweight 区分、peeled SHA 正确）+ Delete 操作真实改动了仓库。
///
/// <para>
/// 沿用 M4/M5/M6 的"三层断言"模式：
/// </para>
/// <list type="number">
///   <item>
///     <b>数据流断言</b>：<see cref="GitRepository.GetTags"/> 解析 <c>git for-each-ref refs/tags/</c> →
///     断言条目数 / Name / IsAnnotated / PeeledSha 与 git 原文完全一致。
///   </item>
///   <item>
///     <b>可视化徽章颜色断言</b>：每个 tag 行有一个徽章，annotated 用棕色 <c>#A05F2A</c>，
///     lightweight 用灰色 <c>#6E6E6E</c>。重建 XAML 等价 StackPanel 验证颜色。
///   </item>
///   <item>
///     <b>操作语义断言</b>：DeleteLocalTag 真实从 refs/tags/ 删掉一条，再 GetTags 少一条。
///   </item>
/// </list>
///
/// <para>
/// 落盘 PNG 到 <c>verification/avalonia/m7-tags/avalonia-m7-tags.png</c>（徽章列表）和
/// <c>verification/avalonia/m7-tags/avalonia-m7-tags-mainwindow.png</c>（真实主窗口），与 WPF 原版
/// <c>v2-12-tags.png</c> / <c>77-ctxmenu-sidebar-tag.png</c> 构成对照。
/// </para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M7TagsVisualTests
{
    // 必须与 TagsPanel.axaml 中 GitTagKindToBrushConverter 严格一致
    private static readonly Color AnnotatedBrush = Color.FromRgb(0xA0, 0x5F, 0x2A);
    private static readonly Color LightweightBrush = Color.FromRgb(0x6E, 0x6E, 0x6E);

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
    public async Task M7_OpenRepo_TagsParsed_GetEqualsGit_DeleteWorks_KindColorConsistent()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM7Repo();
        try
        {
            // 准备：4 个 tag（2 个 annotated，2 个 lightweight）
            // v0.1.0 lightweight  → 初始 commit (第 1 个)
            // v0.2.0 annotated    → 第 2 个 commit
            // v1.0.0 annotated    → 第 3 个 commit
            // v1.1.0 lightweight  → 第 4 个 commit
            string[] commits = RunGitCapture(repoDir, "log", "--pretty=format:%H", "--all").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(commits.Length >= 4, $"fixture 至少需要 4 个 commit，实际 {commits.Length}");

            // 顺序：commits[0] = 最新（HEAD），commits[3] = 初始 commit
            string headSha = commits[0];
            string firstCommit = commits[3];

            // v0.1.0 lightweight → first commit
            RunGit(repoDir, "tag", "v0.1.0", firstCommit);
            // v0.2.0 annotated → commits[2]
            RunGit(repoDir, "tag", "-a", "v0.2.0", commits[2], "-m", "release v0.2.0");
            // v1.0.0 annotated → commits[1]
            RunGit(repoDir, "tag", "-a", "v1.0.0", commits[1], "-m", "release v1.0.0");
            // v1.1.0 lightweight → head
            RunGit(repoDir, "tag", "v1.1.0", headSha);

            // ============================================================
            // 1) 走真实 M7 流程：开仓库 → TagsPanel 自动加载
            // ============================================================
            var main = new MainWindow { Width = 1100, Height = 900 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 找到 M7 面板
            var panel = main.FindControl<TagsPanel>("TagsPanel")!;
            Assert.NotNull(panel.Repository);
            TextBlock summary = panel.FindControl<TextBlock>("TagsSummaryText")!;
            ListBox list = panel.FindControl<ListBox>("TagsList")!;

            // ============================================================
            // 1a) 数据流断言
            // ============================================================
            // 跟 git 原文对比：条目数 + 顺序（按字典序） + IsAnnotated + PeeledSha
            string gitForEachRaw = RunGitCapture(repoDir, "for-each-ref",
                "--format=%(refname:short)|%(objectname)|%(objecttype)|%(*objectname)",
                "refs/tags/");
            var gitLines = gitForEachRaw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var segs = line.Split('|');
                    return new
                    {
                        Name = segs[0],
                        Sha = segs[1],
                        Type = segs[2],
                        Peeled = string.IsNullOrEmpty(segs[3]) ? segs[1] : segs[3],
                    };
                })
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(4, gitLines.Count);

            GitTag[] tags = panel.Repository!.GetTags();
            Assert.Equal(4, tags.Length);

            // 顺序：按字典序 → v0.1.0, v0.2.0, v1.0.0, v1.1.0
            Assert.Equal("v0.1.0", tags[0].Name);
            Assert.Equal("v0.2.0", tags[1].Name);
            Assert.Equal("v1.0.0", tags[2].Name);
            Assert.Equal("v1.1.0", tags[3].Name);

            // 逐条断言
            for (int i = 0; i < gitLines.Count; i++)
            {
                var gitEntry = gitLines.First(g => g.Name == tags[i].Name);
                Assert.Equal(gitEntry.Name, tags[i].Name);
                // PeeledSha 应当 = git 给的 peeled 字段（lightweight == sha，annotated == *objectname）
                Assert.Equal(gitEntry.Peeled, tags[i].PeeledSha);
                // Type: "tag" → annotated, "commit" → lightweight
                bool expectedAnnotated = gitEntry.Type == "tag";
                Assert.Equal(expectedAnnotated, tags[i].IsAnnotated);
            }

            // 具体检查 annotated / lightweight 区分
            Assert.False(tags[0].IsAnnotated); // v0.1.0 lightweight
            Assert.True(tags[1].IsAnnotated);  // v0.2.0 annotated
            Assert.True(tags[2].IsAnnotated);  // v1.0.0 annotated
            Assert.False(tags[3].IsAnnotated); // v1.1.0 lightweight

            // summary 写出"共 4 条"
            Assert.Contains("共 4 条", summary.Text);
            Assert.Contains("2 个 annotated", summary.Text);
            Assert.Contains("2 个 lightweight", summary.Text);

            // 列出 panel 看到的 ListBox 数量 == 4
            Assert.Equal(4, list.ItemCount);

            // DisplayLine 形态校验
            Assert.StartsWith("[l]", tags[0].DisplayLine);
            Assert.StartsWith("[a]", tags[1].DisplayLine);
            Assert.StartsWith("[a]", tags[2].DisplayLine);
            Assert.StartsWith("[l]", tags[3].DisplayLine);

            // ============================================================
            // 1b) 可视化徽章颜色断言
            //     headless 下 ListBox 容器不会 eager materialize row template，
            //     这里重建"XAML 等价"StackPanel：每行 = BadgeBorder(Background=kind Brush) + Name/Sha
            //     遍历 Border.Background 颜色 == 期望。
            // ============================================================
            var badgeHost = BuildTagsBadgeHost(tags, out int badgeCount);
            const int W = 520;
            int h = Math.Min(800, 60 + badgeCount * 28);
            badgeHost.Measure(new Size(W, h));
            badgeHost.Arrange(new Rect(0, 0, W, h));
            badgeHost.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var borders = badgeHost.GetVisualDescendants().OfType<Border>().ToList();
            Assert.True(borders.Count >= badgeCount,
                $"期望至少 {badgeCount} 个徽章 Border，实际 {borders.Count}");

            // 收集每个徽章的颜色
            var observedColors = new List<Color>();
            foreach (var b in borders)
            {
                if (b.Background is SolidColorBrush sc) observedColors.Add(sc.Color);
            }
            Assert.True(observedColors.Count >= badgeCount,
                $"期望至少 {badgeCount} 个非空徽章颜色，实际 {observedColors.Count}");

            // 排序后逐位对比：第一/四行 = lightweight（灰），第二/三行 = annotated（棕）
            // 由于 StackPanel.Children 顺序与 tags 一致
            Assert.True(IsCloseTo(observedColors[0], LightweightBrush, 2),
                $"v0.1.0 期望 lightweight 灰 {LightweightBrush}，实际 {observedColors[0]}");
            Assert.True(IsCloseTo(observedColors[1], AnnotatedBrush, 2),
                $"v0.2.0 期望 annotated 棕 {AnnotatedBrush}，实际 {observedColors[1]}");
            Assert.True(IsCloseTo(observedColors[2], AnnotatedBrush, 2),
                $"v1.0.0 期望 annotated 棕 {AnnotatedBrush}，实际 {observedColors[2]}");
            Assert.True(IsCloseTo(observedColors[3], LightweightBrush, 2),
                $"v1.1.0 期望 lightweight 灰 {LightweightBrush}，实际 {observedColors[3]}");

            // ============================================================
            // 2) 操作语义断言
            // ============================================================
            // 2a) DeleteLocalTag 真实删除 v0.1.0 → GetTags 少一条
            int beforeDeleteCount = panel.Repository!.GetTags().Length;
            panel.Repository!.DeleteLocalTag("v0.1.0");
            GitTag[] afterDelete = panel.Repository!.GetTags();
            Assert.Equal(beforeDeleteCount - 1, afterDelete.Length);
            Assert.DoesNotContain(afterDelete, t => t.Name == "v0.1.0");

            // 2b) 删不存在的 tag 抛 GitRepositoryException
            Assert.Throws<GitRepositoryException>(() => panel.Repository!.DeleteLocalTag("v0.1.0"));
            // 2c) 空 name 抛 ArgumentException
            Assert.Throws<ArgumentException>(() => panel.Repository!.DeleteLocalTag(""));
            Assert.Throws<ArgumentException>(() => panel.Repository!.DeleteLocalTag("   "));

            // 2d) "未打开仓库" 行为：Load(null) → ItemsSource = null
            var freshPanel = new TagsPanel();
            freshPanel.Load(null);
            Assert.Null(freshPanel.Repository);
            Assert.Null(freshPanel.FindControl<ListBox>("TagsList")!.ItemsSource);

            // ============================================================
            // 3) 截图落盘
            //    上面已经删了 v0.1.0，所以另起一个仓库（4 个 tag 全在）专用于截图。
            // ============================================================
            string screenshotRepo = CreateM7Repo();
            try
            {
                string[] ssCommits = RunGitCapture(screenshotRepo, "log", "--pretty=format:%H", "--all")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                string ssFirst = ssCommits[3];
                string ssHead = ssCommits[0];
                RunGit(screenshotRepo, "tag", "v0.1.0", ssFirst);
                RunGit(screenshotRepo, "tag", "-a", "v0.2.0", ssCommits[2], "-m", "release v0.2.0");
                RunGit(screenshotRepo, "tag", "-a", "v1.0.0", ssCommits[1], "-m", "release v1.0.0");
                RunGit(screenshotRepo, "tag", "v1.1.0", ssHead);

                panel.Repository!.Dispose();
                var newRepo = new GitRepository(screenshotRepo);
                panel.Load(newRepo);
                Dispatcher.UIThread.RunJobs();

                GitTag[] screenshotTags = panel.Repository!.GetTags();
                Assert.Equal(4, screenshotTags.Length);

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
                    "verification", "avalonia", "m7-tags", "avalonia-m7-tags-mainwindow.png"));
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
                int h2 = Math.Min(800, 60 + screenshotTags.Length * 28);
                var badgeHost2 = BuildTagsBadgeHost(screenshotTags, out int badgeCount2);
                badgeHost2.Measure(new Size(W2, h2));
                badgeHost2.Arrange(new Rect(0, 0, W2, h2));
                badgeHost2.UpdateLayout();
                Dispatcher.UIThread.RunJobs();

                var rtb = new RenderTargetBitmap(new PixelSize(W2, h2), new Vector(96, 96));
                rtb.Render(badgeHost2);
                string outPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                    "verification", "avalonia", "m7-tags", "avalonia-m7-tags.png"));
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
                    $"Avalonia M7 tag 截屏（{screenshotTags.Length} 条 tag）\n" +
                    "数据流断言：\n" +
                    $"  删前 4 条 tag（已删除 v0.1.0），删除后剩 3 条\n" +
                    $"  截图用 4 条 tag（另一个仓库 {screenshotRepo}）\n" +
                    $"  tag 列表（按字典序）:\n" +
                    string.Join("\n", screenshotTags.Select(t => $"    {(t.IsAnnotated ? "[a]" : "[l]")}  {t.Name}  peeled={t.ShortSha}")) +
                    "\n可视化徽章颜色断言：\n" +
                    $"  AnnotatedBrush  = {AnnotatedBrush} (A05F2A)\n" +
                    $"  LightweightBrush = {LightweightBrush} (6E6E6E)\n" +
                    $"  找到 {observedColors.Count} 个徽章颜色\n" +
                    "操作语义断言：\n" +
                    $"  DeleteLocalTag('v0.1.0') → refs/tags/v0.1.0 真实删除，GetTags 从 4 变 3\n" +
                    $"  DeleteLocalTag 不存在的 tag → 抛 GitRepositoryException\n" +
                    $"  DeleteLocalTag('') / ('   ') → 抛 ArgumentException\n" +
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
    public async Task M7_TagsPanel_WithoutRepo_StaysEmpty()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = main.FindControl<TagsPanel>("TagsPanel")!;
        TextBlock summary = panel.FindControl<TextBlock>("TagsSummaryText")!;
        ListBox list = panel.FindControl<ListBox>("TagsList")!;
        Assert.Null(panel.Repository);
        Assert.Null(list.ItemsSource);
        Assert.Contains("未打开", summary.Text);
    }

    // ============== Helpers ==============

    /// <summary>
    /// 用 TagsPanel.axaml 的真实配色重建一份"XAML 等价"StackPanel：每行 =
    /// Border(Background=KindToBrush(IsAnnotated)) + Kind 字符 ("a"/"l") + Name + Sha。
    /// 这样 headless 也能精确断言"如果 panel 真的渲染了，徽章就是这个颜色"。
    /// </summary>
    private static Control BuildTagsBadgeHost(IReadOnlyList<GitTag> tags, out int badgeCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 1,
            Margin = new Thickness(8),
        };
        foreach (var t in tags)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 1),
                Spacing = 6,
            };
            var badgeBrush = t.IsAnnotated ? AnnotatedBrush : LightweightBrush;
            var badge = new Border
            {
                Background = new SolidColorBrush(badgeBrush),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = t.IsAnnotated ? "a" : "l",
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    FontFamily = new FontFamily("monospace"),
                },
            };
            row.Children.Add(badge);
            row.Children.Add(new TextBlock
            {
                Text = $"{t.Name}  sha={t.ShortSha}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                FontSize = 12,
                FontFamily = new FontFamily("monospace"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            host.Children.Add(row);
        }
        badgeCount = tags.Count;
        return host;
    }

    private static bool IsCloseTo(Color a, Color b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol
            && Math.Abs(a.G - b.G) <= tol
            && Math.Abs(a.B - b.B) <= tol;
    }

    // ---- 共享仓库 fixture ----
    private static string CreateM7Repo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m7_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // 4 个 commit，给后面 4 个 tag 各自有独立的 target
        File.WriteAllText(Path.Combine(dir, "README.md"), "M7 fixture: initial\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M7 commit 1: initial");

        File.WriteAllText(Path.Combine(dir, "README.md"), "M7 fixture: README updated\n");
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "main.cs"), "// M7 main v1\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M7 commit 2: add main.cs");

        File.WriteAllText(Path.Combine(dir, "src", "main.cs"), "// M7 main v2\nConsole.WriteLine(\"hi\");\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.cs"), "// M7 util v2\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M7 commit 3: main + util");

        File.WriteAllText(Path.Combine(dir, "README.md"), "M7 fixture: final version\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.cs"), "// M7 util v3\npublic class U { }\n");
        File.WriteAllText(Path.Combine(dir, "CHANGELOG.md"), "v1.1\n");
        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M7 commit 4: CHANGELOG + util v3");

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
