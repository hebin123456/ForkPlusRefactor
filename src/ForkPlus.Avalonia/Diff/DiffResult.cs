using System.Collections.Generic;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 一次差异计算的结果。对标 ForkPlus 把 biturbo 的 <c>BtParsePatchResult.tokens</c>
/// 组装后得到的 <c>Chunk</c> 序列（此处拍平为有序的 <see cref="DiffLine"/> 列表，
/// 渲染层可按需再聚合成 hunk / ChangeChunk / ContextChunk）。
/// </summary>
public sealed class DiffResult
{
    public DiffResult(string oldPath, string newPath, IReadOnlyList<DiffLine> lines)
    {
        OldPath = oldPath;
        NewPath = newPath;
        Lines = lines;
    }

    /// <summary>旧文件（如 <c>a/file.cs</c>）。</summary>
    public string OldPath { get; }

    /// <summary>新文件（如 <c>b/file.cs</c>）。</summary>
    public string NewPath { get; }

    /// <summary>按显示顺序排列的差异行（含上下文、新增、删除）。</summary>
    public IReadOnlyList<DiffLine> Lines { get; }
}
