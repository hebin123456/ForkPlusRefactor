using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class GitFlowStartReleaseViewModel
{
    public string DialogTitle { get; set; } = "Start Release";
    public string DialogDescription { get; set; } = "Start a new release branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Start";

    public string Prefix { get; set; } = "release/";
    public string Name { get; set; } = string.Empty;
    public string NamePlaceholder { get; set; } = "Release Name";
    public string FullName => Prefix + Name;

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }
}
