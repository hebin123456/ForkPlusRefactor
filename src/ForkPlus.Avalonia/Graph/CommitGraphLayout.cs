using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Graph;

/// <summary>
/// M2 提交图布局：把扁平的 <see cref="GitCommit"/> 列表（按时间/拓扑序）摊成可绘制的行数据。
///
/// <para>
/// 对标原 WPF <c>ForkPlus.Git.RevisionGraph</c> + <c>GraphLayout</c> 体系（Avalonia 端
/// 第一次实现，所以参考 git log --graph 的算法：每条 lane 跟踪"它在等哪个 SHA"）。
/// </para>
///
/// <para><b>算法</b>（一过扫描，每行 O(行数)）：
/// </para>
/// <list type="number">
///   <item>维护一组 active lane，每条 lane 记一个"它接下来要接到哪条 commit"。</item>
///   <item>对每个 commit：找一个 lane 正好在等自己 → 该 lane 继续往下走，并把 lane
///         的"等待目标"换成这个 commit 的第一个 parent。</item>
///   <item>找不到 → 这个 commit 是新起点，开一条新 lane（= 追加到末尾）。</item>
///   <item>commit 的非首 parent（merge）→ 各开一条新 lane，从这个 commit 出发去等那个 parent。</item>
///   <item>每个 commit 行画：自己列上的圆点 + 经过该行的所有 lane 的竖线 +
///         merge 时从其他列到本列的折线。</item>
/// </list>
///
/// <para><b>颜色</b>：lane 颜色按"lane 索引 → 调色板"取，颜色稳定（同一个 lane 永远一个色）。
/// 这样 ref 装饰（branch / tag）落在哪条 lane 上，就用那条 lane 的颜色，跟线条对得上。
/// </para>
/// </summary>
public static class CommitGraphLayout
{
	/// <summary>
	/// 调色板：每条 lane 循环取一个。参照 WPF v3.9.0 截图（03-demo-graph.png），
	/// 主分支橙 #E0A060，次分支绿 #3FA34D，标签紫 #9050C0，蓝 #4F8FC0，
	/// 玫红 #C0508C，青 #3FA0A0，棕 #A07A4F，黄 #C0A040，灰蓝 #5F7F9F。
	/// 顺时针循环，相邻 lane 颜色差异够大，便于视觉区分。
	/// </summary>
	public static readonly IReadOnlyList<Color> LanePalette = new[]
	{
		Color.FromRgb(0xE0, 0xA0, 0x60),  // 主线：橙
		Color.FromRgb(0x3F, 0xA3, 0x4D),  // 支线：绿
		Color.FromRgb(0x90, 0x50, 0xC0),  // 紫
		Color.FromRgb(0x4F, 0x8F, 0xC0),  // 蓝
		Color.FromRgb(0xC0, 0x50, 0x8C),  // 玫红
		Color.FromRgb(0x3F, 0xA0, 0xA0),  // 青
		Color.FromRgb(0xA0, 0x7A, 0x4F),  // 棕
		Color.FromRgb(0xC0, 0xA0, 0x40),  // 黄
		Color.FromRgb(0x5F, 0x7F, 0x9F),  // 灰蓝
	};

	public static Color LaneColor(int laneIndex)
	{
		int n = LanePalette.Count;
		int i = ((laneIndex % n) + n) % n;
		return LanePalette[i];
	}

	/// <summary>把 <paramref name="commits"/> 摊成可绘制的行。</summary>
	public static IReadOnlyList<CommitGraphRow> Compute(IReadOnlyList<GitCommit> commits)
	{
		var rows = new List<CommitGraphRow>(commits.Count);
		// active lanes: lanes[i].ExpectedSha 是这条 lane 下一行期待看到的 commit SHA
		// lanes[i].Color 是这条 lane 用的颜色，跨多行保持稳定
		var lanes = new List<LaneInfo>();

		for (int idx = 0; idx < commits.Count; idx++)
		{
			GitCommit commit = commits[idx];

			// 1) 找一条 lane 在等这个 commit
			int col = -1;
			for (int i = 0; i < lanes.Count; i++)
			{
				if (lanes[i].ExpectedSha == commit.Sha)
				{
					col = i;
					break;
				}
			}

			Color myColor;
			if (col < 0)
			{
				// 没 lane 在等 → 这是新起点（最常见：第一个 commit / 来自 merge 的新分支）
				col = lanes.Count;
				Color startColor = LaneColor(col);
				string? firstParent = commit.Parents.Count > 0 ? commit.Parents[0] : null;
				lanes.Add(new LaneInfo { ExpectedSha = firstParent, Color = startColor });
				myColor = startColor;
			}
			else
			{
				// lane 找到了 → 这条 lane 继续往下走，颜色保持
				myColor = lanes[col].Color;
				string? firstParent = commit.Parents.Count > 0 ? commit.Parents[0] : null;
				lanes[col] = new LaneInfo { ExpectedSha = firstParent, Color = myColor };
			}

			// 2) merge: 非首 parent 各开一条新 lane（颜色 = 自己颜色 —— 分叉视觉上从主线出去）
			for (int p = 1; p < commit.Parents.Count; p++)
			{
				Color branchColor = LaneColor(lanes.Count);
				lanes.Add(new LaneInfo { ExpectedSha = commit.Parents[p], Color = branchColor });
			}

			// 3) 构造本行 segments:
			//    - 自身颜色 = myColor（commit dot + 上下半段竖线）
			//    - 每条经过本行的 lane 都要画竖线，颜色取那条 lane 的颜色
			//    - merge 时从其他 lane 折线过来 (horizontal segment from col→other)
			var segments = new List<GraphSegment>(lanes.Count + commit.Parents.Count);
			int rowSelfCol = col;
			// 主竖线：本列在本行 (上半段)
			segments.Add(new GraphSegment(rowSelfCol, rowSelfCol, GraphSegmentKind.VerticalSelf, myColor));
			// 经过本行的其他 lane：每条画一条竖线
			for (int i = 0; i < lanes.Count; i++)
			{
				if (i == rowSelfCol) continue;
				segments.Add(new GraphSegment(i, i, GraphSegmentKind.VerticalOther, lanes[i].Color));
			}
			// merge 折线：从父列画到本列（水平 + 拐点）
			for (int p = 1; p < commit.Parents.Count; p++)
			{
				int parentCol = lanes.Count - (commit.Parents.Count - 1 - p) - 1;
				// 上面新加的 lane 顺序是 [p=1, p=2, ...]，因此父 lane 索引
				// = lanes.Count - (commit.Parents.Count - p)  (在本 commit 处理完后)
				// 因为 lanes 已经追加了所有 parent，我们重新算：
				parentCol = FindParentLaneIndex(lanes, commit.Parents[p], rowSelfCol);
				if (parentCol >= 0 && parentCol != rowSelfCol)
				{
					segments.Add(new GraphSegment(parentCol, rowSelfCol,
						GraphSegmentKind.MergeEdge, lanes[parentCol].Color));
				}
			}

			// 4) decoration
			var decorations = commit.RefDecorations
				.Select(name => new DecorationLabel(name, myColor))
				.ToList();

			rows.Add(new CommitGraphRow(commit, rowSelfCol, myColor, segments, decorations));
		}

		return rows;
	}

	private static int FindParentLaneIndex(List<LaneInfo> lanes, string parentSha, int excludeCol)
	{
		for (int i = 0; i < lanes.Count; i++)
		{
			if (i == excludeCol) continue;
			if (lanes[i].ExpectedSha == parentSha) return i;
		}
		return -1;
	}

	private struct LaneInfo
	{
		public string? ExpectedSha;
		public Color Color;
	}
}

/// <summary>提交图中的一行（一个 commit）—— 包含布局好的所有可绘制元素。</summary>
public sealed class CommitGraphRow
{
	public CommitGraphRow(
		GitCommit commit,
		int commitColumn,
		Color commitColor,
		IReadOnlyList<GraphSegment> segments,
		IReadOnlyList<DecorationLabel> decorations)
	{
		Commit = commit;
		CommitColumn = commitColumn;
		CommitColor = commitColor;
		Segments = segments;
		Decorations = decorations;
	}

	public GitCommit Commit { get; }
	public int CommitColumn { get; }
	public Color CommitColor { get; }
	public IReadOnlyList<GraphSegment> Segments { get; }
	public IReadOnlyList<DecorationLabel> Decorations { get; }
}

/// <summary>一行中要画的一条线段。</summary>
public sealed class GraphSegment
{
	public GraphSegment(int fromColumn, int toColumn, GraphSegmentKind kind, Color color)
	{
		FromColumn = fromColumn;
		ToColumn = toColumn;
		Kind = kind;
		Color = color;
	}
	public int FromColumn { get; }
	public int ToColumn { get; }
	public GraphSegmentKind Kind { get; }
	public Color Color { get; }
}

public enum GraphSegmentKind
{
	/// <summary>本列的竖线（commit dot 上下半段）。</summary>
	VerticalSelf,
	/// <summary>其他 lane 经过本行的竖线。</summary>
	VerticalOther,
	/// <summary>merge 时从父 lane 折线到本行 commit。</summary>
	MergeEdge,
}

/// <summary>commit 图左侧的 ref 装饰标签（如 <c>master</c>、<c>origin/master</c>、<c>v1.0</c>）。</summary>
public sealed class DecorationLabel
{
	public DecorationLabel(string text, Color laneColor)
	{
		Text = text;
		LaneColor = laneColor;
	}
	public string Text { get; }
	public Color LaneColor { get; }
}
