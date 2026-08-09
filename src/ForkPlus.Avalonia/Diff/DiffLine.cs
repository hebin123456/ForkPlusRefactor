namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 单行差异的种类。与 ForkPlus 的 biturbo 原生结构 <c>BtPatchToken.kind</c>
/// （Context / Add / Delete）以及 <c>ChangeChunk</c> / <c>ContextChunk</c> 一一对应。
/// </summary>
public enum DiffLineKind
{
    /// <summary>未改动（上下文行，对应 biturbo 的 Context token）。</summary>
    Unchanged,

    /// <summary>新增行（对应 biturbo 的 Add token）。</summary>
    Added,

    /// <summary>删除行（对应 biturbo 的 Delete token）。</summary>
    Removed,
}

/// <summary>
/// 一行差异。对标 ForkPlus 渲染层使用的 <c>Line</c> / <c>SelectableLine</c>：
/// 一侧是行文本，另两侧是它在旧/新版本中的行号（删除行无新行号，新增行无旧行号）。
/// </summary>
public sealed record DiffLine(
    DiffLineKind Kind,
    string Text,
    /// <summary>在旧文本中的行号（1-based）；删除/新增行分别为 -1。</summary>
    int OldLineNumber,
    /// <summary>在新文本中的行号（1-based）；删除/新增行分别为 -1。</summary>
    int NewLineNumber);
