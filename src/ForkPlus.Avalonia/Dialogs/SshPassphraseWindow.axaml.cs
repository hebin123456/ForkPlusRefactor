using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class SshPassphraseWindow : Window
{
    public SshPassphraseViewModel ViewModel { get; } = new();

    public SshPassphraseWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public string? GetResult() => string.IsNullOrEmpty(ViewModel.Passphrase) ? null : ViewModel.Passphrase;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
