using System;
using System.Runtime.InteropServices;
using ForkPlus.Biturbo;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M2 垂直切片：BtCommitStorage → GitCommit[] 的本地 marshaling。
/// 对标原 WPF <c>ForkPlus.Git.Commands.GetRevisionStorageGitCommand.IntoRevisionStorage</c>
/// + <c>bt_get_revision_headers</c> 的两步查询（先拓扑，再补 subject/author/time）。
///
/// biturbo 数据约定（来自 biturbo 仓库 docs / 原 ForkPlus/BiturboExtensions.cs）：
/// - <see cref="BtCommitStorage.oids"/>：扁平 <c>BtOid[]</c>，按提交顺序串联。
///   每个 commit 占一段：[self, parent1, parent2, ...]，最后一个 commit 延伸至末尾。
/// - <see cref="BtCommitStorage.indexes"/>：<c>uint[]</c>，每个 commit 的起始偏移（= self 在 oids 里的下标）。
/// - <see cref="BtCommitStorage.has_more"/>：是否还有下一页（>0 表示是）。
///
/// 不重用 ForkPlus.Git.RevisionStorage 是因为后者对 RevisionGraph / 父链做了复杂封装，本任务（commit 列表）
/// 只需 self.sha 即可。
/// </summary>
internal static class BtCommitStorageExtensions
{
	/// <summary>
	/// 从扁平 oids + indexes 解出每个 commit 的 self SHA（40 字符小写十六进制）。
	/// 返回数组长度 == <paramref name="storage"/>.indexes_len。
	/// </summary>
	public static string[] ExtractCommitShas(this BtCommitStorage storage)
	{
		long indexesLen = storage.indexes_len;
		if (indexesLen <= 0 || storage.oids == IntPtr.Zero)
		{
			return Array.Empty<string>();
		}

		int oidCount = (int)storage.oids_len;
		int oidSize = Marshal.SizeOf<BtOid>();
		// 一次拷出所有 oids，再按 indexes 切段取 self
		var allOids = new BtOid[oidCount];
		for (int i = 0; i < oidCount; i++)
		{
			IntPtr p = new IntPtr(storage.oids.ToInt64() + (long)i * oidSize);
			allOids[i] = Marshal.PtrToStructure<BtOid>(p);
		}

		int[] indexesRaw = new int[indexesLen];
		Marshal.Copy(storage.indexes, indexesRaw, 0, (int)indexesLen);
		var indexes = new uint[indexesLen];
		for (int i = 0; i < indexesLen; i++)
		{
			indexes[i] = unchecked((uint)indexesRaw[i]);
		}

		var shas = new string[indexesLen];
		for (int i = 0; i < indexesLen; i++)
		{
			shas[i] = allOids[indexes[i]].ToHex();
		}
		return shas;
	}

	/// <summary>将 5 个 dword 序列化为 40 字符小写十六进制 SHA。</summary>
	private static string ToHex(this BtOid oid)
	{
		return string.Concat(
			oid.s0.ToString("x8"),
			oid.s1.ToString("x8"),
			oid.s2.ToString("x8"),
			oid.s3.ToString("x8"),
			oid.s4.ToString("x8"));
	}
}
