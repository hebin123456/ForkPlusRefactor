using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M8：把 <see cref="BranchKind"/> 映射到徽章字形（"L" / "R" / "G"），跟 WPF 原版图标语义一致。
/// Group 节点（"本地" / "远程"） → 空字符串（不显徽章）。
/// </summary>
public sealed class BranchKindToBadgeTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BranchKind k)
        {
            return k switch
            {
                BranchKind.Local => "L",
                BranchKind.Remote => "R",
                BranchKind.Group => "",
                _ => "?",
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BranchKindToBadgeTextConverter 只支持单向绑定");
    }
}

/// <summary>
/// M8：把 <c>bool IsCurrent</c> 映射到"▶"标记（当前 HEAD 指向的分支）。
/// WPF 原版用粗体 + ▶ 标记当前分支；Avalonia 用 FontWeight + 前缀字符。
/// </summary>
public sealed class BranchCurrentToMarkerConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrent && isCurrent)
        {
            return "▶";
        }
        return "  "; // 两个空格的缩进，与 ▶ 等宽
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BranchCurrentToMarkerConverter 只支持单向绑定");
    }
}

/// <summary>
/// M8：把 <c>bool IsCurrent</c> 映射到字体粗细：当前分支 Bold，其他 Normal。
/// </summary>
public sealed class BranchCurrentToFontWeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrent && isCurrent)
        {
            return FontWeight.Bold;
        }
        return FontWeight.Normal;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BranchCurrentToFontWeightConverter 只支持单向绑定");
    }
}
