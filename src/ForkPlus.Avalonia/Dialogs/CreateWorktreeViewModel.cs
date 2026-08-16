using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class CreateWorktreeViewModel
{
    public string DialogTitle { get; set; } = "Create Worktree";
    public string DialogDescription { get; set; } = "Create a new worktree for an existing branch";
    public string SubmitButtonTitle { get; set; } = "Create";

    public string StartFromLabel { get; set; } = "Start from:";
    public string BranchNameLabel { get; set; } = "Branch name:";
    public string BranchNamePlaceholder { get; set; } = "Enter branch name";
    public string LocationLabel { get; set; } = "Location:";

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }

    public string BranchName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(BranchName);
}
