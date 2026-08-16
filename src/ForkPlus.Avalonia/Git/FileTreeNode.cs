using System.Collections.Generic;
using System.Linq;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M5 文件树节点模型（对标 WPF 原版 <c>FileTreeItem</c>）。
///
/// <para>
/// 设计要点：
/// </para>
/// <list type="bullet">
///   <item>扁平输入（<c>git ls-tree -r &lt;ref&gt;</c> 的每行）→ 还原为层级树（递归构造 <see cref="Children"/>）。</item>
///   <item>每个节点都带 <see cref="Depth"/>，方便 <c>TreeView</c> / <c>TextBlock.Margin</c> 缩进</item>
///   <item>叶子 <see cref="ItemType"/> == File 时 <see cref="Children"/> 为空；Submodule 同叶子，但带 commit SHA</item>
///   <item>排序：目录优先（同 WPF 原版），同类型按文件名自然序</item>
/// </list>
/// </summary>
public sealed class FileTreeNode
{
    /// <summary>节点类型。</summary>
    public enum FileTreeNodeKind
    {
        File,
        Directory,
        Submodule,
    }

    /// <summary>显示名（仅最后一段路径）。</summary>
    public string Name { get; }

    /// <summary>相对仓库根的完整路径（用 '/' 分隔，跨平台）。</summary>
    public string FullPath { get; }

    /// <summary>该节点对应的 Git tree OID。文件 = blob OID；目录 = tree OID；子模块 = commit OID。</summary>
    public string Sha { get; }

    /// <summary>节点类型。</summary>
    public FileTreeNodeKind ItemType { get; }

    /// <summary>深度（根目录 = 0，根下文件/子目录 = 1，…）。</summary>
    public int Depth { get; }

    /// <summary>子节点（已排序：目录优先 + 自然序）。</summary>
    public List<FileTreeNode> Children { get; }

    /// <summary>非目录节点的"是否在文件树面板里被勾选/标记"。预留给未来的多选操作（暂不消费）。</summary>
    public bool IsExpanded { get; set; }

    public FileTreeNode(
        string name,
        string fullPath,
        string sha,
        FileTreeNodeKind itemType,
        int depth,
        IEnumerable<FileTreeNode>? children = null)
    {
        Name = name;
        FullPath = fullPath;
        Sha = sha;
        ItemType = itemType;
        Depth = depth;
        Children = children?.ToList() ?? new List<FileTreeNode>();
    }

    /// <summary>便捷：当前节点是否"可继续展开"（目录类）。</summary>
    public bool IsContainer => ItemType == FileTreeNodeKind.Directory;

    /// <summary>汇总"扁平行数"（含自己）。用于三段断言里的"节点总数"统计。</summary>
    public int FlatCount()
    {
        int n = 1;
        foreach (FileTreeNode c in Children)
        {
            n += c.FlatCount();
        }
        return n;
    }
}
