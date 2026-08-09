using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Avalonia.Chart;

namespace ForkPlus.Avalonia.Chart
{
	/// <summary>
	/// 用 Avalonia 原生 DrawingContext 渲染 PlotModel 的控件，替换原 WPF 工程的 OxyPlot.Wpf Plot 控件。
	/// 支持折线（LineSeries）、柱状（BarSeries）、饼图（PieSeries）三种序列。
	/// 零外部依赖，跨平台（Windows/Linux/macOS）一致渲染。
	/// </summary>
	public class AvaloniaChart : Control
	{
		public static readonly DirectProperty<AvaloniaChart, PlotModel> ModelProperty =
			AvaloniaProperty.RegisterDirect<AvaloniaChart, PlotModel>(
				nameof(Model),
				o => o.Model,
				(o, v) => o.Model = v);

		public PlotModel Model
		{
			get => _model;
			set => SetAndRaise(ModelProperty, ref _model, value);
		}
		private PlotModel _model;

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ModelProperty)
				InvalidateVisual();
		}

		public override void Render(DrawingContext context)
		{
			var size = Bounds.Size;
			if (size.Width <= 0 || size.Height <= 0 || Model == null)
				return;

			// 背景
			if (Model.Background != Colors.Transparent)
				context.FillRectangle(new SolidColorBrush(Model.Background), new Rect(0, 0, size.Width, size.Height));

			// 标题
			if (!string.IsNullOrEmpty(Model.Title))
				context.DrawText(
					new FormattedText(Model.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
						Typeface.Default, 14, Brushes.Black),
					new Point(8, 6));

			if (Model.Series.Count == 0)
				return;

			var series = Model.Series[0];
			if (series is PieSeries pie)
				RenderPie(context, pie, size);
			else if (series is LineSeries line)
				RenderLine(context, line, size);
			else if (series is BarSeries bar)
				RenderBar(context, bar, size);
		}

		private const double PadLeft = 44;
		private const double PadBottom = 28;
		private const double PadTop = 30;
		private const double PadRight = 16;

		private void RenderLine(DrawingContext context, LineSeries series, Size size)
		{
			if (series.Points.Count == 0)
				return;
			var plot = PlotRect(size);
			double xmin = double.MaxValue, xmax = double.MinValue, ymin = double.MaxValue, ymax = double.MinValue;
			foreach (var p in series.Points)
			{
				xmin = Math.Min(xmin, p.X); xmax = Math.Max(xmax, p.X);
				ymin = Math.Min(ymin, p.Y); ymax = Math.Max(ymax, p.Y);
			}
			if (xmax == xmin) xmax = xmin + 1;
			if (ymax == ymin) ymax = ymin + 1;
			double MapX(double x) => plot.X + (x - xmin) / (xmax - xmin) * plot.Width;
			double MapY(double y) => plot.Y + plot.Height - (y - ymin) / (ymax - ymin) * plot.Height;

			// 坐标轴
			var axisPen = new Pen(Brushes.Gray, 1);
			context.DrawLine(axisPen, new Point(plot.X, plot.Y), new Point(plot.X, plot.Y + plot.Height));
			context.DrawLine(axisPen, new Point(plot.X, plot.Y + plot.Height), new Point(plot.X + plot.Width, plot.Y + plot.Height));

			// 折线
			var fig = new PathFigure { StartPoint = new Point(MapX(series.Points[0].X), MapY(series.Points[0].Y)) };
			for (int i = 1; i < series.Points.Count; i++)
				fig.Segments.Add(new LineSegment { Point = new Point(MapX(series.Points[i].X), MapY(series.Points[i].Y)) });
			var pg = new PathGeometry { Figures = { fig } };
			context.DrawGeometry(null, new Pen(new SolidColorBrush(series.Color), 2), pg);

			// 数据点
			foreach (var p in series.Points)
				context.DrawGeometry(new SolidColorBrush(series.Color), null,
					new EllipseGeometry(new Rect(MapX(p.X) - 3, MapY(p.Y) - 3, 6, 6)));
		}

		private void RenderBar(DrawingContext context, BarSeries series, Size size)
		{
			if (series.Values.Count == 0)
				return;
			var plot = PlotRect(size);
			double max = 0;
			foreach (var v in series.Values) max = Math.Max(max, v);
			if (max <= 0) max = 1;

			var axisPen = new Pen(Brushes.Gray, 1);
			context.DrawLine(axisPen, new Point(plot.X, plot.Y + plot.Height), new Point(plot.X + plot.Width, plot.Y + plot.Height));

			int n = series.Values.Count;
			double slot = plot.Width / n;
			double barW = slot * 0.7;
			for (int i = 0; i < n; i++)
			{
				double h = series.Values[i] / max * plot.Height;
				double x = plot.X + i * slot + (slot - barW) / 2;
				double y = plot.Y + plot.Height - h;
				context.FillRectangle(new SolidColorBrush(series.Color), new Rect(x, y, barW, h));
				if (i < series.Categories.Count && !string.IsNullOrEmpty(series.Categories[i]))
					context.DrawText(
						new FormattedText(series.Categories[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
							Typeface.Default, 10, Brushes.Gray),
						new Point(x, plot.Y + plot.Height + 4));
			}
		}

		private void RenderPie(DrawingContext context, PieSeries series, Size size)
		{
			if (series.Slices.Count == 0)
				return;
			double total = 0;
			foreach (var s in series.Slices) total += s.Value;
			if (total <= 0) return;

			var cx = size.Width / 2;
			var cy = (size.Height + PadTop) / 2;
			var r = Math.Min(size.Width - 20, size.Height - PadTop - 20) / 2;
			if (r <= 0) return;

			double angle = -90; // 从正上方开始
			foreach (var slice in series.Slices)
			{
				double sweep = slice.Value / total * 360.0;
				var p0 = Polar(cx, cy, r, angle);
				var p1 = Polar(cx, cy, r, angle + sweep);
				var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
				fig.Segments.Add(new LineSegment { Point = p0 });
				fig.Segments.Add(new ArcSegment
				{
					Point = p1,
					Size = new Size(r, r),
					IsLargeArc = sweep > 180,
					SweepDirection = SweepDirection.Clockwise
				});
				fig.Segments.Add(new LineSegment { Point = new Point(cx, cy) });
				var pg = new PathGeometry { Figures = { fig } };
				context.DrawGeometry(new SolidColorBrush(slice.Fill), null, pg);
				angle += sweep;
			}
		}

		private Rect PlotRect(Size size) =>
			new Rect(PadLeft, PadTop, Math.Max(0, size.Width - PadLeft - PadRight), Math.Max(0, size.Height - PadTop - PadBottom));

		private static Point Polar(double cx, double cy, double r, double angleDeg)
		{
			var a = angleDeg * Math.PI / 180.0;
			return new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
		}
	}
}
