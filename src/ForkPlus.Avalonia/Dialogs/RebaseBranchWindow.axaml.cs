using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RebaseBranchWindow : Window
{
    public RebaseBranchViewModel ViewModel { get; } = new();

    public RebaseBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (bool UpdateRefs, bool Autostash, System.Collections.Generic.List<string> DependentBranches) GetResult() =>
        (ViewModel.UpdateRefs, ViewModel.Autostash, ViewModel.DependentBranches);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
