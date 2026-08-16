namespace ForkPlus.Avalonia.Dialogs;

/// <summary>
/// 用户对"本地未提交改动如何处理"的三选一选项。
/// 与 WPF 原版 LeanBranchingStartWindow 的 3 个 RadioButton 一一对应。
/// </summary>
public enum LeanBranchingLocalChanges
{
    /// <summary>如果会覆盖本地改动就报错（git 默认）。</summary>
    DoNotChange,
    /// <summary>stash 一下再 reapply。</summary>
    StashAndReapply,
    /// <summary>直接 discard（--force）。</summary>
    Discard,
}

public sealed class LeanBranchingStartViewModel
{
    public string DialogTitle { get; set; } = "Start Lean Branching";
    public string DialogDescription { get; set; } = "Start a new branch with lean branching policy";
    public string SubmitButtonTitle { get; set; } = "Start";

    public string? StartBranchOnText { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string BranchNamePlaceholder { get; set; } = "Enter branch name";

    public LeanBranchingLocalChanges LocalChangesOption { get; set; } = LeanBranchingLocalChanges.DoNotChange;

    public string LocalChangesLabel { get; set; } = "Local changes:";

    public string DoNotChangeLabel { get; set; } = "Don't change";
    public string DoNotChangeToolTip { get; set; } = "Stop if some local changes would be overwritten by checkout";

    public string StashAndReapplyLabel { get; set; } = "Stash and reapply";
    public string StashAndReapplyToolTip { get; set; } = "Hold Shift to not reapply";

    public string DiscardLabel { get; set; } = "Discard";
    public string DiscardToolTip { get; set; } = "Discard local changes (--force)";
}
