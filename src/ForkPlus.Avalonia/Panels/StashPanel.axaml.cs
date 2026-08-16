using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ForkPlus.Avalonia.Git;

namespace ForkPlus.Avalonia.Panels;

/// <summary>
/// M6 stash 面板：从 <see cref="GitRepository"/> 拉 stash 列表 + apply/pop/drop/show 操作。
///
/// <para>
/// 沿用 M4/M5 的"面板只关心自己的状态，弹窗/反馈交给 MainWindow"模式：
/// </para>
/// <list type="bullet">
///   <item><see cref="Load"/>：外部（MainWindow 打开仓库后）把 <see cref="GitRepository"/> 喂进来</item>
///   <item><see cref="ApplyRequested"/> / <see cref="PopRequested"/> / <see cref="DropRequested"/> /
///         <see cref="ShowDiffRequested"/>：用户点按钮时面板发出事件，
///         MainWindow 收到后调 <see cref="GitRepository.StashApply"/> 等并 reload 面板</item>
///   <item><see cref="SelectionChangedHint"/>：选行时把"stash@{N} - sha"写到主窗口状态栏</item>
/// </list>
///
/// <para>
/// 面板本身不弹窗（弹 DiffWindow 由 MainWindow 决定），也不刷新其它面板（MainWindow 收到事件后
/// 会 <see cref="Load"/> 本面板 + 让 M4 工作区 + M5 文件树一起 reload）。
/// </para>
/// </summary>
public partial class StashPanel : UserControl
{
    private ListBox? _list;
    private TextBlock? _summaryText;
    private Button? _applyButton;
    private Button? _popButton;
    private Button? _dropButton;
    private Button? _showDiffButton;

    /// <summary>当前绑定的仓库。null 表示"未打开仓库"状态。</summary>
    public GitRepository? Repository { get; private set; }

    /// <summary>用户请求 apply 当前选中的 stash（传 null 表示无选择）。</summary>
    public event EventHandler<GitStash?>? ApplyRequested;

    /// <summary>用户请求 pop 当前选中的 stash（传 null 表示无选择）。</summary>
    public event EventHandler<GitStash?>? PopRequested;

    /// <summary>用户请求 drop 当前选中的 stash（传 null 表示无选择）。</summary>
    public event EventHandler<GitStash?>? DropRequested;

    /// <summary>用户请求查看当前选中 stash 的 diff（传 null 表示无选择）。</summary>
    public event EventHandler<GitStash?>? ShowDiffRequested;

    /// <summary>状态文字提示（MainWindow 把这个写到它的 StatusText）。</summary>
    public event EventHandler<string>? SelectionChangedHint;

    public StashPanel()
    {
        InitializeComponent();
        _list = this.FindControl<ListBox>("StashList");
        _summaryText = this.FindControl<TextBlock>("StashSummaryText");
        _applyButton = this.FindControl<Button>("ApplyStashButton");
        _popButton = this.FindControl<Button>("PopStashButton");
        _dropButton = this.FindControl<Button>("DropStashButton");
        _showDiffButton = this.FindControl<Button>("ViewStashDiffButton");

        if (_summaryText != null)
        {
            _summaryText.Text = "未打开仓库。";
        }
        if (_list != null)
        {
            _list.SelectionChanged += (_, _) => OnSelectionChanged();
            _list.DoubleTapped += (_, _) => RaiseShowDiff();
        }
        if (_applyButton != null) _applyButton.Click += (_, _) => RaiseApply();
        if (_popButton != null) _popButton.Click += (_, _) => RaisePop();
        if (_dropButton != null) _dropButton.Click += (_, _) => RaiseDrop();
        if (_showDiffButton != null) _showDiffButton.Click += (_, _) => RaiseShowDiff();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>从外部注入仓库（开新仓库 / apply/pop/drop 之后调用）。</summary>
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

    /// <summary>用当前 Repository 重新拉一次 stash list（不切换 repo 时使用）。</summary>
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
            GitStash[] stashes = Repository.GetStashes();
            if (_list != null) _list.ItemsSource = stashes;
            if (_summaryText != null)
            {
                _summaryText.Text = stashes.Length == 0
                    ? "无 stash。修改文件后点 '创建' 可暂存（暂未在面板暴露，将由后续阶段支持）。"
                    : $"共 {stashes.Length} 条 stash，最新在顶部（stash@{{0}}）。";
            }
        }
        catch (Exception ex)
        {
            if (_list != null) _list.ItemsSource = null;
            if (_summaryText != null) _summaryText.Text = "加载 stash 失败：" + ex.Message;
        }
    }

    /// <summary>当前选中的 stash（用于 MainWindow 弹 DiffWindow 时取 selector）。</summary>
    public GitStash? SelectedStash => _list?.SelectedItem as GitStash;

    private void OnSelectionChanged()
    {
        if (SelectedStash is { } s)
        {
            SelectionChangedHint?.Invoke(this, $"{s.ReflogSelector} - {s.Sha.Substring(0, Math.Min(7, s.Sha.Length))} - {s.DisplayLine}");
        }
    }

    private void RaiseApply() => ApplyRequested?.Invoke(this, SelectedStash!);
    private void RaisePop() => PopRequested?.Invoke(this, SelectedStash!);
    private void RaiseDrop() => DropRequested?.Invoke(this, SelectedStash!);
    private void RaiseShowDiff() => ShowDiffRequested?.Invoke(this, SelectedStash!);
}
