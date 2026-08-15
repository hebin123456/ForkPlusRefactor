namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M4：工作区改动分类。
/// 对应 <c>git status --porcelain</c> 的两字符位图：XY filename。
/// <list type="bullet">
///   <item>X = 索引 (staged) 状态</item>
///   <item>Y = 工作区 (working tree) 状态</item>
/// </list>
/// </summary>
public enum WorkingTreeStatusKind
{
    /// <summary>未改动（例如 ignored、untracked 但实际在 .gitignore 中）。</summary>
    Unmodified,
    /// <summary>已修改（working tree 改了但还没 add）。</summary>
    Modified,
    /// <summary>已暂存（add 后还没 commit）。</summary>
    Added,
    /// <summary>已删除。</summary>
    Deleted,
    /// <summary>重命名。</summary>
    Renamed,
    /// <summary>复制（git status 的 "C" 位）。</summary>
    Copied,
    /// <summary>未跟踪（新文件，没在 git 里）。</summary>
    Untracked,
}

/// <summary>
/// M4：一条工作区改动记录。
/// <see cref="Kind"/> 是 "主分类"（按 Index/Working 中更显眼的一边决定）；
/// <see cref="Staged"/> 表示文件已经被 <c>git add</c> 过；
/// <see cref="Unstaged"/> 表示文件在 working tree 中仍有差异。
/// </summary>
public sealed record WorkingTreeChange(
    string Path,
    WorkingTreeStatusKind Kind,
    bool Staged,
    bool Unstaged,
    string? OldPath = null);
