using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class GitFlowFinishFeatureViewModel
{
    public string DialogTitle { get; set; } = "Finish Feature";
    public string DialogDescription { get; set; } = "Finish a feature branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Finish";

    public string BranchLabel { get; set; } = "Feature:";
    public string DeleteBranchesLabel { get; set; } = "Delete branches";
    public string DeleteBranchesToolTip { get; set; } = "--keep (when unchecked)";
    public string RebaseLabel { get; set; } = "Rebase before merging";
    public string RebaseToolTip { get; set; } = "--rebase";
    public string NoFastForwardLabel { get; set; } = "No fast-forward";
    public string NoFastForwardToolTip { get; set; } = "--no-ff";

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }

    public bool DeleteBranches { get; set; }
    public bool RebaseInsteadOfMerge { get; set; }
    public bool NoFastForward { get; set; }
}
