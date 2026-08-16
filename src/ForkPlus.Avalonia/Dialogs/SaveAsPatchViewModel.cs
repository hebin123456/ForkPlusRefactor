using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>WPF 原版用 <c>Revision : IGitPoint</c>。Avalonia stub 简化为 (Subject, Sha)。</summary>
public sealed class SaveAsPatchRevisionItem
{
    public string Subject { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
}

public sealed class SaveAsPatchViewModel
{
    public string DialogTitle { get; set; } = "Save As Patch";
    public string DialogDescription { get; set; } = "Save selected revisions as patch file(s)";
    public string SubmitButtonTitle { get; set; } = "Save";

    /// <summary>顶部说明文本（"X revision(s) selected"）。</summary>
    public string RevisionsLabel { get; set; } = string.Empty;

    /// <summary>要保存为 patch 的 revision 列表。</summary>
    public List<SaveAsPatchRevisionItem> Revisions { get; set; } = new();

    /// <summary>目标路径（可选；为 null 时表示仅展示列表）。</summary>
    public string? TargetPath { get; set; }
}
