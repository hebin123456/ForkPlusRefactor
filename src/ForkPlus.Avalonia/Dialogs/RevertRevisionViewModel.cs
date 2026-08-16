using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>WPF 原版用 <c>Revision : IGitPoint</c>。Avalonia stub 简化为 (Subject, Sha)。</summary>
public sealed class RevisionItem
{
    public string Subject { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
}

public sealed class RevertRevisionViewModel
{
    public string DialogTitle { get; set; } = "Revert";
    public string DialogDescription { get; set; } = "Revert changes of the individual commit";
    public string SubmitButtonTitle { get; set; } = "Revert";

    /// <summary>要回滚的 commit 的 GitPoint 显示文本。</summary>
    public string? RevisionText { get; set; }

    /// <summary>合并提交（merge commit）的 parent 候选列表。</summary>
    public List<RevisionItem> Parents { get; set; } = new();

    /// <summary>选中的 parent。</summary>
    public RevisionItem? SelectedParent { get; set; }

    /// <summary>是否为 merge commit（决定是否显示 parent ComboBox）。</summary>
    public bool IsMergeRevision => Parents.Count > 1;

    /// <summary>"Commit the changes" 勾选框标签。</summary>
    public string CommitCheckboxLabel { get; set; } = "Commit the changes";

    /// <summary>勾选状态（默认 true）。</summary>
    public bool CommitChecked { get; set; } = true;

    /// <summary>parent 选择框标签。</summary>
    public string ParentLabel { get; set; } = "Parent to skip:";
}
