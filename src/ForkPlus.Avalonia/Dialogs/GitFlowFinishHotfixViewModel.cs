using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class GitFlowFinishHotfixViewModel
{
    public string DialogTitle { get; set; } = "Finish Hotfix";
    public string DialogDescription { get; set; } = "Finish a hotfix branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Finish";

    public string HotfixLabel { get; set; } = "Hotfix:";
    public string MessageLabel { get; set; } = "Message:";
    public string MessagePlaceholder { get; set; } = "Enter hotfix message (optional)";
    public string DeleteBranchesLabel { get; set; } = "Delete branches";
    public string DeleteBranchesToolTip { get; set; } = "--keep (when unchecked)";

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }

    public string Message { get; set; } = string.Empty;
    public bool DeleteBranches { get; set; } = true;
}
