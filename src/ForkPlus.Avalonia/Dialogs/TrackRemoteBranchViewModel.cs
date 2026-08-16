namespace ForkPlus.Avalonia.Dialogs;

public sealed class TrackRemoteBranchViewModel
{
    public string LocalBranchName { get; set; } = string.Empty;
    public CreateBranchViewModel.LocalChangesOption LocalChanges { get; set; } = CreateBranchViewModel.LocalChangesOption.DoNotChange;
}
