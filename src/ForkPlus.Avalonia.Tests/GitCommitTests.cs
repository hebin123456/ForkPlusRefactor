using System;
using System.Diagnostics;
using System.IO;
using ForkPlus.Avalonia.Git;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M2 验收：提交列表垂直切片。
/// 经 biturbo 原生引擎（bt_get_commits + bt_get_revision_headers）从指定 ref 拉取最新 N 条提交，
/// 验证 SHA / ShortSha / Subject / Author / AuthorTime 字段正确，对标原 WPF 的
/// GetRevisionStorageGitCommand + GetRevisionHeaders。
/// </summary>
public class GitCommitTests
{
	[Fact]
	public void GetCommits_ReturnsCommitsInDateOrder_WithSubjectAndAuthor()
	{
		string repoDir = CreateMultiCommitRepo();
		try
		{
			using var repo = new GitRepository(repoDir);
			GitCommit[] commits = repo.GetCommits(refName: "refs/heads/main", maxCount: 10);

			Assert.Equal(3, commits.Length);

			// 按 dateOrder=true，期望最新（c3）在前
			Assert.Equal("third commit", commits[0].Subject);
			Assert.Equal("second commit", commits[1].Subject);
			Assert.Equal("first commit", commits[2].Subject);

			// SHA 格式：40 字符小写 hex；ShortSha 是前 7
			foreach (GitCommit c in commits)
			{
				Assert.Equal(40, c.Sha.Length);
				Assert.Equal(7, c.ShortSha.Length);
				Assert.Equal(c.Sha.Substring(0, 7), c.ShortSha);
				Assert.Equal("ForkPlus CI", c.Author);
			}

			// 时间按从新到旧
			Assert.True(commits[0].AuthorTime > commits[1].AuthorTime);
			Assert.True(commits[1].AuthorTime > commits[2].AuthorTime);

			// DisplayLine 不应抛（UI 渲染用）
			foreach (GitCommit c in commits)
			{
				Assert.Contains(c.ShortSha, c.DisplayLine);
				Assert.Contains(c.Subject, c.DisplayLine);
			}
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[Fact]
	public void GetCommits_AcceptsShortBranchName()
	{
		string repoDir = CreateMultiCommitRepo();
		try
		{
			using var repo = new GitRepository(repoDir);
			// 短名 "main" 应自动补全为 "refs/heads/main"
			GitCommit[] commits = repo.GetCommits(refName: "main", maxCount: 5);
			Assert.NotEmpty(commits);
			Assert.Equal("third commit", commits[0].Subject);
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[Fact]
	public void GetCommits_RespectsMaxCount()
	{
		string repoDir = CreateMultiCommitRepo();
		try
		{
			using var repo = new GitRepository(repoDir);
			GitCommit[] commits = repo.GetCommits("refs/heads/main", maxCount: 2);
			Assert.Equal(2, commits.Length);
			Assert.Equal("third commit", commits[0].Subject);
			Assert.Equal("second commit", commits[1].Subject);
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[Fact]
	public void GetCommits_ThrowsArgumentException_ForUnknownRef()
	{
		string repoDir = CreateMultiCommitRepo();
		try
		{
			using var repo = new GitRepository(repoDir);
			Assert.Throws<ArgumentException>(() => repo.GetCommits("refs/heads/does-not-exist"));
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[Fact]
	public void GetCommits_ThrowsArgumentException_ForEmptyRef()
	{
		using var repo = new GitRepository(System.IO.Path.GetTempPath());
		Assert.Throws<ArgumentException>(() => repo.GetCommits(""));
		Assert.Throws<ArgumentException>(() => repo.GetCommits("   "));
	}

	// ---- 帮助方法 ----

	private static string CreateMultiCommitRepo()
	{
		string dir = Path.Combine(Path.GetTempPath(), "forkplus_m2_repo_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);

		RunGit(dir, "init", "-b", "main");
		RunGit(dir, "config", "user.email", "ci@example.com");
		RunGit(dir, "config", "user.name", "ForkPlus CI");

		// commit 1
		File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
		RunGit(dir, "add", "a.txt");
		RunGit(dir, "commit", "-m", "first commit");
		Sleep(1100); // git 提交时间戳精度 1 秒，避免两条 commit 同一秒

		// commit 2
		File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
		RunGit(dir, "add", "b.txt");
		RunGit(dir, "commit", "-m", "second commit");
		Sleep(1100);

		// commit 3
		File.WriteAllText(Path.Combine(dir, "c.txt"), "c");
		RunGit(dir, "add", "c.txt");
		RunGit(dir, "commit", "-m", "third commit");

		return dir;
	}

	private static void Sleep(int ms) => System.Threading.Thread.Sleep(ms);

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
		catch
		{
			// 临时目录清理失败不影响测试结论
		}
	}
}
