using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M8：分支树节点 —— 跟 WPF 原版侧栏树形结构对齐的"组 + 叶"二态节点。
///
/// <para>
/// 树形态（来自 WPF 原版 <c>v2-16-sidebar-full.png</c>）：
/// </para>
/// <code>
///  本地 (LocalBranches)
///   ├── master             ← IsCurrent（HEAD 指向它）
///   └── feature/login
///  远程 (RemoteBranches, 按 remote 名分组)
///   ├── origin
///   │    ├── origin/main
///   │    └── origin/feature/login
///   └── upstream
///        └── upstream/master
/// </code>
///
/// <para>
/// 设计要点：
/// </para>
/// <list type="bullet">
///   <item><see cref="IsGroup"/> = true 表示这是个虚拟组（"本地" / "远程/origin"），点击不展开 commit 列表</item>
///   <item><see cref="IsGroup"/> = false 表示是真实分支，<see cref="FullRef"/> 必填（refs/heads/&lt;name&gt; 或 refs/remotes/&lt;remote&gt;/&lt;name&gt;）</item>
///   <item><see cref="IsCurrent"/> = 当前 HEAD 指向的本地分支；WPF 原版用粗体 + ▶ 标记</item>
///   <item>UI 层根据 <see cref="Kind"/> 决定前缀图标：L = local / R = remote / G = group</item>
/// </list>
/// </summary>
public sealed class BranchTreeNode
{
    /// <summary>节点显示名（不带前缀）。例如 "master" / "本地" / "origin"。</summary>
    public string Name { get; }

    /// <summary>完整 ref（仅叶节点有意义；组节点为 ""）。例如 "refs/heads/master" / "refs/remotes/origin/main"。</summary>
    public string FullRef { get; }

    /// <summary>组 / 叶 二态：true = 虚拟组（不触发 M2），false = 真实分支（触发 M2）。</summary>
    public bool IsGroup { get; }

    /// <summary>UI 前缀图标：L = local，R = remote，G = group（虚拟组）。</summary>
    public BranchKind Kind { get; }

    /// <summary>当前 HEAD 指向的本地分支。WPF 原版用粗体 + ▶ 标记。</summary>
    public bool IsCurrent { get; }

    /// <summary>子节点（组节点有内容，叶节点为空列表）。</summary>
    public IReadOnlyList<BranchTreeNode> Children { get; }

    public BranchTreeNode(
        string name,
        string fullRef,
        bool isGroup,
        BranchKind kind,
        bool isCurrent,
        IReadOnlyList<BranchTreeNode> children)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        FullRef = fullRef ?? "";
        IsGroup = isGroup;
        Kind = kind;
        IsCurrent = isCurrent;
        Children = children ?? Array.Empty<BranchTreeNode>();
    }

    /// <summary>WPF 风格显示行：组 = "{name} ({N})"；叶 = "[图标] {name}"。</summary>
    public string DisplayLine
    {
        get
        {
            if (IsGroup)
            {
                if (Children.Count > 0)
                {
                    return $"{Name} ({Children.Count})";
                }
                return $"{Name} (0)";
            }
            string prefix = Kind switch
            {
                BranchKind.Local => "L",
                BranchKind.Remote => "R",
                _ => "?",
            };
            return IsCurrent ? $"▶ [{prefix}] {Name}" : $"  [{prefix}] {Name}";
        }
    }
}

/// <summary>M8：分支节点类型，决定 UI 前缀图标和颜色。</summary>
public enum BranchKind
{
    /// <summary>虚拟组节点（"本地" / "远程/origin"）。</summary>
    Group,
    /// <summary>本地分支（refs/heads/*）。</summary>
    Local,
    /// <summary>远程分支（refs/remotes/&lt;remote&gt;/*）。</summary>
    Remote,
}
