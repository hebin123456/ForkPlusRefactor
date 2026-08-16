namespace ForkPlus.Avalonia.Dialogs;

public enum MessageBoxResult
{
    Ok,
    Cancel,
    Yes,
    No,
}

public sealed class MessageBoxViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string OkText { get; set; } = "OK";
    public string CancelText { get; set; } = "Cancel";
    public bool ShowCancel { get; set; }
    public bool ShowOk { get; set; } = true;
    public MessageBoxResult Result { get; set; } = MessageBoxResult.Ok;
}
