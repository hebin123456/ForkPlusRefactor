using System;
using System.Linq;
using Avalonia.Controls;
using ForkPlus.Avalonia.Git;
using ForkPlus.Avalonia.Panels;

namespace ForkPlus.Avalonia;

/// <summary>
/// M1 + M2 + M5 + M6 仓库/分支/提交/文件树/贮藏加载操作处理器：把"打开 GitRepository → 拉分支 → 选分支拉提交
/// → 拉文件树 → 拉 stash"这一串跨 vertical slice 的协调逻辑从 <see cref="MainWindow"/> 抽出来。
///
/// <para>
/// 之前 <see cref="MainWindow"/> 把 M1（仓库+分支）和 M2（提交列表）逻辑都内联在自己身上，
/// 加上 M3 / M4 / M5 之后越来越臃肿。本类只关心"仓库操作"：
/// </para>
/// <list type="bullet">
///   <item>M1：<see cref="Open"/> 给路径 → 创建 <see cref="GitRepository"/> → 拉 <c>GetBranches()</c> →
///         把分支列表喂给传入的 branchesList；同时清空 commit 列表（开新仓库） + 刷工作区（M4） + 刷文件树（M5）
///         + 刷贮藏列表（M6）</item>
///   <item>M2：<see cref="SelectBranch"/> 给分支名 → <c>GetCommits(branch, 50)</c> →
///         把提交列表喂给传入的 <see cref="CommitDiffPanel"/></item>
///   <item>M5：开新仓库时同步刷一次文件树（HEAD）</item>
///   <item>M6：开新仓库时同步刷一次贮藏列表（不依赖分支）</item>
/// </list>
///
/// <para>
/// 状态文字反馈通过传入的 statusText 写到主窗口右侧 StatusText；
/// 不在这里写是为了让 MainWindow 完全掌控"反馈给用户的文字"的格式（多语言 / 主题等）。
/// </para>
/// </summary>
public class RepoOpHandler
{
    private readonly ListBox? _branchesList;
    private readonly CommitDiffPanel? _commitDiffPanel;
    private readonly WorkingTreePanel? _workingTreePanel;
    private readonly TextBlock? _statusText;
    private readonly FileTreePanel? _fileTreePanel;
    private readonly StashPanel? _stashPanel;

    /// <summary>当前打开的仓库。M3 / M4 / M5 / M6 在需要拿 diff / content 时读这个字段。</summary>
    public GitRepository? CurrentRepo { get; private set; }

    public RepoOpHandler(
        ListBox? branchesList,
        CommitDiffPanel? commitDiffPanel,
        WorkingTreePanel? workingTreePanel,
        TextBlock? statusText,
        FileTreePanel? fileTreePanel = null,
        StashPanel? stashPanel = null)
    {
        _branchesList = branchesList;
        _commitDiffPanel = commitDiffPanel;
        _workingTreePanel = workingTreePanel;
        _statusText = statusText;
        _fileTreePanel = fileTreePanel;
        _stashPanel = stashPanel;
    }

    /// <summary>
    /// M1：打开仓库并列出引用。失败时通过 statusText 反馈。
    /// </summary>
    public void Open(string? path)
    {
        path = path?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            if (_statusText != null) _statusText.Text = "请先填写仓库路径。";
            return;
        }
        try
        {
            CurrentRepo?.Dispose();
            CurrentRepo = new GitRepository(path);
            string[] branches = CurrentRepo.GetBranches();
            if (_branchesList != null) _branchesList.ItemsSource = branches;
            // M2：开新仓库时清空提交列表
            _commitDiffPanel?.LoadCommits(Array.Empty<GitCommit>());
            int local = branches.Count(b => b.StartsWith("refs/heads/"));
            if (_statusText != null)
            {
                _statusText.Text = $"已打开 {path}：共 {branches.Length} 个引用，其中本地分支 {local} 个。点击分支以加载提交（M2）。";
            }
            // M4：同步刷一次工作区改动
            _workingTreePanel?.Load(CurrentRepo);
            // M5：同步刷一次文件树（默认 ref=HEAD）
            _fileTreePanel?.Load(CurrentRepo, "HEAD");
            // M6：同步刷一次贮藏列表
            _stashPanel?.Load(CurrentRepo);
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"打开仓库失败：{ex.Message}";
        }
    }

    /// <summary>
    /// M2：分支被选中后，通过 biturbo 列该分支最新 50 条提交，喂给 <see cref="CommitDiffPanel"/>。
    /// M5：同时把"该分支的 tree-ish"传给 fileTreePanel（用 refs/heads/&lt;name&gt; 拿得到 tree，
    /// 短名也行 —— git ls-tree 内部会查 ref）。
    /// </summary>
    public void SelectBranch(string? selected)
    {
        if (CurrentRepo == null || string.IsNullOrEmpty(selected))
        {
            return;
        }
        try
        {
            GitCommit[] commits = CurrentRepo.GetCommits(selected, maxCount: 50);
            _commitDiffPanel?.LoadCommits(commits);
            // M5：切分支时同步刷文件树（用分支名当 ref，git ls-tree 自动解析）
            _fileTreePanel?.Load(CurrentRepo, selected);
            if (_statusText != null)
            {
                _statusText.Text = commits.Length == 0
                    ? $"分支 {selected} 暂无提交。"
                    : $"已加载 {selected} 的最新 {commits.Length} 条提交（首条：{commits[0].DisplayLine}）。";
            }
        }
        catch (Exception ex)
        {
            _commitDiffPanel?.LoadCommits(Array.Empty<GitCommit>());
            if (_statusText != null) _statusText.Text = $"加载 {selected} 提交失败：{ex.Message}";
        }
    }
}
