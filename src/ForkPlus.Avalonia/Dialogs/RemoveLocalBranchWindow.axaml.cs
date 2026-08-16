using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RemoveLocalBranchWindow : Window
{
    public RemoveLocalBranchViewModel ViewModel { get; } = new();

    public RemoveLocalBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (bool DeleteRemote, bool DeleteWorktree)? GetResult() =>
        (ViewModel.DeleteRemoteChecked, ViewModel.DeleteWorktreeChecked);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
