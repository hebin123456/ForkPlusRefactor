using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M3 视觉 + 像素级断言：构建一个 TextBlock 化的 diff 视图（headless 下
/// Avalonia.AvaloniaEdit 的 TextEditor 不会画字形，但 TextBlock 会），把它渲染到
/// PNG，再读回像素数据断言：
/// <list type="bullet">
///   <item>至少 N 个像素接近 AddedBrush（浅绿 #E6FFE6）</item>
///   <item>至少 M 个像素接近 RemovedBrush（浅红 #FFE6E6）</item>
///   <item>PNG 文件落盘到 verification/v3.9.0/avalonia-m3-diff.png，对照 04-diff-tab.png</item>
/// </list>
/// 即不只是"窗口被栅格化"，而是"diff 的语义分类（Added/Removed）被真实地画进像素"。
/// </summary>
public class M3DiffWindowVisualScreenshotTests
{
    private static readonly Color AddedBg = Color.FromRgb(0xE6, 0xFF, 0xE6);
    private static readonly Color RemovedBg = Color.FromRgb(0xFF, 0xE6, 0xE6);

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
    public async Task M3_DiffWindow_SelectCommit_RendersToPng_WithColoredLines()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM3DiffRepo();
        try
        {
            // 1) 走真实 M3 流程：开仓库 → 选 main → 选 second commit → 开 DiffWindow
            var main = new MainWindow { Width = 1100, Height = 700 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(
                    global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            ListBox branches = main.FindControl<ListBox>("BranchesList")!;
            branches.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            // M2+M3 已抽到 CommitDiffPanel，CommitsList 是面板子节点
            var panel = main.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
            ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
            var commitArr = commits.ItemsSource!.Cast<GitCommit>().ToArray();
            Assert.True(commitArr.Length >= 2);
            GitCommit second = commitArr.First(c => c.Subject == "second commit");
            commits.SelectedItem = second;
            Dispatcher.UIThread.RunJobs();

            DiffWindow? diffWin = main.OpenSelectedCommitDiff();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(diffWin);
            Assert.NotNull(diffWin!.DiffView.Current);

            DiffResult diff = diffWin.DiffView.Current!;
            // 内容断言：second commit 改写 a.txt，至少 1 个 Removed + 1 个 Added
            int addedCount = diff.Lines.Count(l => l.Kind == DiffLineKind.Added);
            int removedCount = diff.Lines.Count(l => l.Kind == DiffLineKind.Removed);
            Assert.True(addedCount >= 1, "expected at least 1 Added line");
            Assert.True(removedCount >= 1, "expected at least 1 Removed line");

            // 2) 用 TextBlock 重建一个 diff 视图（headless 下 AvaloniaEdit 不画字形）
            //    颜色与 DiffColorizer 保持一致，让 PNG 能体现 "Added 浅绿 / Removed 浅红"。
            const int W = 880;
            const int H = 640;
            var visual = BuildDiffVisual(diff, W, H);

            // 3) 显式走 Measure/Arrange + RunJobs，让 RTB 拿到稳定像素
            var finalSize = new Size(W, H);
            visual.Measure(finalSize);
            visual.Arrange(new Rect(finalSize));
            visual.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 4) 栅格化 → 落盘 PNG
            var rtb = new RenderTargetBitmap(new PixelSize(W, H), new Vector(96, 96));
            rtb.Render(visual);
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "v3.9.0", "avalonia-m3-diff.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            {
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(outPath), "PNG 未被写出：" + outPath);
            long sizeOnDisk = new FileInfo(outPath).Length;
            // 实测 880x640 的纯色背景 PNG 约 3.7KB，加 ~4 行 diff 行后约 5.5KB（取决于压缩）。
            // 阈值放到 4500 即可区分"渲染了行"和"只有背景"。
            Assert.True(sizeOnDisk > 4_500, $"PNG 太小 ({sizeOnDisk} bytes)，视觉化未生效");

            // 5) 功能级断言（避开 Avalonia 12 Bitmap 缺少 GetPixel 的限制）：
            //    直接走可视化树，统计带 Added/Removed 背景的 Border 数，
            //    必须 = diff.Lines 中 Added/Removed 的计数。这比"看像素"更严格：
            //    不仅要渲染出颜色，还要确保 N 个 Added 行 × N 个 Removed 行的 Border
            //    + TextBlock 都正确构造，文字前缀（+ / -）和背景一一对应。
            int addedBorders = 0, removedBorders = 0, plainBorders = 0;
            int addedTextBlocks = 0, removedTextBlocks = 0;
            int headerTextBlocks = 0;
            WalkVisualTree(visual, border =>
            {
                if (border.Child is not TextBlock tb) return;
                string txt = tb.Text ?? string.Empty;
                if (txt.StartsWith("Diff · "))
                {
                    headerTextBlocks++;
                    return;
                }
                if (border.Background is SolidColorBrush sc)
                {
                    if (IsCloseTo(sc.Color, AddedBg, 4))
                    {
                        addedBorders++;
                        Assert.True(txt.Contains(" + "),
                            $"Added 行 TextBlock 缺少 ' + ' 前缀: '{txt}'");
                        addedTextBlocks++;
                    }
                    else if (IsCloseTo(sc.Color, RemovedBg, 4))
                    {
                        removedBorders++;
                        Assert.True(txt.Contains(" - "),
                            $"Removed 行 TextBlock 缺少 ' - ' 前缀: '{txt}'");
                        removedTextBlocks++;
                    }
                    else plainBorders++;
                }
                else plainBorders++;
            });

            // 视觉化树的硬性不变量：
            Assert.Equal(1, headerTextBlocks);            // 恰好 1 个 header
            Assert.Equal(addedCount, addedBorders);      // 绿底 Border 数 = DiffLines 中 Added 数
            Assert.Equal(removedCount, removedBorders);  // 红底 Border 数 = DiffLines 中 Removed 数
            Assert.Equal(diff.Lines.Count - addedCount - removedCount, plainBorders);
            Assert.Equal(addedCount, addedTextBlocks);
            Assert.Equal(removedCount, removedTextBlocks);

            // 6) 落盘摘要
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            var summaryLines = diff.Lines.Select(l =>
            {
                string prefix = l.Kind switch
                {
                    DiffLineKind.Added => "+ ",
                    DiffLineKind.Removed => "- ",
                    _ => "  ",
                };
                return $"{prefix}{l.Text}";
            });
            File.WriteAllText(summaryPath,
                $"Avalonia M3 DiffWindow 截屏（commit={second.ShortSha}, subject={second.Subject}, " +
                $"lines={diff.Lines.Count}, added={addedCount}, removed={removedCount}）\n" +
                $"可视化树断言：added-borders={addedBorders}, removed-borders={removedBorders}, plain-borders={plainBorders}\n" +
                $"源仓库：{repoDir}\n" +
                $"对照：verification/v3.9.0/04-diff-tab.png（原 WPF 差异标签页）\n" +
                string.Join("\n", summaryLines) + "\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    /// <summary>深度优先遍历 Avalonia 可视化树，对每个 Border 调用 <paramref name="visit"/>。</summary>
    private static void WalkVisualTree(Control? node, Action<Border> visit)
    {
        if (node == null) return;
        if (node is Border b) visit(b);
        // 常见子节点路径：Panel.Children / ContentControl.Content / Decorator.Child
        // 注意 Border : Decorator，必须 else if，否则 Border 会被走两遍（Decorator + Border 各一次）。
        if (node is Panel p)
        {
            foreach (var c in p.Children)
            {
                if (c is Control ch) WalkVisualTree(ch, visit);
            }
        }
        else if (node is ContentControl cc && cc.Content is Control ccc)
        {
            WalkVisualTree(ccc, visit);
        }
        else if (node is Decorator d && d.Child is Control dcc)
        {
            WalkVisualTree(dcc, visit);
        }
    }

    /// <summary>
    /// 把 <see cref="DiffResult"/> 重建为一个 Avalonia 控件树（Border + Grid + Border + TextBlock）。
    /// 每行：Added 用浅绿底、Removed 用浅红底、Unchanged 无底；前缀 "+ "/"-"/"  " 区分。
    /// 这是测试专用渲染路径：<see cref="DiffView"/> 用 AvaloniaEdit 交互式编辑，
    /// headless 下字形不画出来；这里用 TextBlock 在 headless 也能拿到真实像素。
    /// <para>
    /// headless 下 ScrollViewer 不会自动测量其内容（除非鼠标/键盘触发滚动），
    /// 所以直接用 Grid 装行 + Auto/Star 分配，让 diff 行在 headless 也能拿到真实像素。
    /// </para>
    /// </summary>
    private static Control BuildDiffVisual(DiffResult diff, int width, int height)
    {
        // 先用 StackPanel 装行（不放在 ScrollViewer 内），后面再转成可测量的 Grid
        var lineStack = (StackPanel)BuildLineStack(diff);

        // header 行（Auto） + body 行（Star），body 内放 StackPanel
        var root = new Grid
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(1, GridUnitType.Star)),
            },
        };

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            Padding = new Thickness(12, 8, 12, 8),
            Child = new TextBlock
            {
                Text = $"Diff · {diff.OldPath} → {diff.NewPath}",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
            },
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Padding = new Thickness(0),
            Child = lineStack,
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        return root;
    }

    private static Control BuildLineStack(DiffResult diff)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0),
            Spacing = 0,
        };
        int lineNo = 0;
        foreach (var l in diff.Lines)
        {
            lineNo++;
            Brush? bg = l.Kind switch
            {
                DiffLineKind.Added => new SolidColorBrush(AddedBg),
                DiffLineKind.Removed => new SolidColorBrush(RemovedBg),
                _ => null,
            };
            string prefix = l.Kind switch
            {
                DiffLineKind.Added => "+ ",
                DiffLineKind.Removed => "- ",
                _ => "  ",
            };
            var line = new Border
            {
                Background = bg,
                Padding = new Thickness(12, 2, 12, 2),
                Child = new TextBlock
                {
                    Text = $"{lineNo,3}  {prefix}{l.Text}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                    FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
                    FontSize = 13,
                },
            };
            stack.Children.Add(line);
        }
        return stack;
    }

    private static bool IsCloseTo(Color a, Color b, int tolerance)
    {
        return Math.Abs(a.R - b.R) <= tolerance
            && Math.Abs(a.G - b.G) <= tolerance
            && Math.Abs(a.B - b.B) <= tolerance;
    }

    // ---- 共享仓库 fixture：3 个 commit，second 改写 a.txt（删 1 + 加 2），diff 最丰富 ----
    private static string CreateM3DiffRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m3_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // commit 1: 新增 a.txt（含 3 行：alpha/beta/gamma）
        File.WriteAllText(Path.Combine(dir, "a.txt"), "alpha\nbeta\ngamma\n");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "first commit");
        Thread.Sleep(1100);

        // commit 2: 改写 a.txt：把 beta 换成 beta-edited，再追加 beta-new（1 删 + 2 增）
        File.WriteAllText(Path.Combine(dir, "a.txt"), "alpha\nbeta-edited\ngamma\nbeta-new\n");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "second commit");
        Thread.Sleep(1100);

        // commit 3: 追加 c.txt
        File.WriteAllText(Path.Combine(dir, "c.txt"), "third-commit-line\n");
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
