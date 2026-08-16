using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CreateTagWindow : Window
{
    public CreateTagViewModel ViewModel { get; } = new();

    public CreateTagWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Name, string Message, bool Push)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.Name)
            ? null
            : (ViewModel.Name, ViewModel.Message ?? string.Empty, ViewModel.Push);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
