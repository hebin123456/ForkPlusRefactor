using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class MergeTypeOption
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    /// <summary>
    /// 原始 WPF 使用 <c>ForkPlus.Git.MergeType</c> 枚举。此处保留为字符串字段，避免引用 WPF 业务层。
    /// </summary>
    public string MergeType { get; set; } = string.Empty;
}

public sealed class MergeBranchViewModel
{
    public List<MergeTypeOption> Options { get; set; } = new();

    public MergeTypeOption? SelectedOption { get; set; }
}
