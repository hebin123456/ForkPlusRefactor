namespace ForkPlus.Avalonia.Dialogs;

public sealed class AddCustomRefspecViewModel
{
    public string DialogTitle { get; set; } = "Add Custom Refspec";
    public string DialogDescription { get; set; } = "Add a custom refspec to the current remote";
    public string SubmitButtonTitle { get; set; } = "Add";

    public string RemoteName { get; set; } = string.Empty;
    public string BranchNameLabel { get; set; } = string.Empty;
    public string BranchNamePlaceholder { get; set; } = "Remote Branch Name";

    public string BranchName { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(BranchName);
}
