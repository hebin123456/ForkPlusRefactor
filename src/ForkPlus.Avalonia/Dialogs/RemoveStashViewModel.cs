using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>
/// 简单 POCO：用于 ApplyStash / RemoveStash 弹窗 ItemsControl 列表项。
/// WPF 原版用 <c>StashRevision : IGitPoint</c>，迁移到 Avalonia 时抽象为
/// (ReflogName, Subject) 二元组；ApplyStash 额外携带 Branch 字段。
/// </summary>
public sealed class StashItem
{
    public string ReflogName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Display => string.IsNullOrEmpty(Subject) ? ReflogName : $"{ReflogName}: {Subject}";
}

public sealed class RemoveStashViewModel
{
    /// <summary>主标题前缀（"Stash:" / "Stashes:"）。</summary>
    public string StartPointLabel { get; set; } = "Stash:";
    public string DialogTitle { get; set; } = "Delete Stash";
    public string DialogDescription { get; set; } = "Delete stash from your repository";
    public string SubmitButtonTitle { get; set; } = "Delete";

    /// <summary>单条模式：主显示的 GitPoint 文本。</summary>
    public string? SinglePointText { get; set; }

    /// <summary>多条模式：列表展示的 stash 项。</summary>
    public List<StashItem> Stashes { get; set; } = new();

    /// <summary>单条/多条切换：true → 显示单条 GitPointView，false → 显示 ItemsControl 列表。</summary>
    public bool IsSingleMode => Stashes.Count <= 1;
}
