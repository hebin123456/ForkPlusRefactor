namespace ForkPlus.Avalonia.Dialogs;

public sealed class SaveSnapshotViewModel
{
    public string DialogTitle { get; set; } = "Save Snapshot";
    public string DialogDescription { get; set; } = "Save current working tree as a stash";
    public string SubmitButtonTitle { get; set; } = "Save";

    /// <summary>当前 HEAD 的 GitPoint 显示文本。</summary>
    public string? CurrentHeadText { get; set; }

    /// <summary>stash 消息（用户输入，可选）。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>占位提示。</summary>
    public string Placeholder { get; set; } = "Stash message (optional)";

    /// <summary>"Stage new files" 勾选框标签。</summary>
    public string StageNewFilesLabel { get; set; } = "Stage new files";

    /// <summary>勾选状态（默认 false）。</summary>
    public bool StageNewFilesChecked { get; set; }

    /// <summary>提示行文本。</summary>
    public string HintText { get; set; } = "By default stash ignores new files until you stage them";
}
