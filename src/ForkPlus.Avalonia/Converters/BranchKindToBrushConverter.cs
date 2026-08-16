using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M8：把 <see cref="BranchKind"/> 映射到徽章背景色：
/// <list type="bullet">
///   <item><c>Group</c> → 透明（组节点不显徽章，靠"本地 (N)"等文字区分）</item>
///   <item><c>Local</c> → <c>#4A90E2</c>（蓝色，对应 WPF 原版侧栏本地分支的蓝色）</item>
///   <item><c>Remote</c> → <c>#7B68A6</c>（紫色，对应 WPF 原版侧栏远程分支的紫色）</item>
/// </list>
/// </summary>
public sealed class BranchKindToBrushConverter : IValueConverter
{
    private static readonly IBrush LocalBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2));
    private static readonly IBrush RemoteBrush = new SolidColorBrush(Color.FromRgb(0x7B, 0x68, 0xA6));
    private static readonly IBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is BranchKind k)
        {
            return k switch
            {
                BranchKind.Local => LocalBrush,
                BranchKind.Remote => RemoteBrush,
                _ => TransparentBrush,
            };
        }
        return TransparentBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BranchKindToBrushConverter 只支持单向绑定");
    }
}
