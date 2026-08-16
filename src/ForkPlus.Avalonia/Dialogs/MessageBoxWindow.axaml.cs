using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class MessageBoxWindow : Window
{
    public MessageBoxViewModel ViewModel { get; } = new();

    public MessageBoxWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public MessageBoxResult GetResult() => ViewModel.Result;

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        ViewModel.Result = MessageBoxResult.Cancel;
        Close(ViewModel.Result);
    }
    private void OnOk(object? sender, RoutedEventArgs e)
    {
        ViewModel.Result = MessageBoxResult.Ok;
        Close(ViewModel.Result);
    }
}
