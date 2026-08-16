using System.Collections.Generic;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>WPF 原版用 <c>LocalBranch</c>。Avalonia stub 简化为 <c>BranchItem</c>。</summary>
public sealed class GitFlowStartFeatureViewModel
{
    public string DialogTitle { get; set; } = "Start Feature";
    public string DialogDescription { get; set; } = "Start a new feature branch (git flow)";
    public string SubmitButtonTitle { get; set; } = "Start";

    /// <summary>GitFlow 前缀显示（"feature/"）。</summary>
    public string Prefix { get; set; } = "feature/";

    /// <summary>特性名（用户输入，前缀会自动拼到 <see cref="FullName"/>）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>启动基准点（完整分支名）。</summary>
    public string? StartPointText { get; set; }

    /// <summary>候选启动基准分支。</summary>
    public List<BranchItem> Branches { get; set; } = new();
    public BranchItem? SelectedBranch { get; set; }

    /// <summary>完整特性分支名（Prefix + Name）。</summary>
    public string FullName => Prefix + Name;

    public string NamePlaceholder { get; set; } = "Feature Name";
}
