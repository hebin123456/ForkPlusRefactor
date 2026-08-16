using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Converters;

/// <summary>
/// M5：把 <see cref="FileTreeNode.FileTreeNodeKind"/> 映射到一个 IBrush
/// （目录 = 浅黄底，文件 = 浅灰底，子模块 = 浅紫底），用作 TreeView 行
/// 左侧"图标徽章"的背景色。
/// <para>
/// 为什么不是直接画图标：Avalonia 的 Headless 测试不能 load 外部图片资源，
/// 用纯色徽章 + 单字符字形 ([D]/[F]/[S]) 既能在 Skia 渲染出像素做视觉断言，
/// 又不依赖图像资源存在性。
/// </para>
/// </summary>
public sealed class FileTreeNodeKindToBrushConverter : IValueConverter
{
    public static readonly FileTreeNodeKindToBrushConverter Instance = new();

    private static readonly IBrush DirectoryBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x9A, 0x50));
    private static readonly IBrush FileBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
    private static readonly IBrush SubmoduleBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x50, 0xC0));
    private static readonly IBrush FallbackBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileTreeNode.FileTreeNodeKind k)
        {
            return k switch
            {
                FileTreeNode.FileTreeNodeKind.Directory => DirectoryBrush,
                FileTreeNode.FileTreeNodeKind.File => FileBrush,
                FileTreeNode.FileTreeNodeKind.Submodule => SubmoduleBrush,
                _ => FallbackBrush,
            };
        }
        return FallbackBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>M5：把 <see cref="FileTreeNode.FileTreeNodeKind"/> 映射到一个 1 字符徽章字形
/// ([D] / [F] / [S])，便于 headless 像素断言（"至少出现一次 D" 之类）。</summary>
public sealed class FileTreeNodeKindToBadgeTextConverter : IValueConverter
{
    public static readonly FileTreeNodeKindToBadgeTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileTreeNode.FileTreeNodeKind k)
        {
            return k switch
            {
                FileTreeNode.FileTreeNodeKind.Directory => "D",
                FileTreeNode.FileTreeNodeKind.File => "F",
                FileTreeNode.FileTreeNodeKind.Submodule => "S",
                _ => "?",
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
