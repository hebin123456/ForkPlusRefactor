using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ForkPlus.Biturbo;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M2 垂直切片：BtCommitStorage → GitCommit[] 的本地 marshaling。
/// 对标原 WPF <c>ForkPlus.Git.Commands.GetRevisionStorageGitCommand.IntoRevisionStorage</c>
/// + <c>bt_get_revision_headers</c> 的两步查询（先拓扑，再补 subject/author/time）。
///
/// <para>
/// biturbo 数据约定（来自 biturbo 仓库 docs / 原 ForkPlus/BiturboExtensions.cs）：
/// </para>
/// <list type="bullet">
///   <item><see cref="BtCommitStorage.oids"/>：扁平 <c>BtOid[]</c>，按提交顺序串联。
///         每个 commit 占一段：<c>[self, parent1, parent2, ...]</c>，每段长度由相邻 indexes 差决定。</item>
///   <item><see cref="BtCommitStorage.indexes"/>：<c>uint[]</c>，每个 commit 的起始偏移（= self 在 oids 里的下标）。</item>
///   <item><see cref="BtCommitStorage.has_more"/>：是否还有下一页（>0 表示是）。</item>
/// </list>
///
/// <para>
/// 不重用 ForkPlus.Git.RevisionStorage 是因为后者对 RevisionGraph / 父链做了复杂封装，本任务
/// 只需要 self.sha + parents 即可。
/// </para>
/// </summary>
internal static class BtCommitStorageExtensions
{
	/// <summary>
	/// 从扁平 oids + indexes 解出每个 commit 的 self SHA（40 字符小写十六进制）。
	/// 返回数组长度 == <paramref name="storage"/>.indexes_len。
	/// </summary>
	public static string[] ExtractCommitShas(this BtCommitStorage storage)
	{
		BtOid[] allOids = storage.MarshallAllOids(out int oidCount);
		if (oidCount == 0) return Array.Empty<string>();

		uint[] indexes = storage.MarshallIndexes();
		if (indexes.Length == 0) return Array.Empty<string>();

		var shas = new string[indexes.Length];
		for (int i = 0; i < indexes.Length; i++)
		{
			shas[i] = allOids[indexes[i]].ToHex();
		}
		return shas;
	}

	/// <summary>
	/// 一次性把每个 commit 的 self + parents 全部解出（自上而下、自新到旧）。
	/// <c>result[i]</c> 是 SHA 列表，索引 0 是 self，后续是 parents（按 biturbo 顺序）。
	/// </summary>
	public static IReadOnlyList<string[]> ExtractCommitShasAndParents(this BtCommitStorage storage)
	{
		BtOid[] allOids = storage.MarshallAllOids(out int oidCount);
		if (oidCount == 0) return Array.Empty<string[]>();

		uint[] indexes = storage.MarshallIndexes();
		if (indexes.Length == 0) return Array.Empty<string[]>();

		var result = new string[indexes.Length][];
		for (int i = 0; i < indexes.Length; i++)
		{
			uint selfIdx = indexes[i];
			uint nextIdx = (i + 1 < indexes.Length) ? indexes[i + 1] : (uint)oidCount;
			int segLen = (int)(nextIdx - selfIdx);
			if (segLen <= 0) segLen = 1; // 至少 self
			var seg = new string[segLen];
			for (int j = 0; j < segLen; j++)
			{
				seg[j] = allOids[selfIdx + j].ToHex();
			}
			result[i] = seg;
		}
		return result;
	}

	private static BtOid[] MarshallAllOids(this BtCommitStorage storage, out int oidCount)
	{
		oidCount = 0;
		if (storage.oids == IntPtr.Zero) return Array.Empty<BtOid>();
		oidCount = (int)storage.oids_len;
		if (oidCount <= 0) return Array.Empty<BtOid>();
		int oidSize = Marshal.SizeOf<BtOid>();
		var allOids = new BtOid[oidCount];
		for (int i = 0; i < oidCount; i++)
		{
			IntPtr p = new IntPtr(storage.oids.ToInt64() + (long)i * oidSize);
			allOids[i] = Marshal.PtrToStructure<BtOid>(p);
		}
		return allOids;
	}

	private static uint[] MarshallIndexes(this BtCommitStorage storage)
	{
		long indexesLen = storage.indexes_len;
		if (indexesLen <= 0 || storage.indexes == IntPtr.Zero) return Array.Empty<uint>();
		int[] indexesRaw = new int[indexesLen];
		Marshal.Copy(storage.indexes, indexesRaw, 0, (int)indexesLen);
		var indexes = new uint[indexesLen];
		for (int i = 0; i < indexesLen; i++)
		{
			indexes[i] = unchecked((uint)indexesRaw[i]);
		}
		return indexes;
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
