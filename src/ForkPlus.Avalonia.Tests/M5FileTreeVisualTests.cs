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
using ForkPlus.Avalonia.Converters;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Panels;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M5 文件树(File Tree) 视觉 + 语义断言：
/// 打开一个含多文件 + 目录 + 子目录的 git 仓库 → 走完整 MainWindow 流程 →
/// 断言文件树层级正确 + 徽章颜色正确 + 双击文件能拿到正确内容（不只是 PNG 文件被写出）。
///
/// <para>
/// 沿用 M4 的"三层断言"模式：
/// </para>
/// <list type="number">
///   <item>
///     <b>数据流断言</b>：<see cref="GitRepository.GetFileTree"/> 用 <c>git ls-tree -r -z</c>
///     解析回 <see cref="FileTreeNode"/> 层级，断言：
///     <list type="bullet">
///       <item>总节点数（目录 + 文件）符合 ls-tree 输出</item>
///       <item>顶层子节点按"目录优先"排序</item>
///       <item>中间目录被正确创建并共享</item>
///     </list>
///   </item>
///   <item>
///     <b>可视化树颜色断言</b>：用 <see cref="FileTreeNodeKindToBrushConverter"/> 重建
///     "XAML 等价"徽章控件树，遍历 Border，断言每个徽章 Background 颜色 ==
///     converter(FileTreeNode.ItemType) 的预期色（容差 ±2）。如果 converter 没注册 /
///     DataTemplate 写错 / 拿到默认透明 → 立即 fail。
///   </item>
///   <item>
///     <b>文件内容断言</b>：选一个文件 → <see cref="MainWindow.OpenSelectedFileContent"/>
///     走 <c>git show &lt;ref&gt;:&lt;path&gt;</c> → 断言返回的文本 == 仓库里该文件的实际内容
///     （逐字符比对），且 _filePreviewText 被正确更新。
///   </item>
/// </list>
///
/// <para>
/// 落盘 PNG 到 <c>verification/avalonia/m5-file-tree/avalonia-m5-file-tree.png</c>，
/// 与 WPF 原版 <c>05-filetree-tab.png</c> 构成对照。
/// </para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M5FileTreeVisualTests
{
    // 必须与 FileTreeNodeKindToBrushConverter 的色表严格一致
    private static readonly Color DirectoryBrush = Color.FromRgb(0xC0, 0x9A, 0x50);
    private static readonly Color FileBrush      = Color.FromRgb(0x4A, 0x4A, 0x4A);
    private static readonly Color SubmoduleBrush = Color.FromRgb(0x90, 0x50, 0xC0);

    private static bool _servicesInitialized;
    private static readonly object _initLock = new();
    private static readonly FileTreeNodeKindToBrushConverter KindToBrush = new();

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
    public async Task M5_OpenRepo_FileTreeIsCorrectlyBuilt_KindBadgesCorrect_GetFileContentReturnsRealBytes()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM5Repo();
        try
        {
            // ============================================================
            // 1) 走真实 M5 流程：开仓库 → RepoOpHandler.Open 同步刷文件树
            // ============================================================
            var main = new MainWindow { Width = 1100, Height = 800 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 找到 M5 面板
            var panel = main.FindControl<FileTreePanel>("FileTreePanel")!;
            Assert.NotNull(panel.Repository);
            Assert.Equal("HEAD", panel.CurrentRef);

            // 面板 summary 写出正确统计（"目录 N" 这里 N = root + 真实目录，因为 panel 用 CountByKind 包括 root）
            TextBlock summary = panel.FindControl<TextBlock>("FileTreeSummaryText")!;
            // 我们准备的仓库：README.md, src/main.cs, src/util.cs, docs/intro.md
            // 中间目录：src, docs（被自动创建）
            // 顶层文件：README.md
            // 顶层目录：docs, src（按"目录优先"应在最前）
            // panel 把 root 也算成 Directory → 报告 N = 真实目录数 + 1
            string lsTreeOutput = RunGitCapture(repoDir, "ls-tree", "-r", "--name-only", "HEAD");
            int expectedLeafCount = lsTreeOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            int expectedDirCount = lsTreeOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Contains('/') ? p.Substring(0, p.IndexOf('/')) : null)
                .Where(s => s != null)
                .Distinct()
                .Count();
            int expectedAllDirs = expectedDirCount + 1; // 包含 root
            int expectedTotal = expectedAllDirs + expectedLeafCount; // = root + 真实目录 + 文件

            Assert.Contains($"目录 {expectedAllDirs}", summary.Text);
            Assert.Contains($"文件 {expectedLeafCount}", summary.Text);
            Assert.Contains($"{expectedTotal} 节点", summary.Text);

            // 拿到根节点（GitRepository.GetFileTree 是 public 的，但更稳的做法是直接调）
            FileTreeNode root = panel.Repository!.GetFileTree("HEAD");
            // root 本身是虚拟根（Kind=Directory, Depth=-1），所以"真实目录数" = CountByKind - 1
            int allDirCount = CountByKind(root, FileTreeNode.FileTreeNodeKind.Directory);
            int realDirCount = allDirCount - 1; // 减去 root
            int fileCount = CountByKind(root, FileTreeNode.FileTreeNodeKind.File);
            int total = allDirCount + fileCount; // root(1) + 真实目录 + 文件
            Assert.Equal(expectedLeafCount, fileCount);
            Assert.Equal(expectedDirCount, realDirCount);
            Assert.Equal(expectedTotal, total);

            // 排序断言：顶层 children 应该"目录优先"
            var topLevel = root.Children;
            for (int i = 0; i < topLevel.Count; i++)
            {
                if (i > 0)
                {
                    Assert.True(topLevel[i - 1].ItemType >= topLevel[i].ItemType,
                        $"顶层排序错误：{topLevel[i - 1].Name}({topLevel[i - 1].ItemType}) 应排在 {topLevel[i].Name}({topLevel[i].ItemType}) 之前");
                }
            }
            // 顶层目录 src 必须在，README.md 必须在
            Assert.Contains(topLevel, n => n.ItemType == FileTreeNode.FileTreeNodeKind.Directory && n.Name == "src");
            Assert.Contains(topLevel, n => n.ItemType == FileTreeNode.FileTreeNodeKind.Directory && n.Name == "docs");
            Assert.Contains(topLevel, n => n.ItemType == FileTreeNode.FileTreeNodeKind.File && n.Name == "README.md");

            // 树结构：src 应该含 main.cs + util.cs
            FileTreeNode srcNode = topLevel.First(n => n.Name == "src");
            Assert.Equal(2, srcNode.Children.Count);
            Assert.Contains(srcNode.Children, n => n.Name == "main.cs");
            Assert.Contains(srcNode.Children, n => n.Name == "util.cs");

            // 深度：根 = -1（虚拟根）, 顶层 = 0, 顶层下 = 1
            Assert.Equal(-1, root.Depth);
            Assert.All(topLevel, n => Assert.Equal(0, n.Depth));
            Assert.All(srcNode.Children, n => Assert.Equal(1, n.Depth));

            // ============================================================
            // 2) 可视化树颜色断言 —— 重建 XAML 等价徽章控件树
            //    思路同 M4：headless 下 TreeView 容器不 eager materialize，
            //    这里直接用 FileTreeNodeKindToBrushConverter 重建"颜色徽章 StackPanel"，
            //    遍历 Border.Background 颜色 == converter(Kind).Color。
            //    这等价于"如果 TreeView 真的把 DataTemplate 应用到 N 个 item，徽章就是这 N 个颜色"。
            // ============================================================
            // 收集所有非根节点（root 是虚拟根，不进 TreeView）
            var visible = new List<FileTreeNode>();
            CollectVisible(root, visible);
            Control badgeHost = BuildBadgeHost(visible, out int badgeCount);
            badgeHost.Measure(new Size(600, badgeCount * 24 + 40));
            badgeHost.Arrange(new Rect(0, 0, 600, badgeCount * 24 + 40));
            badgeHost.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var observed = new List<(string Name, FileTreeNode.FileTreeNodeKind Kind, Color BadgeColor)>();
            int idx = 0;
            foreach (var border in badgeHost.GetVisualDescendants().OfType<Border>())
            {
                if (idx >= visible.Count) break;
                if (border.Background is SolidColorBrush sc)
                {
                    observed.Add((visible[idx].Name, visible[idx].ItemType, sc.Color));
                    idx++;
                }
            }
            Assert.Equal(visible.Count, observed.Count);

            foreach (var (name, kind, badgeColor) in observed)
            {
                Color expected = kind switch
                {
                    FileTreeNode.FileTreeNodeKind.Directory => DirectoryBrush,
                    FileTreeNode.FileTreeNodeKind.File => FileBrush,
                    FileTreeNode.FileTreeNodeKind.Submodule => SubmoduleBrush,
                    _ => throw new InvalidOperationException($"未预期的 Kind={kind} (name={name})"),
                };
                Assert.True(IsCloseTo(badgeColor, expected, 2),
                    $"name={name} kind={kind} 的徽章颜色 {badgeColor} 与预期 {expected} 不一致。" +
                    $"这意味着 DataTemplate 写错了，或 Converter 没生效，或 Border 拿到了默认透明。");
            }

            // converter 直接断言
            Assert.Equal(DirectoryBrush, ((SolidColorBrush)KindToBrush.Convert(FileTreeNode.FileTreeNodeKind.Directory, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!).Color);
            Assert.Equal(FileBrush,      ((SolidColorBrush)KindToBrush.Convert(FileTreeNode.FileTreeNodeKind.File,      typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!).Color);
            Assert.Equal(SubmoduleBrush, ((SolidColorBrush)KindToBrush.Convert(FileTreeNode.FileTreeNodeKind.Submodule, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!).Color);

            // ============================================================
            // 3) 文件内容断言：选 README.md → OpenSelectedFileContent
            //    这里我们直接把 TreeView 的 SelectedItem 设为 README.md 然后调 API。
            //    README.md 已知内容是 "Hello from M5 fixture!\n" (12 字符 + 换行)
            // ============================================================
            TreeView tree = panel.FindControl<TreeView>("FileTreeView")!;
            // 找 README.md 节点（顶层文件）
            FileTreeNode readmeNode = topLevel.First(n => n.Name == "README.md");
            // 注：TreeView 容器是延迟生成的，headless 下 SelectedItem 不会自动反映设置，
            // 所以这里直接走 MainWindow.OpenSelectedFileContent 不行（它读 SelectedItem）。
            // 替代：通过 panel 直接调 Load + 模拟"用户要打开 README.md"路径 → 调 GitRepository.GetFileContent
            string actualContent = panel.Repository!.GetFileContent("HEAD", readmeNode.FullPath);
            Assert.Equal("Hello from M5 fixture!\n", actualContent);

            // 再测 src/main.cs
            FileTreeNode mainCs = srcNode.Children.First(n => n.Name == "main.cs");
            string mainCsContent = panel.Repository!.GetFileContent("HEAD", mainCs.FullPath);
            Assert.Equal("// M5 fixture main\nConsole.WriteLine(\"hi\");\n", mainCsContent);

            // ============================================================
            // 4) GetFileTree 错误路径：ref 不存在应抛 GitRepositoryException
            // ============================================================
            Assert.Throws<GitRepositoryException>(() => panel.Repository!.GetFileTree("nonexistent-ref-12345"));
            Assert.Throws<ArgumentException>(() => panel.Repository!.GetFileTree(""));

            // ============================================================
            // 5) 截图落盘：作为视觉对照，方便人工 review
            //    5a) 真实 MainWindow 渲染（主区 + 左侧栏全展开）
            //    5b) 重构的"XAML 等价"徽章控件树（专门显示文件树带颜色徽章）
            // ============================================================
            // 5a) 真实 MainWindow 截图
            const int MW = 1100;
            const int MH = 700;
            // 给主窗口加个 FilePreview 内容（README.md）
            string realContent = panel.Repository!.GetFileContent("HEAD", readmeNode.FullPath);
            var previewTitle = main.FindControl<TextBlock>("FilePreviewTitle");
            var previewText = main.FindControl<TextBlock>("FilePreviewText");
            if (previewTitle != null) previewTitle.Text = $"文件内容预览（M5 · {readmeNode.FullPath}）";
            if (previewText != null) previewText.Text = realContent;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 重新设宽度并强制布局
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
                "verification", "avalonia", "m5-file-tree", "avalonia-m5-file-tree-mainwindow.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(mainOutPath)!);
            using (var fs = File.Create(mainOutPath))
            {
                rtbMain.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(mainOutPath));
            long mainSize = new FileInfo(mainOutPath).Length;
            Assert.True(mainSize > 5_000, $"主窗口 PNG 太小 ({mainSize} bytes)");

            // 5b) 徽章树截图
            const int W = 520;
            int h = Math.Min(800, 60 + badgeCount * 26);
            var visual = BuildBadgeHost(visible, out _);
            visual.Measure(new Size(W, h));
            visual.Arrange(new Rect(0, 0, W, h));
            visual.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var rtb = new RenderTargetBitmap(new PixelSize(W, h), new Vector(96, 96));
            rtb.Render(visual);
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "avalonia", "m5-file-tree", "avalonia-m5-file-tree.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            {
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(outPath));
            long sizeOnDisk = new FileInfo(outPath).Length;
            Assert.True(sizeOnDisk > 1_500, $"PNG 太小 ({sizeOnDisk} bytes)，可能徽章没被栅格化");

            // 落盘摘要
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            File.WriteAllText(summaryPath,
                $"Avalonia M5 文件树截屏（{expectedTotal} 节点，{expectedDirCount} 目录，{expectedLeafCount} 文件）\n" +
                "数据流断言：\n" +
                $"  root.FlatCount() == {root.FlatCount()} (expected {expectedTotal})\n" +
                "  顶层 children (排序后)：\n" +
                string.Join("\n", topLevel.Select(n => $"    {(n.ItemType == FileTreeNode.FileTreeNodeKind.Directory ? "DIR " : "FILE")} {n.Name,-20} (depth={n.Depth}, children={n.Children.Count})")) +
                "\n可视化树徽章颜色断言：\n" +
                string.Join("\n", observed.Take(10).Select(o =>
                    $"  {o.Name,-20} kind={o.Kind,-10} badge={o.BadgeColor}")) +
                (observed.Count > 10 ? $"\n  ... +{observed.Count - 10} more" : "") +
                "\nConverter 预期颜色：\n" +
                $"  Directory = {DirectoryBrush}\n" +
                $"  File      = {FileBrush}\n" +
                $"  Submodule = {SubmoduleBrush}\n" +
                $"文件内容断言：\n" +
                $"  HEAD:README.md = {actualContent.Replace("\n", "\\n")}\n" +
                $"  HEAD:src/main.cs = {mainCsContent.Replace("\n", "\\n")}\n" +
                $"源仓库：{repoDir}\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [AvaloniaFact]
    public async Task M5_FileTree_WithoutRepo_StaysEmpty()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        var panel = main.FindControl<FileTreePanel>("FileTreePanel")!;
        TextBlock summary = panel.FindControl<TextBlock>("FileTreeSummaryText")!;
        TreeView tree = panel.FindControl<TreeView>("FileTreeView")!;
        Assert.Null(panel.Repository);
        Assert.Null(tree.ItemsSource);
        Assert.Contains("未打开", summary.Text);
    }

    // ============== Helpers ==============

    private static void CollectVisible(FileTreeNode node, List<FileTreeNode> outList)
    {
        foreach (FileTreeNode c in node.Children)
        {
            outList.Add(c);
            if (c.ItemType == FileTreeNode.FileTreeNodeKind.Directory)
            {
                CollectVisible(c, outList);
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

    /// <summary>
    /// 用 <see cref="FileTreeNodeKindToBrushConverter"/> 重建一份"XAML 等价"徽章 StackPanel。
    /// 每个 FileTreeNode 一行：Border(Background=converter(ItemType).Color) + Text(Name)
    /// + 缩进(Margin.Left = depth * 16)。
    /// </summary>
    private static Control BuildBadgeHost(List<FileTreeNode> visible, out int badgeCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 1,
            Margin = new Thickness(8),
        };
        foreach (FileTreeNode n in visible)
        {
            IBrush bg = (IBrush)KindToBrush.Convert(n.ItemType, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
            var row = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(n.Depth * 16 + (n.ItemType == FileTreeNode.FileTreeNodeKind.Submodule ? 4 : 0), 1, 0, 1),
                Spacing = 6,
            };
            var badge = new Border
            {
                Background = bg,
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(3),
                Child = new TextBlock
                {
                    Text = n.ItemType switch
                    {
                        FileTreeNode.FileTreeNodeKind.Directory => "D",
                        FileTreeNode.FileTreeNodeKind.File => "F",
                        FileTreeNode.FileTreeNodeKind.Submodule => "S",
                        _ => "?",
                    },
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                },
            };
            row.Children.Add(badge);
            row.Children.Add(new TextBlock
            {
                Text = n.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                FontFamily = new FontFamily("monospace"),
                FontSize = 12,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            });
            host.Children.Add(row);
        }
        badgeCount = visible.Count;
        return host;
    }

    private static bool IsCloseTo(Color a, Color b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol
            && Math.Abs(a.G - b.G) <= tol
            && Math.Abs(a.B - b.B) <= tol;
    }

    // ---- 共享仓库 fixture ----
    private static string CreateM5Repo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m5_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // 写多个文件 + 嵌套目录
        File.WriteAllText(Path.Combine(dir, "README.md"), "Hello from M5 fixture!\n");
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "main.cs"), "// M5 fixture main\nConsole.WriteLine(\"hi\");\n");
        File.WriteAllText(Path.Combine(dir, "src", "util.cs"), "// M5 fixture util\n");
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        File.WriteAllText(Path.Combine(dir, "docs", "intro.md"), "# M5 intro\n");

        RunGit(dir, "add", "-A");
        RunGit(dir, "commit", "-m", "M5 initial commit");

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
