using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class ApplyStashViewModel
{
    public string DialogTitle { get; set; } = "Apply Stash";
    public string DialogDescription { get; set; } = "Apply a stashed change to the working tree";
    public string SubmitButtonTitle { get; set; } = "Apply";

    public string StashLabel { get; set; } = "Stash:";
    public string RestoreIndexLabel { get; set; } = "Restore index";
    public string RestoreIndexToolTip { get; set; } = "--index (when checked)";

    public List<StashItem> Stashes { get; set; } = new();
    public StashItem? SelectedStash { get; set; }

    public bool RestoreIndex { get; set; }
}
