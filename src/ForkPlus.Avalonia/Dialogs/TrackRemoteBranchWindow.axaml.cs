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

    public (string LocalBranchName, LeanBranchingLocalChanges Option)? GetResult() =>
        ViewModel.IsValid
            ? (ViewModel.LocalBranchName, ViewModel.LocalChangesOption)
            : null;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
