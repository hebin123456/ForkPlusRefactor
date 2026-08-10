using System;
using System.Diagnostics;
using System.IO;
using ForkPlus.Avalonia.Git;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M1 验收：仓库浏览垂直切片。
/// 经 biturbo 原生引擎（bt_get_references）列出刚初始化的临时仓库的引用，
/// 验证 GitRepository 三平台接线（Windows / Linux / macOS），对标原 WPF 的
/// GetReferencesGitCommand。测试内用 git CLI 建临时仓库作为被测对象。
/// </summary>
public class GitRepositoryTests
{
	[Fact]
	public void GetBranches_ReturnsLocalBranchRefs_ForFreshRepo()
	{
		string repoDir = CreateTempGitRepo();
		try
		{
			using var repo = new GitRepository(repoDir);
			string[] refs = repo.GetBranches();

			// biturbo 的 bt_get_references 对全新仓库返回本地分支引用（refs/heads/*），
			// HEAD 走 symref，不在 names 列表里，故不断言 HEAD。
			Assert.NotEmpty(refs);
			Assert.Contains(refs, r => r.StartsWith("refs/heads/", StringComparison.Ordinal));

			// 默认分支名因 git 版本而异（main / master），只要存在一个本地分支即可
			int localBranches = 0;
			foreach (string r in refs)
			{
				if (r.StartsWith("refs/heads/", StringComparison.Ordinal))
				{
					localBranches++;
				}
			}
			Assert.True(localBranches >= 1, $"应至少有一个本地分支，实际引用：{string.Join(", ", refs)}");
		}
		finally
		{
			TryDelete(repoDir);
		}
	}

	[Fact]
	public void GetBranches_ThrowsGitRepositoryException_ForNonGitPath()
	{
		string nonGit = Path.Combine(Path.GetTempPath(), "forkplus_not_a_repo_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(nonGit);
		try
		{
			using var repo = new GitRepository(nonGit);
			GitRepositoryException ex = Assert.Throws<GitRepositoryException>(() => repo.GetBranches());
			Assert.False(string.IsNullOrEmpty(ex.Message));
		}
		finally
		{
			TryDelete(nonGit);
		}
	}

	// ---- 帮助方法 ----

	private static string CreateTempGitRepo()
	{
		string dir = Path.Combine(Path.GetTempPath(), "forkplus_m1_repo_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);

		RunGit(dir, "init");
		RunGit(dir, "config", "user.email", "ci@example.com");
		RunGit(dir, "config", "user.name", "ForkPlus CI");
		File.WriteAllText(Path.Combine(dir, "README.md"), "# ForkPlus M1 test repo\n");
		RunGit(dir, "add", "README.md");
		RunGit(dir, "commit", "-m", "initial commit");

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
		catch
		{
			// 临时目录清理失败不影响测试结论
		}
	}
}
