namespace ForkPlus.Avalonia.Dialogs;

public sealed class DeleteSubmoduleViewModel
{
    public string DialogTitle { get; set; } = "Delete Submodule";
    public string DialogDescription { get; set; } = "Delete a registered submodule";
    public string SubmitButtonTitle { get; set; } = "Delete";

    public string NameLabel { get; set; } = "Submodule name:";
    public string PathLabel { get; set; } = "Path:";
    public string ForceDeleteLabel { get; set; } = "Force delete";
    public string ForceDeleteToolTip { get; set; } = "Allow deleting a submodule that contains uncommitted changes";

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool ForceDelete { get; set; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Name);
}
