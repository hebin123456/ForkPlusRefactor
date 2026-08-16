using System;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M7：单个 git tag 的不可变记录。
///
/// <para>
/// 对应 <c>git tag -l</c> 的一行（经 <c>--format</c> 渲染）：例如
/// <c>refs/tags/v1.0.0</c> / <c>refs/tags/v2.0.0^{}</c>（peeled commit）。
/// 我们不直接用 <c>git tag -l</c> 默认输出（依赖本地化），而是用
/// <c>git for-each-ref --format=...</c> 拿机器友好字段再拼装。
/// </para>
///
/// <para>
/// 关键字段：
/// </para>
/// <list type="bullet">
///   <item><see cref="Name"/>：tag 短名（<c>v1.0.0</c>），UI 显示用</item>
///   <item><see cref="Sha"/>：tag 指向的对象 SHA（annotated tag = tag commit；lightweight = 直接 commit）</item>
///   <item><see cref="PeeledSha"/>：annotated tag 解引用后的 commit SHA；
///         lightweight tag == <see cref="Sha"/>。为统一接口，M7 阶段统一用 <see cref="PeeledSha"/></item>
///   <item><see cref="IsAnnotated"/>：是否为 annotated tag（<c>git tag -a</c> 创建）</item>
///   <item><see cref="Tagger"/> / <see cref="TagDate"/>：annotated tag 自带；lightweight 为空</item>
/// </list>
///
/// <para>
/// M7 阶段所有字段都是不可变 string / bool，UI 层只读。
/// </para>
/// </summary>
public sealed class GitTag
{
    /// <summary>tag 短名（去掉 refs/tags/ 前缀）。</summary>
    public string Name { get; }

    /// <summary>tag 自身指向的对象 SHA（annotated → tag commit；lightweight → target commit）。</summary>
    public string Sha { get; }

    /// <summary>peeled 后的 commit SHA（annotated tag 解引用到的 commit）。
    /// lightweight tag 此值 == <see cref="Sha"/>。</summary>
    public string PeeledSha { get; }

    /// <summary>是否为 annotated tag（git tag -a 创建，ref 类型为 "tag"）。</summary>
    public bool IsAnnotated { get; }

    /// <summary>tagger 身份（annotated tag 自带，格式 "Name &lt;email&gt;"），lightweight 为空字符串。</summary>
    public string Tagger { get; }

    /// <summary>tag 创建时间（annotated tag 自带，UTC），lightweight 为 <see cref="DateTimeOffset.MinValue"/>。</summary>
    public DateTimeOffset TagDate { get; }

    public GitTag(string name, string sha, string peeledSha, bool isAnnotated, string tagger, DateTimeOffset tagDate)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Sha = sha ?? "";
        PeeledSha = peeledSha ?? "";
        IsAnnotated = isAnnotated;
        Tagger = tagger ?? "";
        TagDate = tagDate;
    }

    /// <summary>在 UI 侧栏显示用：annotated 加 [a] 前缀，lightweight 加 [l] 前缀。</summary>
    public string DisplayLine
    {
        get
        {
            string kind = IsAnnotated ? "[a]" : "[l]";
            string shortSha = PeeledSha.Length >= 7 ? PeeledSha.Substring(0, 7) : PeeledSha;
            return $"{kind} {Name} ({shortSha})";
        }
    }

    /// <summary>7 位 SHA 缩写（peeled 后 commit SHA 的前缀），UI 状态栏 / 测试断言用。</summary>
    public string ShortSha =>
        PeeledSha.Length >= 7 ? PeeledSha.Substring(0, 7) : PeeledSha;
}
