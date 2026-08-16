namespace ForkPlus.Avalonia.Dialogs;

public enum ResetType
{
    Soft,
    Mixed,
    Hard,
}

public sealed class ResetBranchViewModel
{
    public ResetType ResetType { get; set; } = ResetType.Mixed;
}
