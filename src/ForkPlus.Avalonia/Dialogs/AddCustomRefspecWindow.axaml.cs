using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class AddCustomRefspecWindow : Window
{
    public AddCustomRefspecViewModel ViewModel { get; }

    public AddCustomRefspecWindow()
    {
        ViewModel = new AddCustomRefspecViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.BranchName)) { Close(null); return; }
        Close(ViewModel.FullRef);
    }
}
