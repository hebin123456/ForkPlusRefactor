namespace ForkPlus.Avalonia.Dialogs;

public sealed class OpenRepositoryAlertViewModel
{
    public string Title { get; set; } = "Open Repository";
    public string Message { get; set; } = string.Empty;
    public string FirstButtonText { get; set; } = "OK";
    public bool ShowFirstButton { get; set; } = true;
}
