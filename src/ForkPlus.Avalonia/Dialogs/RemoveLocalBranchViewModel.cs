using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>
/// WPF 原版用 <c>LocalBranch : Reference</c>。Avalonia stub 简化为 (BranchName, UpstreamName) 二元组。
/// </summary>
public sealed class BranchItem
{
    public string BranchName { get; set; } = string.Empty;
    public string? UpstreamName { get; set; }
    public bool HasUpstream => !string.IsNullOrEmpty(UpstreamName);
}

public sealed class RemoveLocalBranchViewModel
{
    public string StartPointLabel { get; set; } = "Branches:";
    public string DialogTitle { get; set; } = "Delete Local Branch";
    public string DialogDescription { get; set; } = "Delete local branches";
    public string SubmitButtonTitle { get; set; } = "Delete";

    public string? SinglePointText { get; set; }

    public List<BranchItem> Branches { get; set; } = new();

    public bool IsSingleMode => Branches.Count <= 1;

    /// <summary>是否启用"同时删除远端分支"选项。</summary>
    public bool DeleteRemoteEnabled { get; set; }

    /// <summary>是否同时删除 worktree。</summary>
    public bool DeleteWorktreeEnabled { get; set; }

    /// <summary>勾选状态。</summary>
    public bool DeleteRemoteChecked { get; set; }
    public bool DeleteWorktreeChecked { get; set; }

    /// <summary>勾选标签文本。</summary>
    public string? DeleteRemoteLabel { get; set; }
    public string? DeleteWorktreeLabel { get; set; }
}
