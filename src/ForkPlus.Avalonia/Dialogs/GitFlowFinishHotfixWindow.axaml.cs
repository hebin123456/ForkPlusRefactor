using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GitFlowFinishHotfixWindow : Window
{
    public GitFlowFinishHotfixViewModel ViewModel { get; } = new();

    public GitFlowFinishHotfixWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (BranchItem? Branch, string Message, bool DeleteBranches)? GetResult() =>
        ViewModel.SelectedBranch is null
            ? null
            : (ViewModel.SelectedBranch, ViewModel.Message, ViewModel.DeleteBranches);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
