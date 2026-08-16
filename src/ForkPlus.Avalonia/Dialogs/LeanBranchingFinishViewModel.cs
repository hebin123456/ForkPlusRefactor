namespace ForkPlus.Avalonia.Dialogs;

public sealed class LeanBranchingFinishViewModel
{
    public string DialogTitle { get; set; } = "Finish Lean Branching";
    public string DialogDescription { get; set; } = "Merge a lean branch back into the main branch";
    public string SubmitButtonTitle { get; set; } = "Finish";

    public string CurrentBranchText { get; set; } = string.Empty;
    public string MainBranchText { get; set; } = string.Empty;
}
