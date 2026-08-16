using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CheckoutBranchWindow : Window
{
    public CheckoutBranchViewModel ViewModel { get; } = new();

    public CheckoutBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string SwitchTo, LeanBranchingLocalChanges Option) GetResult() =>
        (ViewModel.SwitchToText, ViewModel.LocalChangesOption);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
