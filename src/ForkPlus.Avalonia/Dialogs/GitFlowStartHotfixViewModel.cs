using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>WPF 原版用 <c>LocalBranch</c>。Avalonia stub 简化为 <c>BranchItem</c>。</summary>
public sealed class GitFlowStartHotfixViewModel
{
    public string DialogTitle { get; set; } = "Start Hotfix";
    public string DialogDescription { get; set; } = "Start a new hotfix branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Start";

    public string Prefix { get; set; } = "hotfix/";
    public string Name { get; set; } = string.Empty;
    public string NamePlaceholder { get; set; } = "Hotfix Name";
    public string FullName => Prefix + Name;

    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }
}
