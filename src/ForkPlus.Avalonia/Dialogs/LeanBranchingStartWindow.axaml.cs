using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class LeanBranchingStartWindow : Window
{
    public LeanBranchingStartViewModel ViewModel { get; } = new();

    public LeanBranchingStartWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string BranchName, LeanBranchingLocalChanges Option)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.BranchName)
            ? null
            : (ViewModel.BranchName, ViewModel.LocalChangesOption);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
