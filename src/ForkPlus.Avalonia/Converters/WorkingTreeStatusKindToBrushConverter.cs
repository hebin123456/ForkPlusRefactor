using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M4：把 <see cref="WorkingTreeStatusKind"/> 映射到用作 "状态徽章" 的 <see cref="IBrush"/>。
/// 颜色对标原 ForkPlus WPF "UnversionedControl" / "ModifiedControl" 的刷子：
/// <list type="bullet">
///   <item>Modified  → 橙 (#E0A060)</item>
///   <item>Added     → 绿 (#3FA34D)</item>
///   <item>Deleted   → 红 (#D24F4F)</item>
///   <item>Renamed   → 蓝 (#4F8FD2)</item>
///   <item>Copied    → 青 (#4FC2D2)</item>
///   <item>Untracked → 灰 (#808080)</item>
///   <item>Unmodified/未知 → 透明</item>
/// </list>
/// <para>
/// 在 headless 视觉验证里，<see cref="M4WorkingTreeVisualTests"/> 会对每个列表项的
/// 徽章 Border 调 <see cref="Convert"/>，断言真实颜色与本表一致 —— 而不是只截一张图。
/// </para>
/// </summary>
public sealed class WorkingTreeStatusKindToBrushConverter : IValueConverter
{
    private static readonly IBrush ModifiedBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x60));
    private static readonly IBrush AddedBrush    = new SolidColorBrush(Color.FromRgb(0x3F, 0xA3, 0x4D));
    private static readonly IBrush DeletedBrush  = new SolidColorBrush(Color.FromRgb(0xD2, 0x4F, 0x4F));
    private static readonly IBrush RenamedBrush  = new SolidColorBrush(Color.FromRgb(0x4F, 0x8F, 0xD2));
    private static readonly IBrush CopiedBrush   = new SolidColorBrush(Color.FromRgb(0x4F, 0xC2, 0xD2));
    private static readonly IBrush UntrackedBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is WorkingTreeStatusKind k)
        {
            return k switch
            {
                WorkingTreeStatusKind.Modified  => ModifiedBrush,
                WorkingTreeStatusKind.Added     => AddedBrush,
                WorkingTreeStatusKind.Deleted   => DeletedBrush,
                WorkingTreeStatusKind.Renamed   => RenamedBrush,
                WorkingTreeStatusKind.Copied    => CopiedBrush,
                WorkingTreeStatusKind.Untracked => UntrackedBrush,
                _ => Brushes.Transparent,
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("WorkingTreeStatusKindToBrushConverter 仅支持单向绑定");
}
