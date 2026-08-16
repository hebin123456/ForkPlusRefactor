using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M7：把 <c>GitTag.IsAnnotated</c> (bool) 映射到徽章背景色：
/// <list type="bullet">
///   <item><c>true</c> (annotated) → <c>#A05F2A</c>（棕色，对应 WPF 原版 Tag 徽章的暖色调）</item>
///   <item><c>false</c> (lightweight) → <c>#6E6E6E</c>（灰色，弱化显示）</item>
/// </list>
/// 必须在 StaticResource 注册时配 <see cref="GitTagKindToBadgeTextConverter"/> 一起用。
/// </summary>
public sealed class GitTagKindToBrushConverter : IValueConverter
{
    private static readonly IBrush AnnotatedBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0x5F, 0x2A));
    private static readonly IBrush LightweightBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAnnotated)
        {
            return isAnnotated ? AnnotatedBrush : LightweightBrush;
        }
        // 非 bool 视为 lightweight（兜底）
        return LightweightBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("GitTagKindToBrushConverter 只支持单向绑定");
    }
}
