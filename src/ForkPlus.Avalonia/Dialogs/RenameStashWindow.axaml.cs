using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RenameStashWindow : Window
{
    public RenameStashViewModel ViewModel { get; } = new();

    public RenameStashWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public string? GetResult() => string.IsNullOrWhiteSpace(ViewModel.Message) ? null : ViewModel.Message;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
