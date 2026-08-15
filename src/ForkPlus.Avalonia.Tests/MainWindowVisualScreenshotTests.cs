using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M2 视觉验证：Avalonia Headless 跑主窗口 → 打开仓库 → 选中分支 → 触发 OnBranchSelectionChanged
/// → 拉取真实 commit 列表 → 截取主窗口的渲染帧并保存为 PNG。
///
/// 对应原 WPF 工程 ForkPlus/UI/MainWindow.xaml "打开仓库 → 看到提交列表" 的视觉回归。
/// 生成的 PNG 落盘到 <c>verification/v3.9.0/avalonia-m2-commits.png</c>，与
/// <c>03-demo-graph.png</c> 共同构成 "原版 WPF / Avalonia 跨平台版" 的对照图组。
///
/// 不直接做像素 diff：M2 的 Avalonia 布局是双面板 + commit 列表（迁移骨架），与 v3.9.0
/// 原版的 "提交图" 视觉差异是设计差异（双面板/侧栏 vs 中央图），不在像素回归范围内。
/// 这里只断言：渲染帧非空、尺寸合规、列表项可见、PNG 文件被真实写出。
///
/// 启动器 <see cref="SkiaHeadlessTestApp"/>（在 AssemblyInfo.cs 用 [assembly: AvaloniaTestApplication] 挂上）
/// 强制走 Skia CPU 渲染（默认 stub renderer 不画像素，RTB.Save 会写出 0 字节 PNG）。
/// </summary>
public class MainWindowVisualScreenshotTests
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
    public async Task M2_MainWindow_OpenRepo_CommitsList_RendersToPng()
    {
        EnsureServicesInitialized();
        string repoDir = CreateMultiCommitRepo();
        try
        {
            var window = new MainWindow
            {
                Width = 1100,
                Height = 700,
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // 1) 注入仓库路径并点 "打开"
            TextBox repoBox = window.FindControl<TextBox>("RepoPathBox")!;
            Button openBtn = window.FindControl<Button>("OpenRepoButton")!;
            repoBox.Text = repoDir;
            openBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            // 2) 选 main 分支
            ListBox branches = window.FindControl<ListBox>("BranchesList")!;
            Assert.NotNull(branches.ItemsSource);
            branches.SelectedItem = "refs/heads/main";
            Dispatcher.UIThread.RunJobs();

            // 3) 断言 commit 列表已被填充
            ListBox commits = window.FindControl<ListBox>("CommitsList")!;
            Assert.NotNull(commits.ItemsSource);
            var items = commits.ItemsSource!.Cast<GitCommit>().ToArray();
            Assert.True(items.Length >= 1, "选中 main 分支后 commit 列表应至少含 1 条");

            // 4) 用 RenderTargetBitmap 把主窗口直接栅格化到 Bitmap（不依赖 HeadlessDrawing/Skia 链路，
            //    不需要 ForceRenderTimerTick；和 v3.9.0 截图语义最接近的方案是渲染当前布局快照）。
            var size = new global::Avalonia.PixelSize(1100, 700);
            var rtb = new RenderTargetBitmap(size, new global::Avalonia.Vector(96, 96));
            rtb.Render(window);
            Assert.True(rtb.PixelSize.Width > 0 && rtb.PixelSize.Height > 0,
                $"RenderTargetBitmap 尺寸异常：{rtb.PixelSize}");

            // 5) 落盘 PNG（verification/ 相对仓库根）
            string outPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "verification", "v3.9.0", "avalonia-m2-commits.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            try
            {
                using var fs = File.Create(outPath);
                rtb.Save(fs, new PngBitmapEncoderOptions());
            }
            catch (Exception saveEx)
            {
                throw new InvalidOperationException($"Save 失败，路径={outPath}, Dir.Exists={Directory.Exists(Path.GetDirectoryName(outPath)!)}", saveEx);
            }
            Assert.True(File.Exists(outPath), "PNG 未被写出：" + outPath);
            long sizeOnDisk = new FileInfo(outPath).Length;
            Assert.True(sizeOnDisk > 4_000, $"PNG 太小 ({sizeOnDisk} bytes)，看起来是空白帧");

            // 6) 同时写一份最小的"逐项摘要"以辅助人工/自动 review
            string summaryPath = Path.ChangeExtension(outPath, ".txt");
            var summaryLines = items.Select(c =>
                $"{c.ShortSha}  {c.AuthorTime.LocalDateTime:yyyy-MM-dd HH:mm}  {c.Author}  {c.Subject}");
            File.WriteAllText(summaryPath,
                $"Avalonia M2 截屏时的 commit 列表（{items.Length} 条，来源 biturbo）：\n" +
                string.Join("\n", summaryLines) + "\n");
        }
        finally
        {
            TryDelete(repoDir);
        }
    }

    // ---- 复用自 MainWindowCommitFlowTests 的帮助方法 ----
    private static string CreateMultiCommitRepo()
    {
        string dir = Path.Combine(Path.GetTempPath(), "forkplus_m2_visual_repo_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        RunGit(dir, "init", "-b", "main");
        RunGit(dir, "config", "user.email", "ci@example.com");
        RunGit(dir, "config", "user.name", "ForkPlus CI");

        File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
        RunGit(dir, "add", "a.txt");
        RunGit(dir, "commit", "-m", "first commit");
        System.Threading.Thread.Sleep(1100);

        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        RunGit(dir, "add", "b.txt");
        RunGit(dir, "commit", "-m", "second commit");
        System.Threading.Thread.Sleep(1100);

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
