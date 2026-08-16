using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class GoToLineWindow : Window
{
    public GoToLineViewModel ViewModel { get; } = new();

    public GoToLineWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>OK 时返回用户输入的行号；Cancel 返回 null。</summary>
    public int? GetResult() => int.TryParse(ViewModel.LineNumber.ToString(), out var n) ? n : null;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
