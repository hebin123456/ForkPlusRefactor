using System;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M2 垂直切片：轻量级提交记录视图模型。
/// 对标原 WPF <c>ForkPlus.Git.Revision</c> 类的只读子集（Sha / Subject / Author / AuthorTime），
/// 仅为 Avalonia 端的 commit 列表 UI 服务。后续 M3 接入 <c>FullRevisionDetails</c> 时再扩展
/// （Parents / Tree / Stats / Branch refs）。
/// </summary>
public sealed class GitCommit
{
	public GitCommit(string sha, string subject, string author, DateTimeOffset authorTime)
	{
		Sha = sha ?? throw new ArgumentNullException(nameof(sha));
		ShortSha = sha.Length >= 7 ? sha.Substring(0, 7) : sha;
		Subject = subject ?? string.Empty;
		Author = author ?? string.Empty;
		AuthorTime = authorTime;
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

	/// <summary>UI 行展示用：<c>shortSha  subject  — author  (date)</c>。</summary>
	public string DisplayLine => $"{ShortSha}  {Subject}  — {Author}  ({AuthorTime.LocalDateTime:yyyy-MM-dd HH:mm})";
}
