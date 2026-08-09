using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 基于 <c>Avalonia.AvaloniaEdit</c>（AvalonEdit 的 Avalonia 移植）的差异视图。
/// 对标 ForkPlus 用 <c>ICSharpCode.AvalonEdit</c> 渲染 <c>Chunk</c> / <c>Line</c> 的代码面板。
/// 数据来自 <see cref="DiffResult"/>（与 biturbo 产出的结构同构）。
/// </summary>
public sealed class DiffView : UserControl
{
    private readonly TextEditor _editor;

    public DiffView()
    {
        _editor = new TextEditor
        {
            FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
            FontSize = 13,
            ShowLineNumbers = true,
            IsReadOnly = true,
        };
        Content = _editor;
    }

    public void Load(DiffResult result)
    {
        var lines = new List<DiffLine>(result.Lines);
        var sb = new StringBuilder();
        foreach (var l in lines)
        {
            var prefix = l.Kind switch
            {
                DiffLineKind.Added => "+ ",
                DiffLineKind.Removed => "- ",
                _ => "  ",
            };
            sb.Append(prefix).Append(l.Text).Append('\n');
        }

        _editor.Document = new TextDocument(sb.ToString());
        _editor.TextArea.TextView.LineTransformers.Add(new DiffColorizer(lines));
    }
}
