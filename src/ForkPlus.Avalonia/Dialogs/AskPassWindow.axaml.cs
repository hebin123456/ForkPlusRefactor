using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class AskPassWindow : Window
{
    public AskPassViewModel ViewModel { get; }

    public AskPassWindow()
    {
        ViewModel = new AskPassViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>OK 时返回 (Password, Remember)；Cancel 返回 null。</summary>
    public (string Password, bool Remember)? GetResult() =>
        string.IsNullOrEmpty(ViewModel.Password) ? null : (ViewModel.Password, ViewModel.Remember);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
