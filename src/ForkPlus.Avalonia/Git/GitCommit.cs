using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M2 提交记录视图模型 + 提交图（graph）数据。
/// 对标原 WPF <c>ForkPlus.Git.Revision</c> 类的只读子集。
///
/// <para>
/// 不只是一个"commit 列表行"了 —— 现在还携带了
/// <see cref="Parents"/>（用于画 DAG 连线）和 <see cref="RefDecorations"/>
/// （画在提交图左侧的 branch/tag/stash 标签）。
/// </para>
/// </summary>
public sealed class GitCommit
{
	public GitCommit(
		string sha,
		string subject,
		string author,
		DateTimeOffset authorTime,
		IReadOnlyList<string>? parents = null,
		IReadOnlyList<string>? refDecorations = null)
	{
		Sha = sha ?? throw new ArgumentNullException(nameof(sha));
		ShortSha = sha.Length >= 7 ? sha.Substring(0, 7) : sha;
		Subject = subject ?? string.Empty;
		Author = author ?? string.Empty;
		AuthorTime = authorTime;
		Parents = parents ?? Array.Empty<string>();
		RefDecorations = refDecorations ?? Array.Empty<string>();
	}

	/// <summary>40 字符小写十六进制 SHA。</summary>
	public string Sha { get; }

	/// <summary>短 SHA（git 默认 7 字符）。</summary>
	public string ShortSha { get; }

	/// <summary>提交主题（subject，提交信息第一行）。</summary>
	public string Subject { get; }

	/// <summary>作者名（identities[author_index]）。</summary>
	public string Author { get; }

	/// <summary>作者时间（UTC），由 biturbo 的 unix 秒数转回。</summary>
	public DateTimeOffset AuthorTime { get; }

	/// <summary>
	/// 父提交的 SHA 列表（按 biturbo oids 顺序）。
	/// 0 个 = 仓库根提交；1 个 = 普通；2 个 = merge。
	/// </summary>
	public IReadOnlyList<string> Parents { get; }

	/// <summary>
	/// 指向此 commit 的 ref 装饰标签（去重后的 ref 短名），例如
	/// <c>["master", "origin/master", "v1.0"]</c>。M2 提交图左侧那一列就是它。
	/// </summary>
	public IReadOnlyList<string> RefDecorations { get; }

	/// <summary>UI 行展示用：<c>shortSha  subject  — author  (date)</c>。</summary>
	public string DisplayLine => $"{ShortSha}  {Subject}  — {Author}  ({AuthorTime.LocalDateTime:yyyy-MM-dd HH:mm})";
}
