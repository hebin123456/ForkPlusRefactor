using System;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Biturbo;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M1 仓库浏览：直接调用 biturbo 原生引擎列出仓库引用（分支），对标原 WPF 的
/// <c>GetReferencesGitCommand</c>。不引入原 <c>ForkPlus.Git</c> 命令层，仅复用
/// <c>ForkPlus.Biturbo</c> 的 P/Invoke 与结构体，做最小 marshaling
/// （GetRefs 的原始逻辑见原 Git/Commands/BtReferencesExtensions.cs，此处改为返回 <c>string[]</c>）。
/// 后续 M2 的提交图走 <c>bt_get_commits</c> + <c>BtCommitGraphCache</c>，复用同一接线。
/// </summary>
public sealed class GitRepository : IDisposable
{
	private readonly string _repoPath;
	private bool _disposed;

	public GitRepository(string repositoryPath)
	{
		_repoPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
	}

	/// <summary>返回仓库全部引用名（refs/heads/*、refs/remotes/*、HEAD 等）。</summary>
	public string[] GetBranches()
	{
		EnsureNotDisposed();
		string gitDir = ResolveGitDir(_repoPath);
		BtReferences refs = default;
		BtResult result = Bt.bt_get_references(gitDir, skip_tags: false, ref refs);
		if (result != BtResult.Ok)
		{
			string msg = ReadLastErrorMessage();
			Bt.bt_release_references(ref refs);
			throw new GitRepositoryException($"bt_get_references 失败 ({result})：{msg}");
		}
		try
		{
			return MarshalReferenceNames(refs);
		}
		finally
		{
			Bt.bt_release_references(ref refs);
		}
	}

	private static string ResolveGitDir(string repoPath)
	{
		string dotGit = System.IO.Path.Combine(repoPath, ".git");
		return System.IO.Directory.Exists(dotGit) ? dotGit : repoPath;
	}

	private static string[] MarshalReferenceNames(BtReferences refs)
	{
		int count = (int)refs.names_offsets_len;
		if (count <= 0)
		{
			return Array.Empty<string>();
		}
		long[] offsets = new long[count];
		Marshal.Copy(refs.names_offsets, offsets, 0, count);
		int dataLen = (int)refs.names_data_len;
		byte[] data = new byte[dataLen];
		if (dataLen > 0)
		{
			Marshal.Copy(refs.names_data, data, 0, dataLen);
		}
		var names = new string[count];
		for (int i = 0; i < count; i++)
		{
			int start = (i != 0) ? (int)offsets[i - 1] : 0;
			int len = (int)offsets[i] - start;
			names[i] = Encoding.UTF8.GetString(data, start, len);
		}
		return names;
	}

	private static string ReadLastErrorMessage()
	{
		try
		{
			ulong cap = 1024;
			IntPtr ptr = Marshal.AllocHGlobal((int)cap);
			long n = Bt.bt_get_last_error_message(ptr, cap);
			if (n < 0)
			{
				Marshal.FreeHGlobal(ptr);
				cap = (ulong)(~n);
				ptr = Marshal.AllocHGlobal((int)cap);
				n = Bt.bt_get_last_error_message(ptr, cap);
			}
			if (n < 0)
			{
				Marshal.FreeHGlobal(ptr);
				return "无法读取 biturbo 错误信息";
			}
			byte[] buf = new byte[cap];
			Marshal.Copy(ptr, buf, 0, (int)cap);
			Marshal.FreeHGlobal(ptr);
			int end = Array.IndexOf(buf, (byte)0);
			if (end < 0)
			{
				end = buf.Length;
			}
			return Encoding.UTF8.GetString(buf, 0, end);
		}
		catch
		{
			return "无法读取 biturbo 错误信息";
		}
	}

	private void EnsureNotDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(GitRepository));
		}
	}

	public void Dispose()
	{
		_disposed = true;
		GC.SuppressFinalize(this);
	}
}

/// <summary>biturbo 引擎调用失败时抛出，便于 UI 层捕获并展示。</summary>
public sealed class GitRepositoryException : Exception
{
	public GitRepositoryException(string message) : base(message)
	{
	}
}
