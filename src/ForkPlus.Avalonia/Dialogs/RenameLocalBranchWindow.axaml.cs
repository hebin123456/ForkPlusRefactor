using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RenameLocalBranchWindow : Window
{
    public RenameLocalBranchViewModel ViewModel { get; } = new();

    public RenameLocalBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string NewName, bool RenameRemote)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.NewName)
            ? null
            : (ViewModel.NewName, ViewModel.RenameRemoteChecked);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
