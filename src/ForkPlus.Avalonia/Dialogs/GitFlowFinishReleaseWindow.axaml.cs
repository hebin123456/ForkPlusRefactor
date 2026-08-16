using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GitFlowFinishReleaseWindow : Window
{
    public GitFlowFinishReleaseViewModel ViewModel { get; } = new();

    public GitFlowFinishReleaseWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (BranchItem? Branch, string Message, bool DeleteBranches, bool BackMergeMaster)? GetResult() =>
        ViewModel.SelectedBranch is null
            ? null
            : (ViewModel.SelectedBranch, ViewModel.Message, ViewModel.DeleteBranches, ViewModel.BackMergeMaster);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
