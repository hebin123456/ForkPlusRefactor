using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Git;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M2 验收：端到端 UI 流程——构造 MainWindow，输入仓库路径点"打开"，等分支列表出现后选中第一个分支，
/// 断言右侧 commit 列表出现 N 条 GitCommit，且首条 SHA 长度合规。
/// 对应原 WPF 工程 ForkPlus/UI/MainWindow.xaml 的"打开仓库 → 选中分支 → 看到提交列表"主流程。
/// [AvaloniaFact] 自动拉起 headless Application。
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class MainWindowCommitFlowTests
{
	// [AvaloniaFact] 会拉起一个新的 headless App，但不会触发 App.OnFrameworkInitializationCompleted
	// → ServiceLocator 没注册 → MainWindow 构造访问 AppContext 会 NRE。
	// 这里用一个静态构造 + lock 复刻 App.OnFrameworkInitializationCompleted 的注册（不开主题跟踪，
	// 避免在 headless 测试中污染平台主题）。
	private static bool _servicesInitialized;
	private static readonly object _initLock = new();

	private static void EnsureServicesInitialized()
	{
		if (_servicesInitialized)
		{
			return;
		}
		lock (_initLock)
		{
			if (_servicesInitialized)
			{
				return;
			}
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
	public async Task OpenRepo_SelectBranch_CommitsListPopulates_FromBiturbo()
	{
		EnsureServicesInitialized();
		string repoDir = CreateMultiCommitRepo();
		try
		{
			var window = new MainWindow();
			window.Show();
			Dispatcher.UIThread.RunJobs();

			// 1) 填入仓库路径，模拟点击"打开"
			TextBox repoBox = window.FindControl<TextBox>("RepoPathBox")!;
			Button openBtn = window.FindControl<Button>("OpenRepoButton")!;
			repoBox.Text = repoDir;
			openBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();

			// 2) 分支列表应至少含一条 refs/heads/main
			ListBox branches = window.FindControl<ListBox>("BranchesList")!;
			Assert.NotNull(branches.ItemsSource);
			var branchItems = branches.ItemsSource!.Cast<string>().ToArray();
			Assert.Contains(branchItems, b => b == "refs/heads/main");

			// 3) 模拟选中 refs/heads/main（SelectionChanged 事件触发 OnBranchSelectionChanged）
			branches.SelectedItem = "refs/heads/main";
			Dispatcher.UIThread.RunJobs();

			// 4) 提交列表应出现 3 条提交
			// M2+M3 已抽到 CommitDiffPanel，所以 CommitsList 是面板的子节点
			// —— 必须先找到面板，再从面板里 FindControl。
			var panel = window.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
			ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
			Assert.NotNull(commits.ItemsSource);
			// M2 提交图：ItemsSource 是 CommitGraphRow[]，从 row 取 commit
			var rows = commits.ItemsSource!.Cast<ForkPlus.Avalonia.Graph.CommitGraphRow>().ToArray();
			var commitItems = rows.Select(r => r.Commit).ToArray();
			Assert.Equal(3, commitItems.Length);
			foreach (GitCommit c in commitItems)
			{
				Assert.Equal(40, c.Sha.Length);
				Assert.Equal(7, c.ShortSha.Length);
				Assert.False(string.IsNullOrEmpty(c.Subject));
			}
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[AvaloniaFact]
	public async Task OpenRepo_BadPath_StatusTextReportsError_AndCommitsListStaysEmpty()
	{
		EnsureServicesInitialized();
		var window = new MainWindow();
		window.Show();
		Dispatcher.UIThread.RunJobs();

		string notARepo = Path.Combine(Path.GetTempPath(), "forkplus_not_a_repo_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(notARepo);
		try
		{
			TextBox repoBox = window.FindControl<TextBox>("RepoPathBox")!;
			Button openBtn = window.FindControl<Button>("OpenRepoButton")!;
			repoBox.Text = notARepo;
			openBtn.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();

			TextBlock status = window.FindControl<TextBlock>("StatusText")!;
			Assert.Contains("打开仓库失败", status.Text);
			// M2+M3 已抽到 CommitDiffPanel
			var panel = window.FindControl<ForkPlus.Avalonia.Panels.CommitDiffPanel>("CommitDiffPanel")!;
			ListBox commits = panel.FindControl<ListBox>("CommitsList")!;
			Assert.Null(commits.ItemsSource);
		}
		finally
		{
			try { Directory.Delete(notARepo, recursive: true); } catch { }
		}
	}

	// ---- 帮助方法 ----

	private static string CreateMultiCommitRepo()
	{
		string dir = Path.Combine(Path.GetTempPath(), "forkplus_m2_ui_repo_" + Guid.NewGuid().ToString("N"));
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
		foreach (string a in args)
		{
			psi.ArgumentList.Add(a);
		}

		using var proc = Process.Start(psi)
			?? throw new InvalidOperationException("无法启动 git 进程（CI runner 应预装 git）");
		proc.WaitForExit();

		string stderr = proc.StandardError.ReadToEnd();
		if (proc.ExitCode != 0)
		{
			throw new InvalidOperationException($"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode}): {stderr}");
		}
	}

	private static void TryDelete(string dir)
	{
		try
		{
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
		catch { }
	}
}
