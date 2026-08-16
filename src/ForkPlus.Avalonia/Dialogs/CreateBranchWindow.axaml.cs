using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CreateBranchWindow : Window
{
    public CreateBranchViewModel ViewModel { get; } = new();

    public CreateBranchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Name, bool Checkout, CreateBranchViewModel.LocalChangesOption Option)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.Name)
            ? null
            : (ViewModel.Name, ViewModel.CheckoutAfterCreate, ViewModel.LocalChanges);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
