namespace ForkPlus.Avalonia.Dialogs;

public sealed class CheckoutBranchViewModel
{
    public string DialogTitle { get; set; } = "Checkout Branch";
    public string DialogDescription { get; set; } = "Switch to a different branch";
    public string SubmitButtonTitle { get; set; } = "Checkout";

    public string SwitchToLabel { get; set; } = "Switch to:";
    public string FastForwardToLabel { get; set; } = "Fast-Forward to:";
    public string LocalChangesLabel { get; set; } = "Local changes:";

    public string SwitchToText { get; set; } = string.Empty;
    public string FastForwardText { get; set; } = string.Empty;

    public LeanBranchingLocalChanges LocalChangesOption { get; set; } = LeanBranchingLocalChanges.DoNotChange;

    public string DoNotChangeLabel { get; set; } = "Don't change";
    public string DoNotChangeToolTip { get; set; } = "Stop if some local changes would be overwritten by checkout";

    public string StashAndReapplyLabel { get; set; } = "Stash and reapply";
    public string StashAndReapplyToolTip { get; set; } = "Hold Shift to not reapply";

    public string DiscardLabel { get; set; } = "Discard";
    public string DiscardToolTip { get; set; } = "Discard local changes (--force)";
}
