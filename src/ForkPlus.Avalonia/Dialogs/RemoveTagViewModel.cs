using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>WPF 原版用 <c>Tag : IGitPoint</c>。Avalonia stub 简化为 (Name, Sha) 二元组。</summary>
public sealed class TagItem
{
    public string Name { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
}

public sealed class RemoveTagViewModel
{
    public string StartPointLabel { get; set; } = "Tags:";
    public string DialogTitle { get; set; } = "Delete Tag";
    public string DialogDescription { get; set; } = "Delete tag from your repository";
    public string SubmitButtonTitle { get; set; } = "Delete";

    public string? SinglePointText { get; set; }

    public List<TagItem> Tags { get; set; } = new();

    public bool IsSingleMode => Tags.Count <= 1;

    /// <summary>同时从远端删除选项（勾选状态）。</summary>
    public bool DeleteFromRemotesChecked { get; set; }

    /// <summary>同时从远端删除选项的标签文本。</summary>
    public string? DeleteFromRemotesLabel { get; set; }
}
