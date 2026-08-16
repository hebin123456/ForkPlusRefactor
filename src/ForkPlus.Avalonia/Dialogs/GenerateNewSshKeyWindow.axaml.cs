using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GenerateNewSshKeyWindow : Window
{
    public GenerateNewSshKeyViewModel ViewModel { get; } = new();

    public GenerateNewSshKeyWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string KeyFileName, string Email)? GetResult() =>
        !ViewModel.IsValid
            ? null
            : (ViewModel.KeyFileName, ViewModel.Email);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
