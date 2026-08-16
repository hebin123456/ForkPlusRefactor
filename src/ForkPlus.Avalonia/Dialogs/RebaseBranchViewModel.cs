using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class RebaseBranchViewModel
{
    public string DialogTitle { get; set; } = "Rebase Branch";
    public string DialogDescription { get; set; } = "Reapply commits on top of another base tip";
    public string SubmitButtonTitle { get; set; } = "Rebase";

    public string RebaseLabel { get; set; } = "Rebase:";
    public string OnLabel { get; set; } = "On:";
    public string UpdateRefsLabel { get; set; } = "Update dependent branches";
    public string UpdateRefsToolTip { get; set; } = "--update-refs";
    public string AutostashLabel { get; set; } = "Stash and reapply local changes";

    public string SourceText { get; set; } = string.Empty;
    public string DestinationText { get; set; } = string.Empty;

    public List<string> DependentBranches { get; set; } = new();

    public bool UpdateRefs { get; set; }
    public bool Autostash { get; set; }
}
