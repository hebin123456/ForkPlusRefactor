using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GitFlowFinishFeatureWindow : Window
{
    public GitFlowFinishFeatureViewModel ViewModel { get; } = new();

    public GitFlowFinishFeatureWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (BranchItem? Branch, bool DeleteBranches, bool RebaseInsteadOfMerge, bool NoFastForward)? GetResult() =>
        ViewModel.SelectedBranch is null
            ? null
            : (ViewModel.SelectedBranch,
               ViewModel.DeleteBranches,
               ViewModel.RebaseInsteadOfMerge,
               ViewModel.NoFastForward);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
