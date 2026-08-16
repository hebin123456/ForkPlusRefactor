using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RemoveTagWindow : Window
{
    public RemoveTagViewModel ViewModel { get; } = new();

    public RemoveTagWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public bool GetDeleteFromRemotes() => ViewModel.DeleteFromRemotesChecked;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(ViewModel.DeleteFromRemotesChecked);
}
