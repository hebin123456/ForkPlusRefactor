namespace ForkPlus.Avalonia.Dialogs;

public sealed class RenameLocalBranchViewModel
{
    public string DialogTitle { get; set; } = "Rename Local Branch";
    public string DialogDescription { get; set; } = "Rename a local branch";
    public string SubmitButtonTitle { get; set; } = "Rename";

    /// <summary>新分支名（用户输入）。</summary>
    public string NewName { get; set; } = string.Empty;

    /// <summary>占位提示。</summary>
    public string Placeholder { get; set; } = "Enter new branch name";

    /// <summary>当前分支（GitPoint 显示文本）。</summary>
    public string? CurrentBranchText { get; set; }

    /// <summary>同时重命名远端分支的勾选框标签。</summary>
    public string? RenameRemoteCheckboxLabel { get; set; }

    /// <summary>勾选状态。</summary>
    public bool RenameRemoteChecked { get; set; }
}
