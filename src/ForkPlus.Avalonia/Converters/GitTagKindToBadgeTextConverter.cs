using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M7：把 <c>GitTag.IsAnnotated</c> (bool) 映射到徽章单字符字形：
/// <list type="bullet">
///   <item><c>true</c> (annotated) → "a"</item>
///   <item><c>false</c> (lightweight) → "l"</item>
/// </list>
/// 与 <see cref="GitTagKindToBrushConverter"/> 配对，给 ListBox 行徽章用纯文本字形（避免位图跨平台问题）。
/// </summary>
public sealed class GitTagKindToBadgeTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAnnotated)
        {
            return isAnnotated ? "a" : "l";
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("GitTagKindToBadgeTextConverter 只支持单向绑定");
    }
}
