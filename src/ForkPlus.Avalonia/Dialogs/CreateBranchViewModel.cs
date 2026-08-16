namespace ForkPlus.Avalonia.Dialogs;

public sealed class CreateBranchViewModel
{
    public string Name { get; set; } = string.Empty;
    public bool CheckoutAfterCreate { get; set; } = true;
    public LocalChangesOption LocalChanges { get; set; } = LocalChangesOption.DoNotChange;

    public enum LocalChangesOption
    {
        DoNotChange,
        StashAndReapply,
        Discard,
    }
}
