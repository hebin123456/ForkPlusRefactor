using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class GitFlowFinishReleaseViewModel
{
    public string DialogTitle { get; set; } = "Finish Release";
    public string DialogDescription { get; set; } = "Finish a release branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Finish";

    public string ReleaseLabel { get; set; } = "Release:";
    public string MessageLabel { get; set; } = "Message:";
    public string MessagePlaceholder { get; set; } = "Enter release message (optional)";
    public string DeleteBranchesLabel { get; set; } = "Delete branches";
    public string DeleteBranchesToolTip { get; set; } = "--keep (when unchecked)";
    public string BackMergeLabel { get; set; } = "Back-merge master into develop";
    public string BackMergeToolTip { get; set; } = "--nobackmerge (when unchecked)";

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }

    public string Message { get; set; } = string.Empty;
    public bool DeleteBranches { get; set; } = true;
    public bool BackMergeMaster { get; set; } = true;
}
