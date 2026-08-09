using System.Collections.Generic;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Chart
{
	/// <summary>
	/// 轻量绘图数据模型，刻意与 OxyPlot 的 PlotModel / Series / PieSlice 数据结构同构，
	/// 以便生产迁移时可近乎 1:1 翻译 ForkPlus/UI/UserControls/StatisticsUserControl.PlotHelper
	/// （原工程用 OxyPlot.Wpf 的 Plot 控件承载这些模型）。
	///
	/// 关键差异：官方 oxyplot 仓库（Source/）仅提供 Wpf / WindowsForms / SkiaSharp / ImageSharp / Pdf
	/// 渲染后端，<b>没有</b>第一方 OxyPlot.Avalonia 包；且其对 Avalonia 12 / net10.0 的兼容性无法在
	/// 本环境验证。因此这里用 Avalonia 原生 DrawingContext 渲染，零外部依赖、跨平台必过，
	/// 数据层（本文件）保持与 OxyPlot 同构，渲染层（AvaloniaChart）替换原 WPF 的 Plot 控件。
	/// </summary>

	/// <summary>数据点（对标 OxyPlot.DataPoint）。</summary>
	public readonly struct DataPoint
	{
		public double X { get; }
		public double Y { get; }
		public DataPoint(double x, double y) { X = x; Y = y; }
	}

	/// <summary>所有序列的基类（对标 OxyPlot.Series）。</summary>
	public abstract class Series
	{
		public string Title { get; set; } = "";
		public Color Color { get; set; } = Colors.SteelBlue;
	}

	/// <summary>折线序列（对标 OxyPlot.LineSeries）。</summary>
	public class LineSeries : Series
	{
		public List<DataPoint> Points { get; } = new List<DataPoint>();
	}

	/// <summary>柱状序列（对标 OxyPlot.BarSeries，X=类目索引，Y=值）。</summary>
	public class BarSeries : Series
	{
		public List<double> Values { get; } = new List<double>();
		public List<string> Categories { get; } = new List<string>();
	}

	/// <summary>饼图扇区（对标 OxyPlot.PieSlice）。</summary>
	public class PieSlice
	{
		public string Label { get; }
		public double Value { get; }
		public Color Fill { get; }
		public PieSlice(string label, double value, Color fill)
		{
			Label = label;
			Value = value;
			Fill = fill;
		}
	}

	/// <summary>饼图序列（对标 OxyPlot.PieSeries）。</summary>
	public class PieSeries : Series
	{
		public List<PieSlice> Slices { get; } = new List<PieSlice>();
	}

	/// <summary>绘图模型（对标 OxyPlot.PlotModel）。</summary>
	public class PlotModel
	{
		public string Title { get; set; } = "";
		public Color Background { get; set; } = Colors.Transparent;
		public List<Series> Series { get; } = new List<Series>();
	}
}
