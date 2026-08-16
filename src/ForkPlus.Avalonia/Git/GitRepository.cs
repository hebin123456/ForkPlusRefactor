using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Avalonia.Diff;
using ForkPlus.Biturbo;
using ForkPlus.Services;

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
	/// 流程：bt_get_references 解析 ref → bt_get_commits 拿拓扑（含 parents） →
	/// bt_get_revision_headers 补 subject/author/time → 再回到 references 给每个 commit 标注 ref 装饰。
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

		// 1) 解析 ref → SHA（一次性拿全仓库的 ref 表，后续给 commit 标 decoration 用）
		(string[] refNames, string[] refShas) = ReadReferencesInternal();
		int idx = Array.IndexOf(refNames, fullRef);
		if (idx < 0)
		{
			throw new ArgumentException($"仓库内找不到 ref '{refName}'（已规范化为 '{fullRef}'）。", nameof(refName));
		}
		BtOid tip = HexToBtOid(refShas[idx]);

		// 2) bt_get_commits 拿拓扑 + parents
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

			IReadOnlyList<string[]> shasAndParents = storage.ExtractCommitShasAndParents();
			if (shasAndParents.Count == 0)
			{
				return Array.Empty<GitCommit>();
			}

			// 3) bt_get_revision_headers 补 subject/author/time
			string[] selfShas = new string[shasAndParents.Count];
			for (int i = 0; i < shasAndParents.Count; i++)
			{
				selfShas[i] = shasAndParents[i][0];
			}
			GitCommit[] commits = ResolveHeaders(gitDir, selfShas);

			// 4) 把 parents 和 ref 装饰补回 GitCommit
			//    ref 装饰：去重后只保留我们能识别且 SHA 命中的 ref 短名
			//    （如 refs/heads/master → "master"，refs/remotes/origin/master → "origin/master"，
			//    refs/tags/v1.0 → "v1.0"）。stashes 在 WPF 是单独 API，本期不接。
			var shaToRefs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			for (int i = 0; i < refNames.Length; i++)
			{
				if (refShas[i] == null) continue;
				if (!shaToRefs.TryGetValue(refShas[i], out var list))
				{
					list = new List<string>();
					shaToRefs[refShas[i]] = list;
				}
				list.Add(ShortenRef(refNames[i]));
			}

			for (int i = 0; i < commits.Length; i++)
			{
				GitCommit baseCommit = commits[i];
				string[] seg = shasAndParents[i];
				string[] parents = seg.Length > 1
					? new ArraySegment<string>(seg, 1, seg.Length - 1).ToArray()
					: Array.Empty<string>();
				string[] decorations = shaToRefs.TryGetValue(baseCommit.Sha, out var d)
					? d.ToArray()
					: Array.Empty<string>();
				commits[i] = new GitCommit(
					baseCommit.Sha, baseCommit.Subject, baseCommit.Author, baseCommit.AuthorTime,
					parents, decorations);
			}
			return commits;
		}
		finally
		{
			Bt.bt_release_commit_storage(ref storage);
			Bt.bt_release_cancellation_token(ref cancel);
		}
	}

	/// <summary>
	/// 把 <c>refs/heads/master</c> 压缩为 <c>master</c>、<c>refs/remotes/origin/master</c> 压缩为
	/// <c>origin/master</c>、<c>refs/tags/v1.0</c> 压缩为 <c>v1.0</c>。保留 HEAD 直接。
	/// </summary>
	private static string ShortenRef(string fullName)
	{
		if (string.IsNullOrEmpty(fullName)) return fullName;
		if (fullName == "HEAD") return "HEAD";
		if (fullName.StartsWith("refs/heads/", StringComparison.Ordinal))
		{
			return fullName.Substring("refs/heads/".Length);
		}
		if (fullName.StartsWith("refs/remotes/", StringComparison.Ordinal))
		{
			return fullName.Substring("refs/remotes/".Length);
		}
		if (fullName.StartsWith("refs/tags/", StringComparison.Ordinal))
		{
			return fullName.Substring("refs/tags/".Length);
		}
		return fullName;
	}

	/// <summary>
	/// M3：获取某个 commit 相对其第一个 parent 的 unified diff（整次提交的全部文件）。
	/// 首个 commit（无 parent）通过 <c>git show &lt;sha&gt;</c> 取 patch，相对"空树"。
	///
	/// <para>
	/// 实现：直接 spawn 系统 <c>git</c>（由 <see cref="ServiceLocator.IGitPathResolver"/>
	/// 解析为无扩展名的 <c>git</c>）。biturbo 当前不暴露 <c>git diff-tree</c> 等价 API，
	/// 故保留 .NET <see cref="Process"/> 通道；biturbo 的 <c>bt_spawn_with_output</c>
	/// 是其内部替代，公开 API 同形态，可在重构中替换此处以减少跨平台回归面。
	/// </para>
	///
	/// <para>返回的 <see cref="DiffResult"/> 已经把多文件 patch 拍平为单个
	/// <see cref="DiffLine"/> 序列（文件之间用 Unchanged 标记的横线分隔），可直接喂给
	/// <see cref="ForkPlus.Avalonia.Diff.DiffView"/>。
	/// </para>
	/// </summary>
	/// <param name="commitSha">完整或前缀 SHA（会被 <c>git rev-parse</c> 解析）。</param>
	/// <exception cref="ArgumentException">空 SHA 或 git 返回非 0 退出码。</exception>
	/// <exception cref="GitRepositoryException">git 进程启动失败。</exception>
	public DiffResult GetCommitDiff(string commitSha)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(commitSha))
		{
			throw new ArgumentException("commitSha 不能为空", nameof(commitSha));
		}

		// 1) rev-parse 拿完整 SHA + 父 SHA 列表
		string fullSha = RunGit(commitSha, "rev-parse", "--verify", commitSha.Trim() + "^{commit}").Trim();
		if (fullSha.Length < 7)
		{
			throw new ArgumentException($"无法解析 commit '{commitSha}'（rev-parse 输出为空）。", nameof(commitSha));
		}

		string parentsOutput = RunGit(fullSha, "rev-list", "-n", "1", "--parents", fullSha).Trim();
		// 格式：<sha> [<parent_sha>...]
		string[] parts = parentsOutput.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		bool hasParent = parts.Length >= 2;

		// 2) 拉 unified diff
		//    有 parent：git diff <parent> <sha>，得到"两个版本之间"的差异
		//    无 parent：git show <sha>，包含 commit 头 + patch
		string unified;
		if (hasParent)
		{
			unified = RunGit(fullSha, "diff", "--no-color", "--no-ext-diff", parts[1], fullSha);
		}
		else
		{
			// show 输出含 commit 头 + diff；strip 前导到第一个 "diff --git"
			string showOut = RunGit(fullSha, "show", "--no-color", "--pretty=format:", fullSha);
			unified = showOut;
		}

		if (string.IsNullOrWhiteSpace(unified))
		{
			// 空 commit（例如 merge-only 或没有 tree diff）：返回空 DiffResult
			return new DiffResult("a/" + fullSha, "b/" + fullSha, Array.Empty<DiffLine>());
		}

		return UnifiedDiffParser.Parse(unified,
			fallbackOldPath: "a/" + fullSha.Substring(0, Math.Min(7, fullSha.Length)),
			fallbackNewPath: "b/" + fullSha.Substring(0, Math.Min(7, fullSha.Length)));
	}

	/// <summary>
	/// M4：列出工作区所有相对当前 HEAD 的改动（unstaged + staged + untracked）。
	/// <para>
	/// 实现：spawn <c>git status --porcelain=1 -z</c>，解析两字符 XY 位图。
	/// <c>-z</c> 用 NUL 分隔条目，避免文件名含特殊字符（如中文、换行、引号）时的解析歧义。
	/// </para>
	/// </summary>
	/// <exception cref="GitRepositoryException">git 进程启动失败或返回非 0。</exception>
	public WorkingTreeChange[] GetWorkingTreeChanges()
	{
		EnsureNotDisposed();
		// --porcelain=1 -z 走机器友好格式
		string raw = RunGit("status", "status", "--porcelain=1", "-z", "--untracked-files=all");
		if (string.IsNullOrEmpty(raw))
		{
			return Array.Empty<WorkingTreeChange>();
		}
		return ParsePorcelainStatus(raw);
	}

	/// <summary>
	/// M4：拿到某文件当前 working tree 相对 HEAD 的 unified diff。
	/// <para>
	/// 对应 <c>git diff --no-color -- &lt;path&gt;</c>：
	/// 已暂存的部分要 diff 才看得到，所以这里把 staged + unstaged 拼起来。
	/// </para>
	/// </summary>
	/// <param name="relativePath">相对仓库根的路径（<c>git status</c> 报的形态）。</param>
	public DiffResult GetWorkingTreeDiff(string relativePath)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(relativePath))
		{
			throw new ArgumentException("relativePath 不能为空", nameof(relativePath));
		}
		// 1) staged（index vs HEAD）
		string staged = RunGit("diff-staged", "diff", "--no-color", "--no-ext-diff",
			"--cached", "--", relativePath);
		// 2) unstaged（working tree vs index）
		string unstaged = RunGit("diff-unstaged", "diff", "--no-color", "--no-ext-diff",
			"--", relativePath);
		// 3) untracked：文件不存在，diff 永远是空 -> 由调用方决定是否走"整文件全是 Added"路径
		string combined = (staged ?? "") + (unstaged ?? "");
		if (string.IsNullOrWhiteSpace(combined))
		{
			// 可能是 untracked，单独打 full file
			string fullPath = System.IO.Path.Combine(_repoPath, relativePath);
			if (System.IO.File.Exists(fullPath))
			{
				string[] allLines = System.IO.File.ReadAllLines(fullPath);
				var synthetic = new System.Text.StringBuilder();
				synthetic.Append("--- /dev/null\n");
				synthetic.Append("+++ b/").Append(relativePath).Append('\n');
				synthetic.Append("@@ -0,0 +1,").Append(allLines.Length).Append(" @@\n");
				foreach (string line in allLines)
				{
					synthetic.Append('+').Append(line).Append('\n');
				}
				combined = synthetic.ToString();
			}
			else
			{
				return new DiffResult("a/" + relativePath, "b/" + relativePath, Array.Empty<DiffLine>());
			}
		}
		return UnifiedDiffParser.Parse(combined,
			fallbackOldPath: "a/" + relativePath,
			fallbackNewPath: "b/" + relativePath);
	}

	/// <summary>
	/// M5：拿到指定 ref 的文件树（递归）。
	/// <para>
	/// 对应 <c>git ls-tree -r &lt;ref&gt;</c>：每个 blob 输出一行
	/// <c>&lt;mode&gt; &lt;type&gt; &lt;sha&gt;\t&lt;path&gt;</c>。
	/// 解析后还原为层级 <see cref="FileTreeNode"/>（对标 WPF 原版
	/// <c>GetRevisionFileTreeGitCommand</c> + <c>RevisionFileTreeViewItem</c>）。
	/// </para>
	/// <para>
	/// 这里走 <c>git</c> CLI 而非 biturbo 原生 <c>bt_get_tree</c>，因为
	/// <c>bt_get_tree</c> 不会展开"tree-of-tree"，需要在 C# 侧按路径段递归再调一次，
	/// 等价路径数 = 文件数（每次拿整层）。CLI 一次 <c>git ls-tree -r</c> 拿全所有路径更简单。
	/// 在跨平台侧，git 路径由 <see cref="ServiceLocator.GitPathResolver"/> 解析。
	/// </para>
	/// </summary>
	/// <param name="refName">ref 短名（main）或完整名（refs/heads/main）或 commit SHA。</param>
	/// <returns>根节点（虚拟根，<c>FullPath == ""</c>），其 Children 是仓库顶层目录/文件。</returns>
	/// <exception cref="ArgumentException">refName 为空。</exception>
	/// <exception cref="GitRepositoryException">git ls-tree 失败（ref 不存在、不是 tree、git 缺失等）。</exception>
	public FileTreeNode GetFileTree(string refName)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(refName))
		{
			throw new ArgumentException("refName 不能为空", nameof(refName));
		}
		// -r 递归（一次性拿全部文件），-z NUL 分隔（路径里可能有空格/中文）
		string raw = RunGit("ls-tree", "ls-tree", "-r", "-z", refName);
		FileTreeNode root = new FileTreeNode(
			name: "",
			fullPath: "",
			sha: "",
			itemType: FileTreeNode.FileTreeNodeKind.Directory,
			depth: -1);
		if (string.IsNullOrEmpty(raw))
		{
			return root; // 空 ref（理论上不可能，但兜底）
		}
		string[] entries = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
		foreach (string entry in entries)
		{
			// 格式： "<mode> <type> <sha>\t<path>"
			int tab = entry.IndexOf('\t');
			if (tab <= 0) continue;
			string meta = entry.Substring(0, tab);
			string path = entry.Substring(tab + 1);
			string[] metaParts = meta.Split(' ');
			if (metaParts.Length < 3) continue;
			string typeStr = metaParts[1];
			string sha = metaParts[2];
			FileTreeNode.FileTreeNodeKind kind = typeStr switch
			{
				"blob" => FileTreeNode.FileTreeNodeKind.File,
				"tree" => FileTreeNode.FileTreeNodeKind.Directory,
				"commit" => FileTreeNode.FileTreeNodeKind.Submodule,
				_ => FileTreeNode.FileTreeNodeKind.File,
			};
			// 递归创建/复用中间目录节点
			InsertPath(root, path, sha, kind);
		}
		SortRecursive(root);
		return root;
	}

	/// <summary>把 "src/Foo/Bar/baz.cs" 拆段并依次插入到 root 的 Children 中。
	/// 已存在的中间目录复用（按 FullPath 匹配）。</summary>
	private static void InsertPath(FileTreeNode parent, string relativePath, string sha, FileTreeNode.FileTreeNodeKind leafKind)
	{
		string[] parts = relativePath.Split('/');
		FileTreeNode current = parent;
		for (int i = 0; i < parts.Length; i++)
		{
			string part = parts[i];
			bool isLeaf = i == parts.Length - 1;
			// 找现有同名子节点
			FileTreeNode? existing = null;
			foreach (FileTreeNode c in current.Children)
			{
				if (c.Name == part) { existing = c; break; }
			}
			if (existing != null)
			{
				current = existing;
				continue;
			}
			// 计算当前段全路径（root 的 FullPath 为 ""，拼接时用 '/'）
			string curPath = current.Depth < 0
				? part
				: current.FullPath + "/" + part;
			FileTreeNode.FileTreeNodeKind curKind = isLeaf
				? leafKind
				: FileTreeNode.FileTreeNodeKind.Directory; // 中间段一定是目录
			// 中间段没 SHA（git ls-tree -r 只给叶子 SHA）
			string curSha = isLeaf ? sha : "";
			FileTreeNode node = new FileTreeNode(
				name: part,
				fullPath: curPath,
				sha: curSha,
				itemType: curKind,
				depth: current.Depth + 1);
			current.Children.Add(node);
			current = node;
		}
	}

	/// <summary>递归排序：目录优先，再按文件名自然序（同 WPF 原版 <c>NaturalStringComparer</c>）。</summary>
	private static void SortRecursive(FileTreeNode node)
	{
		if (node.Children.Count == 0) return;
		node.Children.Sort((a, b) =>
		{
			int t = -1 * a.ItemType.CompareTo(b.ItemType); // Directory 在前
			return t != 0 ? t : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
		});
		foreach (FileTreeNode c in node.Children)
		{
			SortRecursive(c);
		}
	}

	/// <summary>
	/// M5：拿到某文件在指定 ref 下的原始内容（<c>git show &lt;ref&gt;:&lt;path&gt;</c>）。
	/// <para>
	/// 用作 file tree 双击文件时在右侧文件内容预览区显示。对标 WPF 原版
	/// <c>FileListUserControl.OnFileDoubleClick</c> → <c>FileContentControl</c>。
	/// </para>
	/// </summary>
	/// <param name="refName">完整 ref 或 commit SHA。</param>
	/// <param name="relativePath">相对仓库根的路径（须是文件，不支持目录）。</param>
	/// <returns>文件内容（UTF-8 解码，失败时返回错误字符串而非抛异常，以便 UI 直接显示）。</returns>
	public string GetFileContent(string refName, string relativePath)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(refName) || string.IsNullOrWhiteSpace(relativePath))
		{
			return string.Empty;
		}
		try
		{
			// git show <ref>:<path> 输出文件内容到 stdout
			return RunGit("show-file", "show", $"{refName}:{relativePath}");
		}
		catch (Exception ex)
		{
			return $"[无法读取 {refName}:{relativePath}：{ex.Message}]";
		}
	}

	/// <summary>
	/// M7：列出当前仓库所有 tag（含 annotated + lightweight，按字典序）。
	/// <para>
	/// 对应 <c>git for-each-ref --format=... refs/tags/</c>，每行 6 段 ASCII Unit Separator
	/// (<c>\u001f</c>) 分隔：
	/// <list type="bullet">
	///   <item><c>%(refname:short)</c> → tag 短名（<c>v1.0.0</c>）</item>
	///   <item><c>%(objectname)</c> → tag 自身 SHA（annotated 为 tag commit，lightweight 为 target commit）</item>
	///   <item><c>%(objecttype)</c> → "tag" / "commit"（用于判定 annotated）</item>
	///   <item><c>%(*objectname)</c> → peeled 后的 commit SHA（lightweight 为空）</item>
	///   <item><c>%(taggername) %(taggeremail)</c> → tagger 身份（lightweight 为空）</item>
	///   <item><c>%(taggerdate:iso-strict)</c> → tag 时间（lightweight 为空）</item>
	/// </list>
	/// 用 <c>\u001f</c>（ASCII Unit Separator）做字段分隔，避免与 tag name / tagger 中可能含的
	/// <c>|</c> 等普通分隔符冲突。
	/// 跨平台实现走 git CLI；biturbo 的 <c>bt_get_tags</c> 公开 API 暂未确认，CLI 路径简单稳定可移植。
	/// </para>
	/// </summary>
	/// <returns>按 tag 短名字典序的 GitTag 数组；无 tag 返回空数组。</returns>
	/// <exception cref="GitRepositoryException">git 进程启动失败。</exception>
	public GitTag[] GetTags()
	{
		EnsureNotDisposed();
		const char Sep = '\u001f';
		string fmt =
			$"%(refname:short){Sep}%(objectname){Sep}%(objecttype){Sep}%(*objectname){Sep}%(taggername) %(taggeremail){Sep}%(taggerdate:iso-strict)";
		string raw = RunGit("for-each-ref-tags", "for-each-ref", "--format=" + fmt, "refs/tags/");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return Array.Empty<GitTag>();
		}
		string[] lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var list = new List<GitTag>(lines.Length);
		for (int i = 0; i < lines.Length; i++)
		{
			// 6 段：name, sha, type, peeled, tagger, date
			string[] parts = lines[i].Split(Sep);
			if (parts.Length < 6) continue;
			string name = parts[0];
			string sha = parts[1];
			string type = parts[2];
			string peeled = parts[3];
			string tagger = parts[4];
			string dateStr = parts[5];
			bool isAnnotated = type == "tag";
			// lightweight tag 没有 peeled 字段 → 回退到 sha
			if (string.IsNullOrEmpty(peeled))
			{
				peeled = sha;
			}
			DateTimeOffset tagDate = DateTimeOffset.MinValue;
			if (!string.IsNullOrWhiteSpace(dateStr) && DateTimeOffset.TryParse(dateStr, out var parsed))
			{
				tagDate = parsed;
			}
			list.Add(new GitTag(name, sha, peeled, isAnnotated, tagger, tagDate));
		}
		// git for-each-ref 默认按 refname 排序；M7 显式按 name 字典序排一遍（去掉 refs/tags/ 前缀后）
		list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
		return list.ToArray();
	}

	/// <summary>
	/// M7：删除一个本地 tag（<c>git tag -d &lt;name&gt;</c>）。
	/// <para>
	/// M7 阶段只支持删除本地 tag。删除远程 tag（<c>git push origin :&lt;name&gt;</c>）涉及远端认证，
	/// 放到后续阶段。
	/// </para>
	/// </summary>
	/// <exception cref="ArgumentException">tagName 为空。</exception>
	/// <exception cref="GitRepositoryException">tag 不存在或 git 失败。</exception>
	public void DeleteLocalTag(string tagName)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(tagName))
		{
			throw new ArgumentException("tagName 不能为空", nameof(tagName));
		}
		RunGit("tag-delete", "tag", "-d", tagName.Trim());
	}

	/// <summary>
	/// M6：列出当前仓库所有 stash 条目。
	/// <para>
	/// 对应 <c>git stash list --format=%gd|%H|%s</c>：
	/// <list type="bullet">
	///   <item><c>%gd</c> → reflog selector（<c>stash@{0}</c>），可喂给 apply/pop/drop</item>
	///   <item><c>%H</c> → stash commit 完整 SHA</item>
	///   <item><c>%s</c> → subject（即 <c>On &lt;branch&gt;: &lt;msg&gt;</c>）</item>
	/// </list>
	/// 跨平台实现走 git CLI；biturbo 的 <c>bt_get_repository_stashes</c> 也能拿
	/// （结构体 <c>BtStash</c> / <c>BtRepositoryStashes</c> 已就位），但 P/Invoke ABI
	/// 在 Linux 上还没 smoke 测过，CLI 路径更稳；后续若 biturbo 跑通可切换。
	/// </para>
	/// </summary>
	/// <returns>按 reflog 顺序（最新在前）的 GitStash 数组；空仓库返回空数组。</returns>
	/// <exception cref="GitRepositoryException">git 进程启动失败。</exception>
	public GitStash[] GetStashes()
	{
		EnsureNotDisposed();
		// --format=%gd|%H|%s 让每行都是 3 段 NUL/换行 分隔
		// 用 -z 让 \n 不会进入 message（message 本身允许 \n）
		// 实际上 stash subject 是单行 --format 直接给 --format=%gd|%H|%s 即可
		string raw = RunGit("stash-list", "stash", "list", "--format=%gd|%H|%s");
		if (string.IsNullOrWhiteSpace(raw))
		{
			return Array.Empty<GitStash>();
		}
		string[] lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var list = new List<GitStash>(lines.Length);
		for (int i = 0; i < lines.Length; i++)
		{
			// 格式: "stash@{N}|<sha>|<subject>"
			string line = lines[i];
			int p1 = line.IndexOf('|');
			if (p1 <= 0) continue;
			int p2 = line.IndexOf('|', p1 + 1);
			if (p2 <= p1) continue;
			string selector = line.Substring(0, p1);
			string sha = line.Substring(p1 + 1, p2 - p1 - 1);
			string subject = line.Substring(p2 + 1);

			// subject 通常是 "On <branch>: <message>"
			// 拿不到这个前缀的（自定义 subject 或 detached HEAD）就当成纯 message
			string branch = "";
			string message = subject;
			if (subject.StartsWith("On ", StringComparison.Ordinal))
			{
				int colonIdx = subject.IndexOf(": ", StringComparison.Ordinal);
				if (colonIdx > 0)
				{
					branch = subject.Substring(3, colonIdx - 3);
					message = subject.Substring(colonIdx + 2);
				}
			}

			// selector 形如 "stash@{0}" → 拿 N
			int idx = ParseReflogIndex(selector);

			list.Add(new GitStash(idx, selector, sha, branch, message));
		}
		return list.ToArray();
	}

	/// <summary>把 "stash@{3}" 解析成数字 3；解析失败返回 -1。</summary>
	private static int ParseReflogIndex(string selector)
	{
		if (string.IsNullOrEmpty(selector)) return -1;
		int start = selector.IndexOf('{');
		int end = selector.IndexOf('}');
		if (start < 0 || end < 0 || end <= start + 1) return -1;
		if (int.TryParse(selector.Substring(start + 1, end - start - 1), out int n))
		{
			return n;
		}
		return -1;
	}

	/// <summary>
	/// M6：在当前 working tree 创建一个新的 stash（不传 message 就用 git 默认 "WIP on &lt;branch&gt;: ..."）。
	/// 对应 <c>git stash push -m &lt;msg&gt;</c>（git ≥ 2.13 用 push；老版本用 save；本仓库假定 ≥ 2.13）。
	/// </summary>
	/// <returns>true 表示成功；false 表示 git 报告"没有可 stash 的改动"（退出码 0 但 stdout 为空）。</returns>
	public bool StashPush(string? message)
	{
		EnsureNotDisposed();
		// 用 --include-untracked 把 untracked 文件也暂存，与 WPF 原版行为一致
		try
		{
			string stdout = RunGit("stash-push", "stash", "push", "--include-untracked",
				string.IsNullOrWhiteSpace(message) ? "" : message!.Trim());
			return !string.IsNullOrEmpty(stdout);
		}
		catch (GitRepositoryException ex)
		{
			// "No local changes to save" → git 退出码 1 + stderr 含此字符串
			if (ex.Message.Contains("No local changes", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			throw;
		}
	}

	/// <summary>
	/// M6：把 <paramref name="reflogSelector"/> 指向的 stash 应用回 working tree，但保留 stash。
	/// 对应 <c>git stash apply &lt;selector&gt;</c>。
	/// </summary>
	/// <exception cref="GitRepositoryException">git 进程失败（含冲突等）。</exception>
	public void StashApply(string reflogSelector)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(reflogSelector))
		{
			throw new ArgumentException("reflogSelector 不能为空", nameof(reflogSelector));
		}
		RunGit("stash-apply", "stash", "apply", reflogSelector.Trim());
	}

	/// <summary>
	/// M6：把 <paramref name="reflogSelector"/> 指向的 stash 应用回 working tree，并从 stash 栈中移除。
	/// 对应 <c>git stash pop &lt;selector&gt;</c>。
	/// </summary>
	/// <exception cref="GitRepositoryException">git 进程失败（含冲突等）。</exception>
	public void StashPop(string reflogSelector)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(reflogSelector))
		{
			throw new ArgumentException("reflogSelector 不能为空", nameof(reflogSelector));
		}
		RunGit("stash-pop", "stash", "pop", reflogSelector.Trim());
	}

	/// <summary>
	/// M6：从 stash 栈中丢弃 <paramref name="reflogSelector"/> 指向的条目。
	/// 对应 <c>git stash drop &lt;selector&gt;</c>。
	/// </summary>
	/// <exception cref="GitRepositoryException">git 进程失败。</exception>
	public void StashDrop(string reflogSelector)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(reflogSelector))
		{
			throw new ArgumentException("reflogSelector 不能为空", nameof(reflogSelector));
		}
		RunGit("stash-drop", "stash", "drop", reflogSelector.Trim());
	}

	/// <summary>
	/// M6：拿到某 stash 相对其 base commit 的 unified diff（整次 stash 的所有文件）。
	/// 对应 <c>git stash show -p --no-color &lt;selector&gt;</c>。空 stash 返回空 DiffResult。
	/// </summary>
	/// <exception cref="ArgumentException">selector 为空。</exception>
	/// <exception cref="GitRepositoryException">git 失败。</exception>
	public DiffResult GetStashDiff(string reflogSelector)
	{
		EnsureNotDisposed();
		if (string.IsNullOrWhiteSpace(reflogSelector))
		{
			throw new ArgumentException("reflogSelector 不能为空", nameof(reflogSelector));
		}
		string raw = RunGit("stash-show", "stash", "show", "-p", "--no-color", reflogSelector.Trim());
		if (string.IsNullOrWhiteSpace(raw))
		{
			return new DiffResult("a/" + reflogSelector, "b/" + reflogSelector, Array.Empty<DiffLine>());
		}
		return UnifiedDiffParser.Parse(raw,
			fallbackOldPath: "a/" + reflogSelector,
			fallbackNewPath: "b/" + reflogSelector);
	}

	private static WorkingTreeChange[] ParsePorcelainStatus(string raw)
	{
		// -z 模式下：
		//  普通条目用 '\0' 分隔，重命名/复制条目用 '\0' 分隔 "old\0new" 两段
		string[] entries = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
		var list = new List<WorkingTreeChange>(entries.Length);
		for (int i = 0; i < entries.Length; i++)
		{
			string entry = entries[i];
			if (entry.Length < 3) continue; // 至少 "XY " + 1 字符
			char x = entry[0];
			char y = entry[1];
			// 第 3 个字符是空格；空格后是文件名（untruncated） 或 "old -> new"
			string rest = entry.Substring(3);

			WorkingTreeStatusKind kind;
			bool staged = x != ' ' && x != '?';
			bool unstaged = y != ' ' && y != '?';
			string? oldPath = null;
			string path = rest;

			// 重命名 / 复制：rest 形如 "old -> new"，且下一条 entry 包含目标路径
			if ((y == 'R' || y == 'C' || x == 'R' || x == 'C') && rest.Contains(" -> "))
			{
				int sep = rest.IndexOf(" -> ", StringComparison.Ordinal);
				oldPath = rest.Substring(0, sep);
				// 在 -z 模式下，目标路径是单独的下一段
				if (i + 1 < entries.Length)
				{
					path = entries[++i];
				}
				else
				{
					path = oldPath;
				}
				kind = (y == 'R' || x == 'R') ? WorkingTreeStatusKind.Renamed : WorkingTreeStatusKind.Copied;
			}
			else if (x == '?' && y == '?')
			{
				kind = WorkingTreeStatusKind.Untracked;
				staged = false;
				unstaged = true;
			}
			else if (x == 'A' || y == 'A' || (x != ' ' && x != '?' && x != 'M' && x != 'D' && x != 'R' && x != 'C'))
			{
				// A 出现在 X 或 Y 上都算 Added
				kind = WorkingTreeStatusKind.Added;
			}
			else if (x == 'D' || y == 'D')
			{
				kind = WorkingTreeStatusKind.Deleted;
			}
			else
			{
				kind = WorkingTreeStatusKind.Modified;
			}
			list.Add(new WorkingTreeChange(path, kind, staged, unstaged, oldPath));
		}
		return list.ToArray();
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

	/// <summary>
	/// 在仓库目录下 spawn <c>git</c>，捕获 stdout；stderr 异常时附在错误信息后。
	/// 解析 git 路径：优先 <see cref="ServiceLocator.GitPathResolver"/>，否则默认 <c>git</c>。
	/// </summary>
	private string RunGit(string forErrorContext, params string[] args)
	{
		string git = ServiceLocator.IsInitialized && ServiceLocator.GitPathResolver != null
			? ServiceLocator.GitPathResolver.GitPath
			: "git";

		var psi = new ProcessStartInfo
		{
			FileName = git,
			WorkingDirectory = _repoPath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		foreach (string a in args)
		{
			psi.ArgumentList.Add(a);
		}

		using var proc = Process.Start(psi);
		if (proc == null)
		{
			throw new GitRepositoryException($"无法启动 '{git}' 进程（{forErrorContext}）");
		}
		string stdout = proc.StandardOutput.ReadToEnd();
		string stderr = proc.StandardError.ReadToEnd();
		proc.WaitForExit();
		if (proc.ExitCode != 0)
		{
			throw new GitRepositoryException(
				$"git {string.Join(" ", args)} 失败 (exit {proc.ExitCode})：{stderr.Trim()}");
		}
		return stdout;
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
