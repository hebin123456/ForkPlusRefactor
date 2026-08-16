using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class CreatePartialStashViewModel
{
    public string DialogTitle { get; set; } = "Create Partial Stash";
    public string DialogDescription { get; set; } = "Stash only the selected files";
    public string SubmitButtonTitle { get; set; } = "Stash";

    public string MessageLabel { get; set; } = "Message:";
    public string MessagePlaceholder { get; set; } = "Stash message (optional)";
    public string IncludeUntrackedLabel { get; set; } = "Include untracked";
    public string KeepIndexLabel { get; set; } = "Keep index (--keep-index)";

    public string Message { get; set; } = string.Empty;

    public List<string> AvailableFiles { get; set; } = new();
    public List<string> SelectedFiles { get; set; } = new();

    public bool IncludeUntracked { get; set; }
    public bool KeepIndex { get; set; }
}
