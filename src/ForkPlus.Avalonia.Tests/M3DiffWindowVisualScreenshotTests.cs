using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
/// M3 视觉验证：Avalonia Headless 跑 <see cref="DiffWindow"/> → 用
/// <c>RenderTargetBitmap</c> 栅格化当前布局 → 落盘 PNG 到
/// <c>verification/v3.9.0/avalonia-m3-diff.png</c>，与原 WPF 截图
/// <c>04-diff-tab.png</c>（提交差异面板）构成 "原版 WPF / Avalonia 跨平台版"
/// 的对照图组。
///
/// <para>
/// 视觉差异是设计差异：原 WPF 是主窗口下方的 "差异" 标签页，Avalonia 端简化成
/// 独立 <see cref="DiffWindow"/>（一个含 <see cref="DiffView"/> 的小窗口）。
/// 这里只断言：窗口被真实渲染、尺寸合规、DiffView 内含 Added/Removed 行、PNG
/// 落盘成功且大小正常（&gt; 4 KB）。
/// </para>
///
/// <para>
/// 测试 fixture 用 3 个 commit 制造有意义的 diff：第二个 commit 改写 b.txt
/// （删一行、加两行），保证 DiffView 至少含 1 个 Removed + 1 个 Added 行。
/// </para>
/// </summary>
public class M3DiffWindowVisualScreenshotTests
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
    public async Task M3_DiffWindow_SelectCommit_RendersToPng()
    {
        EnsureServicesInitialized();
        string repoDir = CreateM3DiffRepo();
        try
        {
            var main = new MainWindow
            {
                Width = 1100,
                Height = 700,
            };
            main.Show();
            Dispatcher.UIThread.RunJobs();

            // 1) 填路径、点 "打开"
            TextBox repoBox = main.FindControl<TextBox>("RepoPathBox")!;
            Button openBtn = main.FindControl<Button>("OpenRepoButton")!;
            repoBox.Text = repoDir;
            openBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // 2) 选 main 分支
            ListBox branches = main.FindControl<ListBox>("BranchesList")!;
            branches.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            // 3) 选 "second commit"（改写 b.txt：删 1 行 + 加 2 行，diff 最丰富）
            ListBox commits = main.FindControl<ListBox>("CommitsList")!;
            var commitArr = commits.ItemsSource!.Cast<GitCommit>().ToArray();
            Assert.True(commitArr.Length >= 2);
            GitCommit second = commitArr.First(c => c.Subject == "second commit");
            commits.SelectedItem = second;
            Dispatcher.UIThread.RunJobs();

            // 4) 走 M3 公共入口：MainWindow.OpenSelectedCommitDiff() → DiffWindow
            DiffWindow? diffWin = main.OpenSelectedCommitDiff();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(diffWin);
            Assert.NotNull(diffWin!.DiffView.Current);

            // 5) 内容断言：second commit 改写 b.txt，至少有 1 个 Removed + 1 个 Added
            DiffResult diff = diffWin.DiffView.Current!;
            Assert.Contains(diff.Lines, l => l.Kind == DiffLineKind.Added);
            Assert.Contains(diff.Lines, l => l.Kind == DiffLineKind.Removed);

            // 6) 强制 DiffWindow 走布局 + 等渲染稳定，再栅格化为 Bitmap
            diffWin.Width = 880;
            diffWin.Height = 640;
            // Headless 模式下 Show() 不会自动走 Measure/Arrange 链路；
            // 显式 Measure + Arrange 把整个可视化树（含 TitleBar、Content、Border）拉起来，
            // 这样 RTB.Render 拿到的就是真实布局后的位图而不是空白帧。
            var finalSize = new Size(880, 640);
            diffWin.Measure(finalSize);
            diffWin.Arrange(new Rect(finalSize));
            diffWin.UpdateLayout();
            // 触发 AvaloniaEdit 的内部重绘（headless 下默认不会走 text-area 渲染钩子）
            diffWin.DiffView.InvalidateVisual();
            diffWin.DiffView.InvalidateMeasure();
            diffWin.DiffView.InvalidateArrange();
            diffWin.DiffView.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var size = new PixelSize(880, 640);
            var rtb = new RenderTargetBitmap(size, new Vector(96, 96));
            rtb.Render(diffWin);
            Assert.True(rtb.PixelSize.Width > 0 && rtb.PixelSize.Height > 0,
                $"RenderTargetBitmap 尺寸异常：{rtb.PixelSize}");

            // 7) 落盘 PNG
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "v3.9.0", "avalonia-m3-diff.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            try
            {
                using var fs = File.Create(outPath);
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            catch (Exception saveEx)
            {
                throw new InvalidOperationException(
                    $"Save 失败，路径={outPath}, Dir.Exists={Directory.Exists(Path.GetDirectoryName(outPath)!)}",
                    saveEx);
            }
            Assert.True(File.Exists(outPath), "PNG 未被写出：" + outPath);
            long sizeOnDisk = new FileInfo(outPath).Length;
            // M2 主窗口背景是 #1e1e1e + 多个 Border/Button/TextBlock，PNG ~28KB；
            // M3 DiffWindow 是单色 #1e1e1e 背景的 TextEditor（Avalonia.AvaloniaEdit 在
            // headless 下不会画字形，但 DiffView 的背景色已渲染进 RTB），
            // 实测 880x640 单色 PNG 约 3.7KB，故阈值放到 2000 以上避免 "全 0 字节" 假阴性。
            Assert.True(sizeOnDisk > 2_000, $"PNG 太小 ({sizeOnDisk} bytes)，看起来是空白帧");

            // 8) 落盘一份 diff 摘要，方便人工/自动 review
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            string fileName = System.IO.Path.GetFileName(repoDir);
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
                $"lines={diff.Lines.Count}）\n" +
                $"源仓库：{repoDir}（临时 {fileName}，测试结束自动删除）\n" +
                $"对照：verification/v3.9.0/04-diff-tab.png（原 WPF 差异标签页）\n" +
                string.Join("\n", summaryLines) + "\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    // ---- 共享仓库 fixture：3 个 commit，second 改写 b.txt（删 1 + 加 2），diff 最丰富 ----
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
