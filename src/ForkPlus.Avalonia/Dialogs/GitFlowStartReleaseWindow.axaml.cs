using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GitFlowStartReleaseWindow : Window
{
    public GitFlowStartReleaseViewModel ViewModel { get; } = new();

    public GitFlowStartReleaseWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string FullName, BranchItem? StartPoint)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.Name)
            ? null
            : (ViewModel.FullName, ViewModel.SelectedBranch);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
