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

    /// <summary>最近一次 <see cref="Load"/> 注入的 diff（按行 + 颜色分类），供测试断言。</summary>
    public DiffResult? Current { get; private set; }

    public DiffView()
    {
        _editor = new TextEditor
        {
            FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
            FontSize = 13,
            ShowLineNumbers = true,
            IsReadOnly = true,
        };
        // 显式给一个深色背景，让 Added/Removed 行的浅绿/浅红在视觉上能凸显，
        // 也避免 headless 渲染时 RTB 拿到一张全白图（M3 视觉截图依赖该背景出像素）。
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        Content = _editor;
    }

    public void Load(DiffResult result)
    {
        Current = result;
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
