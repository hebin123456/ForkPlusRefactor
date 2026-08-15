using System;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Biturbo;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M1+M2 仓库浏览：直接调用 biturbo 原生引擎列出仓库引用（分支）及其提交历史，
/// 对标原 WPF 的 <c>GetReferencesGitCommand</c> + <c>GetRevisionStorageGitCommand</c>
/// + <c>GetRevisionHeaders</c>。不引入原 <c>ForkPlus.Git</c> 命令层，仅复用
/// <c>ForkPlus.Biturbo</c> 的 P/Invoke 与结构体。
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
		(string[] names, string[] _) = ReadReferencesInternal();
		return names;
	}

	/// <summary>
	/// 列出指定引用（refs/heads/main 等）的最新 <paramref name="maxCount"/> 条提交。
	/// 流程：bt_get_references 解析 ref → bt_get_commits 拿拓扑 → bt_get_revision_headers 补 subject/author/time。
	/// </summary>
	/// <param name="refName">完整 ref 名（refs/heads/&lt;name&gt;），或分支短名（自动补全 refs/heads/）。</param>
	/// <param name="maxCount">最多返回的提交数（page_size）；0 或负数视为 50。</param>
	/// <exception cref="GitRepositoryException">biturbo 返回非 Ok 时抛出（信息含 last_error）。</exception>
	/// <exception cref="ArgumentException">仓库内找不到该 ref。</exception>
	public GitCommit[] GetCommits(string refName, int maxCount = 50)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(refName))
		{
			throw new ArgumentException("refName 不能为空", nameof(refName));
		}
		if (maxCount <= 0)
		{
			maxCount = 50;
		}

		string fullRef = NormalizeRefName(refName);
		string gitDir = ResolveGitDir(_repoPath);

		// 1) 解析 ref → SHA
		(string[] names, string[] shas) = ReadReferencesInternal();
		int idx = Array.IndexOf(names, fullRef);
		if (idx < 0)
		{
			throw new ArgumentException($"仓库内找不到 ref '{refName}'（已规范化为 '{fullRef}'）。", nameof(refName));
		}
		BtOid tip = HexToBtOid(shas[idx]);

		// 2) bt_get_commits 拿拓扑
		BtCommitGraphCache cache = default;
		BtCancellationToken cancel = Bt.bt_new_cancellation_token();
		BtCommitStorage storage = default;
		try
		{
			BtOid[] tips = new[] { tip };
			BtResult rc = Bt.bt_get_commits(
				git_dir_path: gitDir,
				tips_ptr: tips, tips_len: tips.Length,
				date_order: true,
				page_size: maxCount, skip_pages: 0, min_pages: 0,
				required_oids_ptr: null, required_oids_len: 0,
				commit_graph_cache_ptr: ref cache,
				cancellation_token_ptr: ref cancel,
				out_result: ref storage);
			if (rc != BtResult.Ok)
			{
				throw new GitRepositoryException($"bt_get_commits 失败 ({rc})：{ReadLastErrorMessage()}");
			}

			string[] commitShas = storage.ExtractCommitShas();
			if (commitShas.Length == 0)
			{
				return Array.Empty<GitCommit>();
			}

			// 3) bt_get_revision_headers 补 subject/author/time
			return ResolveHeaders(gitDir, commitShas);
		}
		finally
		{
			Bt.bt_release_commit_storage(ref storage);
			Bt.bt_release_cancellation_token(ref cancel);
		}
	}

	/// <summary>bt_get_references + 一次性 marshaling（names + 关联 SHA）。</summary>
	private (string[] Names, string[] Shas) ReadReferencesInternal()
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
			int count = (int)refs.names_offsets_len;
			if (count <= 0)
			{
				return (Array.Empty<string>(), Array.Empty<string>());
			}

			// names
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

			// shas
			int oidSize = Marshal.SizeOf<BtOid>();
			var shas = new string[count];
			for (int i = 0; i < count; i++)
			{
				IntPtr p = new IntPtr(refs.oids.ToInt64() + (long)i * oidSize);
				BtOid o = Marshal.PtrToStructure<BtOid>(p);
				shas[i] = string.Concat(
					o.s0.ToString("x8"),
					o.s1.ToString("x8"),
					o.s2.ToString("x8"),
					o.s3.ToString("x8"),
					o.s4.ToString("x8"));
			}
			return (names, shas);
		}
		finally
		{
			Bt.bt_release_references(ref refs);
		}
	}

	private GitCommit[] ResolveHeaders(string gitDir, string[] commitShas)
	{
		var oidArr = new BtOid[commitShas.Length];
		for (int i = 0; i < commitShas.Length; i++)
		{
			oidArr[i] = HexToBtOid(commitShas[i]);
		}

		BtRevisionHeaders headers = default;
		try
		{
			// working_dir_path 取 gitDir 父目录（与原 WPF GetFullRevisionDetailsGitCommand 传法一致）
			string workingDir = System.IO.Directory.GetParent(gitDir)?.FullName ?? _repoPath;
			BtResult rc = Bt.bt_get_revision_headers(workingDir, gitDir, oidArr, oidArr.Length, ref headers);
			if (rc != BtResult.Ok)
			{
				throw new GitRepositoryException($"bt_get_revision_headers 失败 ({rc})：{ReadLastErrorMessage()}");
			}

			int revCount = (int)headers.revisions_len;
			int revSize = Marshal.SizeOf<BtRevisionHeader>();
			var revStructs = new BtRevisionHeader[revCount];
			for (int i = 0; i < revCount; i++)
			{
				IntPtr p = new IntPtr(headers.revisions.ToInt64() + (long)i * revSize);
				revStructs[i] = Marshal.PtrToStructure<BtRevisionHeader>(p);
			}

			var identities = ReadIdentities(headers);

			var result = new GitCommit[revCount];
			for (int i = 0; i < revCount; i++)
			{
				BtRevisionHeader h = revStructs[i];
				int authorIdx = (int)h.author_index;
				string author = (authorIdx >= 0 && authorIdx < identities.Length) ? identities[authorIdx] : "";
				string subject = h.subject == IntPtr.Zero ? "" : ReadUtf8(h.subject);
				DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(h.author_time);
				result[i] = new GitCommit(commitShas[i], subject, author, time);
			}
			return result;
		}
		finally
		{
			Bt.bt_release_revision_headers(ref headers);
		}
	}

	private static string[] ReadIdentities(BtRevisionHeaders headers)
	{
		int n = (int)headers.identities_len;
		if (n <= 0 || headers.identities == IntPtr.Zero)
		{
			return Array.Empty<string>();
		}
		var ptrs = new IntPtr[n];
		Marshal.Copy(headers.identities, ptrs, 0, n);
		var arr = new string[n];
		for (int i = 0; i < n; i++)
		{
			arr[i] = ReadUtf8(ptrs[i]);
		}
		return arr;
	}

	private static string ReadUtf8(IntPtr p)
	{
		if (p == IntPtr.Zero)
		{
			return "";
		}
		var ms = new System.IO.MemoryStream();
		int offset = 0;
		while (true)
		{
			byte b = Marshal.ReadByte(p, offset);
			if (b == 0)
			{
				break;
			}
			ms.WriteByte(b);
			offset++;
		}
		return Encoding.UTF8.GetString(ms.ToArray());
	}

	private static BtOid HexToBtOid(string hex)
	{
		if (hex == null || hex.Length < 40)
		{
			return default;
		}
		BtOid o = default;
		o.s0 = Convert.ToUInt32(hex.Substring(0, 8), 16);
		o.s1 = Convert.ToUInt32(hex.Substring(8, 8), 16);
		o.s2 = Convert.ToUInt32(hex.Substring(16, 8), 16);
		o.s3 = Convert.ToUInt32(hex.Substring(24, 8), 16);
		o.s4 = Convert.ToUInt32(hex.Substring(32, 8), 16);
		return o;
	}

	private static string NormalizeRefName(string refName)
	{
		if (refName.StartsWith("refs/", StringComparison.Ordinal))
		{
			return refName;
		}
		return "refs/heads/" + refName;
	}

	private static string ResolveGitDir(string repoPath)
	{
		string dotGit = System.IO.Path.Combine(repoPath, ".git");
		return System.IO.Directory.Exists(dotGit) ? dotGit : repoPath;
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
