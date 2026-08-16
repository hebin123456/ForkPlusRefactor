using System;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M6：单个 stash 条目的不可变记录。
///
/// <para>
/// 对应 <c>git stash list</c> 的一行：<c>stash@{0}: On master: wip: main 增加追踪器输出</c>。
/// 我们不直接依赖 <c>git stash list</c> 的默认格式（依赖本地化），而是用
/// <c>git stash list --format=...</c> 拿机器友好字段，再拼装。
/// </para>
///
/// <para>
/// 关键字段：
/// </para>
/// <list type="bullet">
///   <item><see cref="ReflogSelector"/>：唯一标识，对应 <c>stash@{0}</c>。
///         不要用 <see cref="Sha"/> 当 key —— git 的 stash 是 reflog 里的，<c>stash pop</c> 后同一 SHA
///         就不在列表里了；要按 index 操作必须用 <c>stash@{N}</c>。</item>
///   <item><see cref="Sha"/>：stash commit 的 SHA，可用于 <c>git show &lt;sha&gt;:&lt;file&gt;</c> 拿内容。</item>
///   <item><see cref="Branch"/>：stash 时的当前分支（WPF 原版侧栏显示用）。</item>
///   <item><see cref="Message"/>：用户提供的 stash message（WPF 原版显示用）。</item>
/// </list>
///
/// <para>
/// M6 阶段所有字段都是不可变 string / int，UI 层只读。
/// </para>
/// </summary>
public sealed class GitStash
{
    /// <summary>stash 在 reflog 里的位置，从 0 开始。最新的是 <c>stash@{0}</c>。</summary>
    public int ReflogIndex { get; }

    /// <summary>完整 selector 字符串（"stash@{0}"），可直接喂给 <c>git stash apply/pop/drop</c>。</summary>
    public string ReflogSelector { get; }

    /// <summary>stash commit 的完整 SHA（git show / git diff 拿内容用）。</summary>
    public string Sha { get; }

    /// <summary>创建 stash 时的当前分支（例如 "master"）；空字符串表示 HEAD detached。</summary>
    public string Branch { get; }

    /// <summary>用户提供的 stash message（例如 "wip: main 增加追踪器输出"）。</summary>
    public string Message { get; }

    public GitStash(int reflogIndex, string reflogSelector, string sha, string branch, string message)
    {
        ReflogIndex = reflogIndex;
        ReflogSelector = reflogSelector ?? throw new ArgumentNullException(nameof(reflogSelector));
        Sha = sha ?? "";
        Branch = branch ?? "";
        Message = message ?? "";
    }

    /// <summary>在 UI 侧栏显示用：把"分支 + 消息"拼成单行，WPF 原版风格。</summary>
    public string DisplayLine
    {
        get
        {
            if (string.IsNullOrEmpty(Branch) && string.IsNullOrEmpty(Message))
            {
                return ReflogSelector;
            }
            if (string.IsNullOrEmpty(Branch))
            {
                return Message;
            }
            if (string.IsNullOrEmpty(Message))
            {
                return Branch;
            }
            return $"{Branch}: {Message}";
        }
    }
}
