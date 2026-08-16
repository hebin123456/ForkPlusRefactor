using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class OpenRepositoryAlertWindow : Window
{
    public OpenRepositoryAlertViewModel ViewModel { get; } = new();

    public OpenRepositoryAlertWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnFirstButtonClick(object? sender, RoutedEventArgs e) => Close(true);
}
