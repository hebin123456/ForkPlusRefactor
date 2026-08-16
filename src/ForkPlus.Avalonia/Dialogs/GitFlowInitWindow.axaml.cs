using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public sealed record GitFlowInitResult(
    string ProductionBranch,
    string DevelopmentBranch,
    string FeaturePrefix,
    string ReleasePrefix,
    string HotfixPrefix,
    string VersionTagPrefix);

public partial class GitFlowInitWindow : Window
{
    public GitFlowInitViewModel ViewModel { get; } = new();

    public GitFlowInitWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public GitFlowInitResult? GetResult() => !ViewModel.IsValid ? null : new GitFlowInitResult(
        ViewModel.ProductionBranch,
        ViewModel.DevelopmentBranch,
        ViewModel.FeaturePrefix,
        ViewModel.ReleasePrefix,
        ViewModel.HotfixPrefix,
        ViewModel.VersionTagPrefix);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
