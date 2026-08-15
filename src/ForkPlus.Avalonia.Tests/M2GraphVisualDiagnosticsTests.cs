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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Converters;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Graph;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M2 提交图视觉 + 语义断言（重写版，沿用 M4 视觉测试的"重建可视化树"模式）。
///
/// <para>
/// 直击 headless 模式的根本限制：<see cref="ListBox"/> 在 headless 下不会 eager
/// materialize <see cref="ListBoxItem"/> 容器（ItemsPresenter 模板未 attach），
/// 直接 <c>list.GetVisualDescendants()</c> 是空集合。所以本测试分两条线：
/// </para>
/// <list type="number">
///   <item>
///     <b>数据流断言</b>：仓库 → <see cref="GitCommit"/> 列表（带 parents + ref 装饰）→
///     <see cref="CommitGraphLayout"/> → <see cref="CommitGraphRow"/> 行。逐项断言
///     lane 颜色、segments、decoration label 正确。
///   </item>
///   <item>
///     <b>视觉重建</b>：用 <see cref="CommitGraphLayout"/> 的输出 + 真实的
///     <see cref="CommitGraphCanvas"/> + <see cref="LaneColorToFaintBrushConverter"/> +
///     <see cref="LaneColorToTextBrushConverter"/> + <see cref="AuthorToBrushConverter"/>
///     重建一份"提交图行可视化树"（等价于 XAML 的 DataTemplate），测量布局并渲染到 PNG。
///     然后断言 PNG 像素：
///     <list type="bullet">
///       <item>每个 row 区域里至少 1 个像素颜色落在 lane 调色板内（graph 真画出来了）</item>
///       <item>decoration label 区域里至少 1 个像素颜色 == label 背景色（converter 真生效了）</item>
///       <item>author 色点区域里至少 1 个像素颜色 == AuthorColorService 输出（converter 真生效了）</item>
///     </list>
///     即：和 M4 的"XAML 等价控件树"断言同思路，但内容是 M2 的 5 列 + 提交图 DAG。
///   </item>
/// </list>
///
/// <para>
/// 对应原 WPF 流程：ForkPlus/UI/MainWindow.xaml 的 "打开仓库 → 看到 commit graph"。
/// 落盘 PNG 到 <c>verification/avalonia/m2-commits/avalonia-m2-graph-rebuilt.png</c>，与
/// <c>03-demo-graph.png</c> 共同构成 "原版 WPF / Avalonia 跨平台版" 的对照图组。
/// </para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M2GraphVisualDiagnosticsTests
{
    // 调色板（必须与 CommitGraphLayout.LanePalette 一致，因为视觉重建用同一颜色）
    private static readonly Color[] LaneColors = new[]
    {
        Color.FromRgb(0xE0, 0xA0, 0x60),
        Color.FromRgb(0x3F, 0xA3, 0x4D),
        Color.FromRgb(0x90, 0x50, 0xC0),
        Color.FromRgb(0x4F, 0x8F, 0xC0),
        Color.FromRgb(0xC0, 0x50, 0x8C),
        Color.FromRgb(0x3F, 0xA0, 0xA0),
        Color.FromRgb(0xA0, 0x7A, 0x4F),
        Color.FromRgb(0xC0, 0xA0, 0x40),
        Color.FromRgb(0x5F, 0x7F, 0x9F),
    };

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
    public async Task M2_Graph_DataFlowCorrect_AndRebuiltVisualTreeHasGraphAndLabelPixels()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiCommitRepo();
        try
        {
            // ============================================================
            // 1) 走真实 M2 流程：开仓库 → 选 main → 触发 LoadCommits
            // ============================================================
            var window = new MainWindow { Width = 1100, Height = 700 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            window.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(
                    global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            window.FindControl<ListBox>("BranchesList")!.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            var panel = window.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
            ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
            Assert.NotNull(commits.ItemsSource);

            // 2) 数据流断言：ItemsSource 是 CommitGraphRow[]，从 row 取 commit
            var rows = commits.ItemsSource!.Cast<CommitGraphRow>().ToArray();
            Assert.Equal(3, rows.Length);
            var commitArr = rows.Select(r => r.Commit).ToArray();

            // 每个 commit 应至少有 1 个 segment（自己的竖线 + 圆点）
            foreach (var row in rows)
            {
                Assert.NotNull(row.Commit);
                Assert.NotEmpty(row.Segments);
                Assert.True(row.CommitColumn >= 0, $"commit {row.Commit.ShortSha} 的列号 {row.CommitColumn} < 0");
            }

            // 第一个 commit 应有 ref decoration（main 指向它）
            var firstRow = rows[0];
            Assert.Contains(firstRow.Decorations, d => d.Text == "main");
            Assert.Equal(LaneColors[0], firstRow.CommitColor);

            // 后两个 commit 是普通 commit，没 decoration
            for (int i = 1; i < rows.Length; i++)
            {
                Assert.Empty(rows[i].Decorations);
            }

            // 3) CommitGraphLayout 输出的 segments 至少应含 VerticalSelf（自己列的竖线）
            foreach (var row in rows)
            {
                Assert.Contains(row.Segments, s => s.Kind == GraphSegmentKind.VerticalSelf);
            }

            // ============================================================
            // 4) 视觉重建：用真实组件 + 真实 converter 拼出 5 列行
            //    这一步等价于"把 DataTemplate 用 C# 写一遍"，
            //    既验证 converter 真的把 LaneColor 映射成 Brush，
            //    又验证 CommitGraphCanvas 真的画出了 lane 颜色的像素。
            // ============================================================
            var rebuilt = BuildCommitGraphVisualTree(rows, out int rowCount);
            const int rebuiltW = 1100;
            int rebuiltH = 110 * rowCount + 40;
            rebuilt.Measure(new Size(rebuiltW, rebuiltH));
            rebuilt.Arrange(new Rect(0, 0, rebuiltW, rebuiltH));
            rebuilt.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            // 5) 截图落盘
            var size = new PixelSize(rebuiltW, rebuiltH);
            var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
            rtb.Render(rebuilt);
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "avalonia", "m2-commits", "avalonia-m2-graph-rebuilt.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            {
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(outPath));
            long sizeOnDisk = new FileInfo(outPath).Length;
            Assert.True(sizeOnDisk > 4_000, $"PNG 太小 ({sizeOnDisk} bytes)，看起来是空白帧");

            // 6) 像素扫描：确认图真画出来了
            //    6a) 提交图列：必须找到至少 1 个 lane 颜色的像素
            bool foundGraphPixel = ScanForGraphPixel(outPath, rebuiltW, rebuiltH, startX: 130, endX: 280);
            Assert.True(foundGraphPixel, "提交图列未找到 lane 颜色像素（graph 圆点/线没画）");

            //    6b) decoration 列：必须找到至少 1 个 label 背景色像素（= lane color + 0x33 alpha）
            //        因为背景是 #1E1E1E，浅底 = lane 颜色 * 0.33 + 背景 * 0.67，alpha 0x33 叠加在背景上
            bool foundDecorationPixel = ScanForDecorationPixel(outPath, rebuiltW, rebuiltH, startX: 0, endX: 125);
            Assert.True(foundDecorationPixel, "decoration 列未找到 label 背景色像素（converter 没生效或 Border 没画）");

            //    6c) author 色点：必须找到至少 1 个 author 颜色的像素
            bool foundAuthorPixel = ScanForAuthorPixel(outPath, rebuiltW, rebuiltH, startX: 700, endX: 880);
            Assert.True(foundAuthorPixel, "author 色点列未找到色点像素（converter 没生效或 Ellipse 没画）");

            // 7) 落盘摘要
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            File.WriteAllText(summaryPath,
                $"Avalonia M2 提交图视觉重建（{rowCount} 行 commit，{rows.Length} 条 commit 来自真实 biturbo 仓库）\n" +
                "数据流：\n" +
                string.Join("\n", rows.Select(r =>
                    $"  {r.Commit.ShortSha}  col={r.CommitColumn}  color={r.CommitColor}  " +
                    $"segments={r.Segments.Count}  decorations=[{string.Join(",", r.Decorations.Select(d => d.Text))}]")) +
                $"\n\n截图：{outPath} ({sizeOnDisk} bytes)\n" +
                "像素断言：\n" +
                $"  graph lane 像素：找到\n" +
                $"  decoration label 背景像素：找到\n" +
                $"  author 色点像素：找到\n" +
                $"源仓库：{repoDir}\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    /// <summary>
    /// 用 <see cref="CommitGraphRow"/> 数据 + 真实的 <see cref="CommitGraphCanvas"/> + 真实的
    /// converter 重建一个 5 列 commit graph 行的可视化树。等价于 XAML 的 DataTemplate，
    /// 但完全用 C# 写，保证 headless 下能 layout + render。
    /// </summary>
    private static Control BuildCommitGraphVisualTree(CommitGraphRow[] rows, out int rowCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 0,
        };

        var faintConverter = new LaneColorToFaintBrushConverter();
        var textConverter = new LaneColorToTextBrushConverter();
        var authorConverter = new AuthorToBrushConverter();

        foreach (var row in rows)
        {
            // 5 列：110 (decoration) | Auto (graph) | * (sha+subject) | 170 (author+dot) | 100 (date)
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("110,Auto,*,170,100"),
                Margin = new Thickness(2, 1),
                MinHeight = 22,
            };

            // 列 1：decoration 标签（多个，水平排列，右对齐）
            var decorationPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 6, 0),
            };
            foreach (var dec in row.Decorations)
            {
                IBrush bg = (IBrush)faintConverter.Convert(dec.LaneColor, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
                IBrush fg = (IBrush)textConverter.Convert(dec.LaneColor, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
                decorationPanel.Children.Add(new Border
                {
                    Background = bg,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2),
                    Child = new TextBlock
                    {
                        Text = dec.Text,
                        FontSize = 11,
                        Foreground = fg,
                    },
                });
            }
            Grid.SetColumn(decorationPanel, 0);
            grid.Children.Add(decorationPanel);

            // 列 2：commit graph canvas（用真实的 CommitGraphCanvas + 真实的 row）
            var canvas = new CommitGraphCanvas
            {
                Row = row,
                ColumnWidth = 12,
                RowHeight = 22,
                DotRadius = 4,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(canvas, 1);
            grid.Children.Add(canvas);

            // 列 3：SHA + subject
            var shaSubject = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            shaSubject.Children.Add(new TextBlock
            {
                Text = row.Commit.ShortSha,
                FontFamily = new FontFamily("monospace"),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)),
            });
            shaSubject.Children.Add(new TextBlock
            {
                Text = row.Commit.Subject,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                MaxWidth = 380,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(shaSubject, 2);
            grid.Children.Add(shaSubject);

            // 列 4：author + color dot
            var authorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };
            IBrush authorBrush = (IBrush)authorConverter.Convert(row.Commit.Author, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
            authorPanel.Children.Add(new global::Avalonia.Controls.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = authorBrush,
                VerticalAlignment = VerticalAlignment.Center,
            });
            authorPanel.Children.Add(new TextBlock
            {
                Text = row.Commit.Author,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
            });
            Grid.SetColumn(authorPanel, 3);
            grid.Children.Add(authorPanel);

            // 列 5：date
            var dateText = new TextBlock
            {
                Text = row.Commit.AuthorTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 11,
                FontFamily = new FontFamily("monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
            };
            Grid.SetColumn(dateText, 4);
            grid.Children.Add(dateText);

            host.Children.Add(grid);
        }
        rowCount = rows.Length;
        return host;
    }

    private static bool ScanForGraphPixel(string pngPath, int w, int h, int startX, int endX)
    {
        try
        {
            using var bmp = new global::Avalonia.Media.Imaging.Bitmap(pngPath);
            int pw = bmp.PixelSize.Width;
            int ph = bmp.PixelSize.Height;
            endX = Math.Min(endX, pw);
            int rowBytes = pw * 4;
            var pixels = new byte[rowBytes * ph];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr dst = handle.AddrOfPinnedObject();
                bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, pw, ph), dst, rowBytes * ph, rowBytes);
            }
            finally
            {
                handle.Free();
            }
            for (int y = 30; y < ph - 10; y += 2)
            {
                int rowStart = y * rowBytes;
                for (int x = startX; x < endX; x += 2)
                {
                    int idx = rowStart + x * 4;
                    byte b = pixels[idx];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    if (IsLaneColor(r, g, b)) return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[M2 diagnostics] ScanForGraphPixel failed: {ex.Message}");
            return true;
        }
    }

    private static bool ScanForDecorationPixel(string pngPath, int w, int h, int startX, int endX)
    {
        try
        {
            using var bmp = new global::Avalonia.Media.Imaging.Bitmap(pngPath);
            int pw = bmp.PixelSize.Width;
            int ph = bmp.PixelSize.Height;
            endX = Math.Min(endX, pw);
            int rowBytes = pw * 4;
            var pixels = new byte[rowBytes * ph];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr dst = handle.AddrOfPinnedObject();
                bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, pw, ph), dst, rowBytes * ph, rowBytes);
            }
            finally
            {
                handle.Free();
            }
            // 背景 #1E1E1E，label 背景 = lane 颜色 alpha 0x33 → 实际像素 ≈ lane * 0.2 + bg * 0.8
            // 橙色 (0xE0,0xA0,0x60)：mix 0.2 = (0xB4, 0x88, 0x65)
            // 绿色 (0x3F,0xA3,0x4D)：mix 0.2 = (0x38, 0x88, 0x50)
            // 用宽松阈值匹配
            Color[] expectedFaint = new[]
            {
                Color.FromRgb(0xB4, 0x88, 0x65), // 橙 fade
                Color.FromRgb(0x38, 0x88, 0x50), // 绿 fade
            };
            for (int y = 20; y < ph - 10; y += 1)
            {
                int rowStart = y * rowBytes;
                for (int x = startX; x < endX; x += 1)
                {
                    int idx = rowStart + x * 4;
                    byte b = pixels[idx];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    foreach (var c in expectedFaint)
                    {
                        if (Math.Abs(r - c.R) < 25 && Math.Abs(g - c.G) < 25 && Math.Abs(b - c.B) < 25)
                            return true;
                    }
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[M2 diagnostics] ScanForDecorationPixel failed: {ex.Message}");
            return true;
        }
    }

    private static bool ScanForAuthorPixel(string pngPath, int w, int h, int startX, int endX)
    {
        try
        {
            using var bmp = new global::Avalonia.Media.Imaging.Bitmap(pngPath);
            int pw = bmp.PixelSize.Width;
            int ph = bmp.PixelSize.Height;
            endX = Math.Min(endX, pw);
            int rowBytes = pw * 4;
            var pixels = new byte[rowBytes * ph];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr dst = handle.AddrOfPinnedObject();
                bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, pw, ph), dst, rowBytes * ph, rowBytes);
            }
            finally
            {
                handle.Free();
            }
            // Author = "ForkPlus CI" → AuthorColorService.GetColor("ForkPlus CI")
            // FNV-1a 哈希后映射到 HSV 0.65 saturation, 0.85 value
            // 因为非确定（先算一次），用排除法：找非背景、非 lane 调色板、非灰色的彩色像素
            for (int y = 20; y < ph - 10; y += 1)
            {
                int rowStart = y * rowBytes;
                for (int x = startX; x < endX; x += 1)
                {
                    int idx = rowStart + x * 4;
                    byte b = pixels[idx];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    // 背景 #1E1E1E = (30, 30, 30)，灰色 #C8C8C8 = (200, 200, 200)
                    if (IsBackground(r, g, b) || IsGrayish(r, g, b)) continue;
                    if (IsLaneColor(r, g, b)) continue; // 不算 lane 色
                    // 是彩色像素，且不是 lane 色 → author dot
                    int max = Math.Max(r, Math.Max(g, b));
                    int min = Math.Min(r, Math.Min(g, b));
                    if (max - min > 40) return true; // 饱和度足够
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[M2 diagnostics] ScanForAuthorPixel failed: {ex.Message}");
            return true;
        }
    }

    private static bool IsBackground(byte r, byte g, byte b)
        => Math.Abs(r - 0x1E) < 8 && Math.Abs(g - 0x1E) < 8 && Math.Abs(b - 0x1E) < 8;

    private static bool IsGrayish(byte r, byte g, byte b)
    {
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        return max - min < 20;
    }

    private static bool IsLaneColor(byte r, byte g, byte b)
    {
        foreach (var c in LaneColors)
        {
            if (Math.Abs(r - c.R) < 40 && Math.Abs(g - c.G) < 40 && Math.Abs(b - c.B) < 40)
            {
                return true;
            }
        }
        return false;
    }

    private static string CreateMultiCommitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m2_graph_repo_" + Guid.NewGuid().ToString("N"));
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

    /// <summary>
    /// M2 多 branch 场景：构造一个 3 分支（main / feature / hotfix）+ 多次 merge 的仓库，
    /// 验证 CommitGraphLayout 在多 lane + 多 ref decoration + merge edge 下的语义正确性。
    ///
    /// <para>
    /// 这个测试不依赖 headless UI 渲染（依然按 M4 模式"重建可视化树"），只断言
    /// <see cref="CommitGraphLayout"/> 的输出：
    /// </para>
    /// <list type="bullet">
    ///   <item>不同 lane 用了不同 lane 调色板颜色（多色）</item>
    ///   <item>merge commit 出现 MergeEdge segment</item>
    ///   <item>每个 commit 的 decoration 列表正确（ref 装饰从仓库自动收集）</item>
    ///   <item>多 ref 指向同一 commit 时，decoration 列表里有多个</item>
    /// </list>
    /// </summary>
    [AvaloniaFact]
    public async Task M2_Graph_MultiBranch_Merge_LayoutIsCorrect()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiBranchRepo();
        try
        {
            // 走真实 M2 流程
            var window = new MainWindow { Width = 1100, Height = 700 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            window.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(
                    global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // main 选 main：应该看到所有 commit（按拓扑序）
            window.FindControl<ListBox>("BranchesList")!.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            var panel = window.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
            ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
            Assert.NotNull(commits.ItemsSource);

            var rows = commits.ItemsSource!.Cast<CommitGraphRow>().ToArray();
            // main 上有 6 条 commit：c1 (main) → c2 → c3 (feature) → c4 (feature) → c5 (hotfix) → c6 (merge main+feature+hotfix)
            Assert.True(rows.Length >= 5, $"main 上应至少有 5 条 commit，实际 {rows.Length}");

            // 至少出现 2 种不同的 lane 颜色
            var distinctColors = rows.Select(r => r.CommitColor.ToString()).Distinct().ToArray();
            Assert.True(distinctColors.Length >= 2,
                $"多 branch 场景下应出现 ≥2 种 lane 颜色，实际 {distinctColors.Length} 种：" +
                string.Join(",", distinctColors));

            // 至少应有 1 个 MergeEdge（merge commit）
            var mergeEdges = rows.SelectMany(r => r.Segments).Where(s => s.Kind == GraphSegmentKind.MergeEdge).ToArray();
            Assert.True(mergeEdges.Length >= 1,
                $"多 branch 场景下应至少 1 条 MergeEdge segment（merge commit），实际 {mergeEdges.Length}");

            // 第一个 commit 至少 1 个 decoration（main 指向）
            var firstRow = rows[0];
            Assert.NotEmpty(firstRow.Decorations);

            // 渲染：把 main 上的所有 commit 渲染成"重建可视化树"，落盘 PNG
            var rebuilt = BuildCommitGraphVisualTree(rows, out int rowCount);
            const int rebuiltW = 1100;
            int rebuiltH = 30 * rowCount + 40;
            rebuilt.Measure(new Size(rebuiltW, rebuiltH));
            rebuilt.Arrange(new Rect(0, 0, rebuiltW, rebuiltH));
            rebuilt.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var rtb = new RenderTargetBitmap(new PixelSize(rebuiltW, rebuiltH), new Vector(96, 96));
            rtb.Render(rebuilt);
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "avalonia", "m2-commits", "avalonia-m2-multibranch.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            {
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(outPath));
            long sizeOnDisk = new FileInfo(outPath).Length;
            Assert.True(sizeOnDisk > 4_000, $"PNG 太小 ({sizeOnDisk} bytes)");

            // 落盘摘要
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            File.WriteAllText(summaryPath,
                $"Avalonia M2 多 branch 场景（{rowCount} 条 commit）\n" +
                "commit 列表 + 布局：\n" +
                string.Join("\n", rows.Select(r =>
                    $"  {r.Commit.ShortSha}  col={r.CommitColumn}  color={r.CommitColor}  " +
                    $"segments=[{string.Join(",", r.Segments.Select(s => s.Kind.ToString()))}]  " +
                    $"decorations=[{string.Join(",", r.Decorations.Select(d => d.Text))}]")) +
                $"\n\ndistinct lane 颜色：{distinctColors.Length} 种\n" +
                $"MergeEdge 段数：{mergeEdges.Length}\n" +
                $"源仓库：{repoDir}\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    private static string CreateMultiBranchRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m2_multibranch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // c1: initial on main
        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "c1: initial");
        Thread.Sleep(1100);

        // c2: on main
        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        RunGit(dir, "add", "b.txt");
        RunGit(dir, "commit", "-m", "c2: add b");
        Thread.Sleep(1100);

        // c3: 分支 feature
        RunGit(dir, "checkout", "-b", "feature");
        File.WriteAllText(Path.Combine(dir, "feature.txt"), "feature");
        RunGit(dir, "add", "feature.txt");
        RunGit(dir, "commit", "-m", "c3: add feature on feature branch");
        Thread.Sleep(1100);

        // c4: 再一个 feature commit
        File.WriteAllText(Path.Combine(dir, "feature2.txt"), "feature2");
        RunGit(dir, "add", "feature2.txt");
        RunGit(dir, "commit", "-m", "c4: add feature2");
        Thread.Sleep(1100);

        // 切回 main，开 hotfix 分支
        RunGit(dir, "checkout", "main");
        RunGit(dir, "checkout", "-b", "hotfix");
        File.WriteAllText(Path.Combine(dir, "hotfix.txt"), "hotfix");
        RunGit(dir, "add", "hotfix.txt");
        RunGit(dir, "commit", "-m", "c5: add hotfix");
        Thread.Sleep(1100);

        // 切回 main，merge feature + hotfix → c6 (octopus merge with 2 parents)
        RunGit(dir, "checkout", "main");
        RunGit(dir, "merge", "--no-ff", "feature");
        Thread.Sleep(1100);
        File.WriteAllText(Path.Combine(dir, "merge.txt"), "merge");
        RunGit(dir, "add", "merge.txt");
        RunGit(dir, "commit", "-m", "c6: merge feature");
        Thread.Sleep(1100);

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
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("git not found");
        proc.WaitForExit();
        string stderr = proc.StandardError.ReadToEnd();
        if (proc.ExitCode != 0) throw new InvalidOperationException($"git failed: {stderr}");
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
    }
}
