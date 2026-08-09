using System.Collections.Generic;
using Avalonia;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 按行给 <see cref="DiffLineKind"/> 上色：新增行浅绿、删除行浅红（对标 ForkPlus
/// 在 AvalonEdit 里使用的 <c>AddedBrush</c> / <c>RemovedBrush</c>）。
/// 通过 <see cref="DocumentColorizingTransformer"/> 在渲染期作用于每一行背景。
/// </summary>
public sealed class DiffColorizer : DocumentColorizingTransformer
{
    private static readonly Brush AddedBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xFF, 0xE6));
    private static readonly Brush RemovedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xE6, 0xE6));

    private readonly List<DiffLine> _lines;

    public DiffColorizer(List<DiffLine> lines) => _lines = lines;

    protected override void ColorizeLine(DocumentLine line)
    {
        int idx = line.LineNumber - 1;
        if (idx < 0 || idx >= _lines.Count)
            return;

        Brush? brush = _lines[idx].Kind switch
        {
            DiffLineKind.Added => AddedBrush,
            DiffLineKind.Removed => RemovedBrush,
            _ => null,
        };
        if (brush is not null)
            ChangeLineBackground(line, brush);
    }
}
