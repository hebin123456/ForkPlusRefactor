using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CreateWorktreeWindow : Window
{
    public CreateWorktreeViewModel ViewModel { get; } = new();

    public CreateWorktreeWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (BranchItem? Branch, string BranchName, string Path)? GetResult() =>
        !ViewModel.IsValid
            ? null
            : (ViewModel.SelectedBranch, ViewModel.BranchName, ViewModel.Path);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
    private void OnBrowse(object? sender, RoutedEventArgs e) { /* placeholder for folder picker */ }
}
