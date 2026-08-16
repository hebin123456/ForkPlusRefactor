using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class MergeBranchWindow : Window
{
    public MergeBranchViewModel ViewModel { get; } = new();

    public MergeBranchWindow()
    {
        // 预填合并选项列表（与 WPF 原版 MergeBranchWindow 保持一致：Default / No-FF / 分割 / Squash / No-Commit）
        ViewModel.Options = new List<MergeTypeOption>
        {
            new() { Title = "Default",         Description = "Fast-forward if possible", Command = string.Empty, MergeType = "FastForward" },
            new() { Title = "No Fast-Forward", Description = "Always create a merge commit", Command = "--no-ff", MergeType = "NoFastForward" },
            new() { Title = "Squash",          Description = "Squash merge", Command = "--squash", MergeType = "Squash" },
            new() { Title = "Don't Commit",    Description = "Merge without commit", Command = "--no-commit", MergeType = "NoCommit" },
        };
        ViewModel.SelectedOption = ViewModel.Options.First();

        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// 返回用户选中的合并选项（null 表示取消）。
    /// </summary>
    public MergeTypeOption? GetResult() => ViewModel.SelectedOption;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(ViewModel.SelectedOption);
}
