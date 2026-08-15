using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ForkPlus.Avalonia.Graph;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M2 提交图：lane 颜色 → 装饰标签的"浅底"（20% 不透明度背景）。
/// 让 ref 装饰看起来和线条对得上色，又不刺眼（参考 WPF v3.9.0 截图 03）。
/// </summary>
public sealed class LaneColorToFaintBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
        {
            return new SolidColorBrush(Color.FromArgb(0x33, c.R, c.G, c.B));
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// M2 提交图：lane 颜色 → 装饰标签的"深色文字"。颜色太浅时用白色，否则用 lane 颜色本身。
/// </summary>
public sealed class LaneColorToTextBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
        {
            // 用亮度阈值（HSV 的 V）决定文字色：太浅就用深灰，否则用原色
            double brightness = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
            if (brightness > 180)
            {
                return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            }
            return new SolidColorBrush(c);
        }
        return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// M2 提交图：author 字符串 → 一致 <see cref="IBrush"/>。颜色由
/// <see cref="AuthorColorService.GetColor"/> 决定（按 FNV-1a 哈希到 HSV）。
/// </summary>
public sealed class AuthorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? author = value as string;
        Color c = AuthorColorService.GetColor(author);
        return new SolidColorBrush(c);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
