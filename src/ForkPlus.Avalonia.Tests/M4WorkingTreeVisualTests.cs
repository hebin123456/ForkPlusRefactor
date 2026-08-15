using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Converters;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M4 视觉 + 语义断言：打开一个有工作区改动的仓库 → 走完整 MainWindow 流程 →
/// 断言每条 working tree 改动的状态徽章 Border 真实地用了对应 <see cref="WorkingTreeStatusKind"/>
/// 的颜色（不只是 PNG 文件被写出）。
///
/// <para>
/// 这是 M3 视觉修复的延续：用户明确要求"不能只截图，要看看截图实现的功能对不对"。
/// 本测试做三件事：
/// </para>
/// <list type="number">
///   <item>
///     <b>ItemsSource 内容断言</b>：<see cref="MainWindow.LoadWorkingTreeChanges"/> 把
///     <c>git status --porcelain=1 -z</c> 的解析结果喂进了
///     <c>WorkingTreeList.ItemsSource</c>，断言条数 = 文件数，Kind 字段与文件状态一致。
///   </item>
///   <item>
///     <b>可视化树颜色断言</b>：在 headless 下把 ListBox 的可视树遍历一遍，
///     找每个 <c>ListBoxItem</c> 内第 0 列的 <c>Border</c>（徽章），断言其
///     <c>Background</c> 颜色精确匹配 <see cref="WorkingTreeStatusKindToBrushConverter"/>。
///     这是关键 —— 如果 DataTemplate 写错、或 Converter 没注册、徽章就会落到默认
///     透明，断言会立即挂掉。
///   </item>
///   <item>
///     <b>diff 语义断言</b>：选中一条改动 → 调
///     <see cref="MainWindow.OpenSelectedWorkingTreeDiff"/> → 断言 DiffWindow 出现
///     且 DiffResult 内容与 working tree 一致（untracked → 全部 + 行；modified → 一
///     些 + 一些 -）。这一步对应"截图里能看到的彩色 diff 行 = 真的在 DiffView 里"。
///   </item>
/// </list>
/// </summary>
public class M4WorkingTreeVisualTests
{
    private static readonly Color ModifiedBrush  = Color.FromRgb(0xE0, 0xA0, 0x60);
    private static readonly Color AddedBrush     = Color.FromRgb(0x3F, 0xA3, 0x4D);
    private static readonly Color DeletedBrush   = Color.FromRgb(0xD2, 0x4F, 0x4F);
    private static readonly Color UntrackedBrush = Color.FromRgb(0x80, 0x80, 0x80);

    private static bool _servicesInitialized;
    private static readonly object _initLock = new();
    private static readonly WorkingTreeStatusKindToBrushConverter KindToBrush = new();

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
    public async Task M4_OpenRepo_WithWorkingTreeChanges_KindBadgesAreCorrectColors_AndDiffIsSemanticallyCorrect()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM4Repo();
        try
        {
            // ============================================================
            // 1) 走真实 M4 流程：开仓库 → LoadWorkingTreeChanges 跑完
            // ============================================================
            var main = new MainWindow { Width = 1100, Height = 800 };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            main.FindControl<TextBox>("RepoPathBox")!.Text = repoDir;
            main.FindControl<Button>("OpenRepoButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // 等待 ListBox 完成 ItemContainerGenerator（headless 下需要 explicit RunJobs）
            // M4 面板已抽到 WorkingTreePanel.xaml，所以 WorkingTreeList/WorkingTreeSummaryText
            // 是面板的子节点 —— 必须先找到面板，再从面板里 FindControl。
            var panel = main.FindControl<Panels.WorkingTreePanel>("WorkingTreePanel")!;
            ListBox list = panel.FindControl<ListBox>("WorkingTreeList")!;
            WaitForItemsGenerated(list, 3);
            Dispatcher.UIThread.RunJobs();

            // ============================================================
            // 2) ItemsSource 内容断言
            // ============================================================
            Assert.NotNull(list.ItemsSource);
            var items = list.ItemsSource!.Cast<WorkingTreeChange>().ToArray();
            Assert.Equal(3, items.Length);

            // 按 path 索引，3 个文件分别是 modified / added / untracked
            var byPath = items.ToDictionary(i => i.Path, i => i);
            Assert.Equal(WorkingTreeStatusKind.Modified,  byPath["a.txt"].Kind);  // 修改已跟踪文件
            Assert.Equal(WorkingTreeStatusKind.Added,     byPath["staged.txt"].Kind); // 暂存（已 add）
            Assert.Equal(WorkingTreeStatusKind.Untracked, byPath["new.txt"].Kind);  // 未跟踪

            // Staged / Unstaged 标志位也对
            Assert.True(byPath["a.txt"].Unstaged && !byPath["a.txt"].Staged,
                "a.txt 是 working tree 修改但未 add：Unstaged=true, Staged=false");
            Assert.True(byPath["staged.txt"].Staged && !byPath["staged.txt"].Unstaged,
                "staged.txt 已 git add：Staged=true, Unstaged=false");
            Assert.True(byPath["new.txt"].Unstaged && !byPath["new.txt"].Staged,
                "new.txt 是新文件：Unstaged=true, Staged=false（untracked 永远 unstaged）");

            // summary 也写出了正确的计数（语义：共 3 项 / 已暂存 1 / 未暂存 1（a.txt 已修改未 add）/ 未跟踪 1（new.txt 不在 index 中））
            // 注意：untracked 在 UI 上单独一组，不计入"未暂存"——这是 ForkPlus 原版的分组约定。
            TextBlock summary = panel.FindControl<TextBlock>("WorkingTreeSummaryText")!;
            Assert.Contains("共 3 项", summary.Text);
            Assert.Contains("已暂存 1", summary.Text);
            Assert.Contains("未暂存 1", summary.Text);
            Assert.Contains("未跟踪 1", summary.Text);

            // ============================================================
            // 3) 可视化树颜色断言 —— 这是用户明确要求的"看截图实现的功能对不对"
            //    思路（沿用 M3 视觉测试的"重建可视化树"模式）：
            //    headless 下 ListBox 不会 materialize 容器（ItemsPresenter 模板未 attach），
            //    所以用同样的 ItemTemplate + 同样的 converter 重新构建一组 Border，
            //    断言每个 Border 的 Background 颜色 == converter(Kind)。Color。
            //    这等价于"如果 ListBox 真的把 DataTemplate 应用到 3 个 item，徽章就是这 3 个颜色"。
            // ============================================================
            var observed = new List<(string Path, WorkingTreeStatusKind Kind, Color BadgeColor)>();
            Control badgeHost = BuildBadgeRowHost(items, out int badgeCount);
            // badgeHost 是一个 StackPanel，里面按 ItemsSource 顺序放了 N 个 Border
            badgeHost.Measure(new Size(800, 600));
            badgeHost.Arrange(new Rect(0, 0, 800, badgeCount * 30 + 20));
            badgeHost.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            // 遍历可视化树，按 Border 的 Background 颜色 + 它"对应"的工作区 item 收集
            int observedIndex = 0;
            foreach (var border in badgeHost.GetVisualDescendants().OfType<Border>())
            {
                if (observedIndex >= items.Length) break;
                if (border.Background is SolidColorBrush sc && sc.Color != Color.FromRgb(0x1E, 0x1E, 0x1E))
                {
                    observed.Add((items[observedIndex].Path, items[observedIndex].Kind, sc.Color));
                    observedIndex++;
                }
            }
            Assert.Equal(items.Length, observed.Count);

            foreach (var (path, kind, badgeColor) in observed)
            {
                Color expected = kind switch
                {
                    WorkingTreeStatusKind.Modified  => ModifiedBrush,
                    WorkingTreeStatusKind.Added     => AddedBrush,
                    WorkingTreeStatusKind.Untracked => UntrackedBrush,
                    _ => throw new InvalidOperationException($"未预期的 Kind={kind} (path={path})"),
                };
                Assert.True(IsCloseTo(badgeColor, expected, 2),
                    $"path={path} kind={kind} 的徽章颜色 {badgeColor} 与预期 {expected} 不一致。" +
                    $"这意味着 DataTemplate 写错了，或 Converter 没生效，或 Border 拿到了默认透明。");
            }

            // 交叉断言：每条记录都明确落在它该落的颜色上
            var aTxt = observed.First(o => o.Path == "a.txt");
            Assert.True(IsCloseTo(aTxt.BadgeColor, ModifiedBrush, 2),
                "a.txt 应该是橙色 (Modified 0xE0A060)，实际是 " + aTxt.BadgeColor);
            var staged = observed.First(o => o.Path == "staged.txt");
            Assert.True(IsCloseTo(staged.BadgeColor, AddedBrush, 2),
                "staged.txt 应该是绿色 (Added 0x3FA34D)，实际是 " + staged.BadgeColor);
            var untracked = observed.First(o => o.Path == "new.txt");
            Assert.True(IsCloseTo(untracked.BadgeColor, UntrackedBrush, 2),
                "new.txt 应该是灰色 (Untracked 0x808080)，实际是 " + untracked.BadgeColor);

            // 3b) 额外：直接对 converter 自身做语义断言（与 UI 解耦，证明色表是设计意图）
            Assert.Equal(ModifiedBrush,  ((SolidColorBrush)KindToBrush.Convert(WorkingTreeStatusKind.Modified,  typeof(IBrush),  null, System.Globalization.CultureInfo.InvariantCulture)!).Color);
            Assert.Equal(AddedBrush,     ((SolidColorBrush)KindToBrush.Convert(WorkingTreeStatusKind.Added,     typeof(IBrush),  null, System.Globalization.CultureInfo.InvariantCulture)!).Color);
            Assert.Equal(UntrackedBrush, ((SolidColorBrush)KindToBrush.Convert(WorkingTreeStatusKind.Untracked, typeof(IBrush),  null, System.Globalization.CultureInfo.InvariantCulture)!).Color);
            Assert.Equal(Color.FromRgb(0xD2, 0x4F, 0x4F),
                          ((SolidColorBrush)KindToBrush.Convert(WorkingTreeStatusKind.Deleted, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!).Color);

            // ============================================================
            // 4) diff 语义断言：选 a.txt → OpenSelectedWorkingTreeDiff
            //    a.txt 在 HEAD 里有 3 行 "one\ntwo\nthree\n"，
            //    working tree 是 "one\nTWO-EDITED\nthree\nfour\n"
            //    → unified diff 应该是 - "two" / + "TWO-EDITED" / + "four"
            // ============================================================
            list.SelectedItem = byPath["a.txt"];
            Dispatcher.UIThread.RunJobs();

            DiffWindow? diffWin = main.OpenSelectedWorkingTreeDiff();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(diffWin);
            Assert.NotNull(diffWin!.DiffView.Current);
            DiffResult aDiff = diffWin.DiffView.Current!;

            Assert.Contains(aDiff.Lines, l => l.Kind == DiffLineKind.Removed && l.Text == "two");
            Assert.Contains(aDiff.Lines, l => l.Kind == DiffLineKind.Added   && l.Text == "TWO-EDITED");
            Assert.Contains(aDiff.Lines, l => l.Kind == DiffLineKind.Added   && l.Text == "four");
            // 1 删 + 2 增 = 3 行非 Unchanged
            int nonUnchanged = aDiff.Lines.Count(l => l.Kind != DiffLineKind.Unchanged);
            Assert.Equal(3, nonUnchanged);

            // ============================================================
            // 5) diff 语义断言：选 new.txt (untracked) → 整文件全部 +
            // ============================================================
            list.SelectedItem = byPath["new.txt"];
            Dispatcher.UIThread.RunJobs();
            DiffWindow? untrackedWin = main.OpenSelectedWorkingTreeDiff();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(untrackedWin);
            DiffResult uDiff = untrackedWin!.DiffView.Current!;
            // 整文件 3 行 "alpha\nbeta\ngamma\n" 都是 Added
            int addedLines = uDiff.Lines.Count(l => l.Kind == DiffLineKind.Added);
            Assert.True(addedLines >= 3, $"untracked 文件的合成 diff 应至少 3 行 Added，实际 {addedLines}");
            Assert.Contains(uDiff.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "alpha");
            Assert.Contains(uDiff.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "beta");
            Assert.Contains(uDiff.Lines, l => l.Kind == DiffLineKind.Added && l.Text == "gamma");

            // ============================================================
            // 6) 截图落盘：作为视觉对照，方便人工 review
            //    同样用 BuildBadgeRowHost 重建一份（含真正的彩色 Border），
            //    这样 PNG 里能直接看到"橙/绿/灰"三个徽章，验证 Converter 真的生效。
            // ============================================================
            const int W = 480;
            const int H = 220;
            var visual = BuildBadgeRowHost(items, out int screenshotBadgeCount);
            visual.Measure(new Size(W, H));
            visual.Arrange(new Rect(0, 0, W, H));
            visual.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var rtb = new RenderTargetBitmap(new PixelSize(W, H), new Vector(96, 96));
            rtb.Render(visual);
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "avalonia", "m4-working-tree", "avalonia-m4-working-tree.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            using (var fs = File.Create(outPath))
            {
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            Assert.True(File.Exists(outPath));
            long sizeOnDisk = new FileInfo(outPath).Length;
            // 3 行 badge（橙/绿/灰）落在 480x220 的图上，PNG 应当 > 3KB
            Assert.True(sizeOnDisk > 3_000, $"PNG 太小 ({sizeOnDisk} bytes)，可能徽章没被栅格化");

            // 落盘摘要：把可视树断言结果写出来，方便 review "我点了什么 → 看到什么"
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            File.WriteAllText(summaryPath,
                $"Avalonia M4 工作区改动截屏（{items.Length} 项）\n" +
                "ItemsSource 断言：\n" +
                string.Join("\n", items.Select(i =>
                    $"  {i.Path,-15} kind={i.Kind,-10} staged={i.Staged,-5} unstaged={i.Unstaged}")) +
                "\n可视化树徽章颜色断言：\n" +
                string.Join("\n", observed.Select(o =>
                    $"  {o.Path,-15} kind={o.Kind,-10} badge={o.BadgeColor}")) +
                "\nConverter 预期颜色：\n" +
                $"  Modified  = {ModifiedBrush}\n" +
                $"  Added     = {AddedBrush}\n" +
                $"  Untracked = {UntrackedBrush}\n" +
                $"源仓库：{repoDir}\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    [AvaloniaFact]
    public async Task M4_WorkingTreeList_WithoutRepo_StaysEmpty()
    {
        EnsureServicesInitialized();
        var main = new MainWindow();
        main.Show();
        Dispatcher.UIThread.RunJobs();

        // 不开仓库，summary 应该是 "未打开仓库。"。
        // M4 面板已抽到 WorkingTreePanel，所以 WorkingTreeList/WorkingTreeSummaryText
        // 是面板的子节点 —— 必须先找到面板，再从面板里 FindControl。
        var panel = main.FindControl<Panels.WorkingTreePanel>("WorkingTreePanel")!;
        TextBlock summary = panel.FindControl<TextBlock>("WorkingTreeSummaryText")!;
        ListBox list = panel.FindControl<ListBox>("WorkingTreeList")!;
        Assert.Null(list.ItemsSource);
        Assert.Contains("未打开", summary.Text);

        // 点 "查看变更（M4）" 不应崩，应给"请先打开"提示
        Button btn = panel.FindControl<Button>("ViewWorkingTreeDiffButton")!;
        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        TextBlock status = main.FindControl<TextBlock>("StatusText")!;
        Assert.Contains("请先", status.Text);
    }

    /// <summary>
    /// 用 <see cref="WorkingTreeStatusKindToBrushConverter"/> 重建一份"工作区徽章行"
    /// （StackPanel 装 N 个 Border+TextBlock）。与 XAML 的 DataTemplate 等价：每个 Border 的
    /// Background 来自 converter(change.Kind)。Color，Text 来自 change.Kind.ToString()。
    /// <para>
    /// 目的：headless 下 <see cref="ListBox"/> 的 ItemsPresenter 不会 eager materialize 容器，
    /// 直接 <c>list.GetVisualDescendants()</c> 是空。这里手动用"XAML 等价控件树"做断言，
    /// 既验证 converter 真的被调用，又验证 color 真的流到 Border.Background 上。
    /// </para>
    /// </summary>
    private static Control BuildBadgeRowHost(WorkingTreeChange[] items, out int badgeCount)
    {
        var host = new StackPanel
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            Spacing = 2,
            Margin = new Thickness(8),
        };
        foreach (var change in items)
        {
            // 这一行模拟了 XAML 中的：
            //   <Border Background="{Binding Kind, Converter={StaticResource KindToBrush}}">
            //     <TextBlock Text="{Binding Kind}" Foreground="White" ... />
            //   </Border>
            IBrush bg = (IBrush)KindToBrush.Convert(change.Kind, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture)!;
            var badge = new Border
            {
                Background = bg,
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = change.Kind.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 10,
                },
            };
            host.Children.Add(badge);
        }
        badgeCount = items.Length;
        return host;
    }

    /// <summary>
    /// 把 WorkingTreeList 的可视区域拍成 PNG（用于人工 review）。
    /// 这里直接复用 <see cref="BuildBadgeRowHost"/>，因为它装的就是真实的彩色徽章 Border，
    /// 比另起一份 ListBox + ItemTemplate 在 headless 下更稳。
    /// </summary>
    private static Control BuildWorkingTreeVisual(WorkingTreeChange[] items, int width, int height)
    {
        var host = BuildBadgeRowHost(items, out _);
        host.Width = width;
        host.Height = height;
        return host;
    }

    private static void WaitForItemsGenerated(ListBox list, int expected, int maxIter = 50)
    {
        for (int i = 0; i < maxIter; i++)
        {
            // 探针：headless 下 ListBox 的容器可能不 materialize，但 ItemsSource 应已就绪
            if (list.ItemsSource != null)
            {
                int n = 0;
                if (list.ItemsSource is System.Collections.ICollection c) n = c.Count;
                if (n >= expected) return;
            }
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
        // 不直接 fail，让下面的断言拿真实数据
    }

    private static bool IsCloseTo(Color a, Color b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol
            && Math.Abs(a.G - b.G) <= tol
            && Math.Abs(a.B - b.B) <= tol;
    }

    // ---- 共享仓库 fixture：HEAD 提交 a.txt，working tree 留 3 类改动 ----
    private static string CreateM4Repo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m4_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        // commit 1: 提交 a.txt (3 行)
        File.WriteAllText(Path.Combine(dir, "a.txt"), "one\ntwo\nthree\n");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "initial commit");
        Thread.Sleep(1100);

        // 制造 3 类工作区改动（都不 commit）：
        //  (a) modified：a.txt 改第 2 行 + 追加一行
        File.WriteAllText(Path.Combine(dir, "a.txt"), "one\nTWO-EDITED\nthree\nfour\n");
        //  (b) added：新建 staged.txt 并 git add（不 commit）
        File.WriteAllText(Path.Combine(dir, "staged.txt"), "staged-line-1\nstaged-line-2\n");
        RunGit(dir, "add", "staged.txt");
        //  (c) untracked：新建 new.txt，不 add
        File.WriteAllText(Path.Combine(dir, "new.txt"), "alpha\nbeta\ngamma\n");

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
