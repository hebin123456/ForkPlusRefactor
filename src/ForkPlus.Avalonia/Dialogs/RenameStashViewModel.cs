namespace ForkPlus.Avalonia.Dialogs;

public sealed class RenameStashViewModel
{
    public string DialogTitle { get; set; } = "Rename Stash";
    public string DialogDescription { get; set; } = "Rename an existing stash entry";
    public string SubmitButtonTitle { get; set; } = "Rename";

    public string MessageLabel { get; set; } = "Message:";
    public string MessagePlaceholder { get; set; } = "Enter new stash message";

    public string Message { get; set; } = string.Empty;
}
