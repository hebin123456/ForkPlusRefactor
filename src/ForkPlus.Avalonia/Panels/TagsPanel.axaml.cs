using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M7 tag 面板：从 <see cref="GitRepository"/> 拉 tag 列表 + 跳到 commit / 删除本地 tag。
///
/// <para>
/// 沿用 M4/M5/M6 的"面板只关心自己的状态，弹窗/反馈交给 MainWindow"模式：
/// </para>
/// <list type="bullet">
///   <item><see cref="Load"/>：外部（MainWindow 打开仓库后）把 <see cref="GitRepository"/> 喂进来</item>
///   <item><see cref="ViewRequested"/>：用户点"查看 commit"按钮 → 触发 M2 加载该 commit 所在分支的提交列表（高亮选中）</item>
///   <item><see cref="DeleteRequested"/>：用户点"删除"按钮 → MainWindow 调 <c>git tag -d</c> + reload 面板</item>
///   <item><see cref="SelectionChangedHint"/>：选行时把 tag name + sha7 写到主窗口状态栏</item>
/// </list>
///
/// <para>
/// M7 不做 create / push tag —— 后续阶段加上。也不做双击行打开新窗口（WPF 原版的"重置到 tag"对话框）
/// —— M7 阶段先只展示列表，让 verify 流程能跑通。
/// </para>
/// </summary>
public partial class TagsPanel : UserControl
{
    private ListBox? _list;
    private TextBlock? _summaryText;
    private Button? _viewButton;
    private Button? _deleteButton;

    /// <summary>当前绑定的仓库。null 表示"未打开仓库"状态。</summary>
    public GitRepository? Repository { get; private set; }

    /// <summary>用户请求"查看"当前选中的 tag（参数是 tag 对象或 null）。</summary>
    public event EventHandler<GitTag?>? ViewRequested;

    /// <summary>用户请求"删除"当前选中的 tag（参数是 tag 对象或 null）。</summary>
    public event EventHandler<GitTag?>? DeleteRequested;

    /// <summary>状态文字提示（MainWindow 把这个写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    public TagsPanel()
    {
        InitializeComponent();
        _list = this.FindControl<ListBox>("TagsList");
        _summaryText = this.FindControl<TextBlock>("TagsSummaryText");
        _viewButton = this.FindControl<Button>("ViewTagButton");
        _deleteButton = this.FindControl<Button>("DeleteTagButton");

        if (_summaryText != null) _summaryText.Text = "未打开仓库。";
        if (_list != null)
        {
            _list.SelectionChanged += (_, _) => OnSelectionChanged();
            _list.DoubleTapped += (_, _) => RaiseView();
        }
        if (_viewButton != null) _viewButton.Click += (_, _) => RaiseView();
        if (_deleteButton != null) _deleteButton.Click += (_, _) => RaiseDelete();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入仓库（开新仓库 / 删除 tag 后调用）。</summary>
    public void Load(GitRepository? repo)
    {
        Repository = repo;
        if (repo == null)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "未打开仓库。";
            return;
        }
        Reload();
    }

    /// <summary>用当前 Repository 重新拉一次 tag 列表。</summary>
    public void Reload()
    {
        if (Repository == null)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "未打开仓库。";
            return;
        }
        try
        {
            GitTag[] tags = Repository.GetTags();
            if (_list != null) _list.ItemsSource = tags;
            if (_summaryText != null)
            {
                int annotated = 0;
                foreach (var t in tags) if (t.IsAnnotated) annotated++;
                _summaryText.Text = tags.Length == 0
                    ? "无 tag。用 git tag 或 git tag -a 创建。"
                    : $"共 {tags.Length} 条 tag（{annotated} 个 annotated，{tags.Length - annotated} 个 lightweight），按字典序。";
            }
        }
        catch (Exception ex)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "加载 tag 失败：" + ex.Message;
        }
    }

    /// <summary>当前选中的 tag。</summary>
    public GitTag? SelectedTag => _list?.SelectedItem as GitTag;

    private void OnSelectionChanged()
    {
        if (SelectedTag is { } t)
        {
            string shortSha = t.PeeledSha.Length >= 7 ? t.PeeledSha.Substring(0, 7) : t.PeeledSha;
            SelectionChangedHint?.Invoke(this, $"{t.Name} ({shortSha}{(t.IsAnnotated ? ", annotated" : ", lightweight")})");
        }
    }

    private void RaiseView() => ViewRequested?.Invoke(this, SelectedTag!);
    private void RaiseDelete() => DeleteRequested?.Invoke(this, SelectedTag!);
}
