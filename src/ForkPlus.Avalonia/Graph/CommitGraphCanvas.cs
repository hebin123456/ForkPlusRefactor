using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Graph;

/// <summary>
/// M2 提交图绘制控件：单行的"提交图格子"。
///
/// <para>
/// 绘制三件事：
/// </para>
/// <list type="bullet">
///   <item>经过本行的所有 lane 的竖线（中央点上下各半段，竖直贯穿）</item>
///   <item>本列 commit 的圆点（带黑边，commit color 填充）</item>
///   <item>merge 时从其他列折线过来（在 commit 点高度处画一条水平线，再回到
///         父列的下半段）</item>
/// </list>
///
/// <para>
/// 自身大小由 <see cref="ColumnWidth"/> ×（最大列数+1）和固定行高决定。数据通过
/// <see cref="Row"/> 注入；不设 Row 时为空（占位用）。
/// </para>
/// </summary>
public sealed class CommitGraphCanvas : Control
{
	/// <summary>每条 lane 横向占多少 px。</summary>
	public static readonly StyledProperty<double> ColumnWidthProperty =
		AvaloniaProperty.Register<CommitGraphCanvas, double>(nameof(ColumnWidth), 12.0);

	/// <summary>行高（commit 圆点中心 = 行高/2）。</summary>
	public static readonly StyledProperty<double> RowHeightProperty =
		AvaloniaProperty.Register<CommitGraphCanvas, double>(nameof(RowHeight), 24.0);

	/// <summary>commit 圆点半径。</summary>
	public static readonly StyledProperty<double> DotRadiusProperty =
		AvaloniaProperty.Register<CommitGraphCanvas, double>(nameof(DotRadius), 4.0);

	/// <summary>本行要绘制的图数据（null = 空行）。</summary>
	public static readonly StyledProperty<CommitGraphRow?> RowProperty =
		AvaloniaProperty.Register<CommitGraphCanvas, CommitGraphRow?>(nameof(Row));

	public double ColumnWidth
	{
		get => GetValue(ColumnWidthProperty);
		set => SetValue(ColumnWidthProperty, value);
	}
	public double RowHeight
	{
		get => GetValue(RowHeightProperty);
		set => SetValue(RowHeightProperty, value);
	}
	public double DotRadius
	{
		get => GetValue(DotRadiusProperty);
		set => SetValue(DotRadiusProperty, value);
	}
	public CommitGraphRow? Row
	{
		get => GetValue(RowProperty);
		set => SetValue(RowProperty, value);
	}

	static CommitGraphCanvas()
	{
		// 让 Control 知道我们支持 StyledProperty（强制重新 render）
		AffectsRender<CommitGraphCanvas>(RowProperty, ColumnWidthProperty, RowHeightProperty, DotRadiusProperty);
		AffectsMeasure<CommitGraphCanvas>(RowProperty, ColumnWidthProperty, RowHeightProperty, DotRadiusProperty);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		CommitGraphRow? row = Row;
		int maxCol = row != null ? row.CommitColumn : 0;
		if (row != null)
		{
			foreach (var seg in row.Segments)
			{
				if (seg.FromColumn > maxCol) maxCol = seg.FromColumn;
				if (seg.ToColumn > maxCol) maxCol = seg.ToColumn;
			}
		}
		double w = (maxCol + 1) * ColumnWidth + 4;
		double h = RowHeight;
		return new Size(w, h);
	}

	public override void Render(DrawingContext context)
	{
		CommitGraphRow? row = Row;
		if (row == null) return;

		double w = Bounds.Width;
		double h = Bounds.Height;
		double midY = h / 2.0;
		double lineThickness = 1.5;
		var lineCap = PenLineCap.Round;

		// 1) 经过本行的所有 lane 的竖线
		foreach (var seg in row.Segments)
		{
			if (seg.Kind == GraphSegmentKind.VerticalSelf || seg.Kind == GraphSegmentKind.VerticalOther)
			{
				double x = ColumnToX(seg.FromColumn, w, row);
				var brush = new SolidColorBrush(seg.Color);
				var pen = new Pen(brush, lineThickness, null, lineCap);
				context.DrawLine(pen, new Point(x, 0), new Point(x, h));
			}
		}

		// 2) merge 折线（水平 + 下半段竖线）
		foreach (var seg in row.Segments)
		{
			if (seg.Kind == GraphSegmentKind.MergeEdge)
			{
				double fromX = ColumnToX(seg.FromColumn, w, row);
				double toX = ColumnToX(seg.ToColumn, w, row);
				var brush = new SolidColorBrush(seg.Color);
				var pen = new Pen(brush, lineThickness, null, lineCap);
				// 水平线（在 commit 点高度）
				context.DrawLine(pen, new Point(fromX, midY), new Point(toX, midY));
				// from 端下半段：让父 lane 继续往下走（到本行底）
				context.DrawLine(pen, new Point(fromX, midY), new Point(fromX, h));
			}
		}

		// 3) commit 圆点（黑边 + commit color 填充）
		double dotX = ColumnToX(row.CommitColumn, w, row);
		double dotY = midY;
		var fill = new SolidColorBrush(row.CommitColor);
		var outline = new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), 1.0);
		context.DrawEllipse(fill, outline, new Point(dotX, dotY), DotRadius, DotRadius);
	}

	private double ColumnToX(int col, double width, CommitGraphRow row)
	{
		// 把 commit 列放在控件中央，其他 lane 在左侧
		// 简单策略：所有列等间距，从左到右 0,1,2,...，最后 + padding
		// commit 列不强制居中（多列时居中会让 fold 不直观）
		return (col + 0.5) * ColumnWidth + 2;
	}
}
