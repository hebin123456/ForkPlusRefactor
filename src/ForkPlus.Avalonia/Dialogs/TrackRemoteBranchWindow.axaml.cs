using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class TrackRemoteBranchWindow : Window
{
    public TrackRemoteBranchViewModel ViewModel { get; } = new();

    public TrackRemoteBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string LocalBranchName, CreateBranchViewModel.LocalChangesOption LocalChanges)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.LocalBranchName)
            ? null
            : (ViewModel.LocalBranchName, ViewModel.LocalChanges);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
