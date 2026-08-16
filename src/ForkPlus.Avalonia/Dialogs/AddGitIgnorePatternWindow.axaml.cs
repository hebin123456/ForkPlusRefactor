using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class AddGitIgnorePatternWindow : Window
{
    public AddGitIgnorePatternViewModel ViewModel { get; }

    public AddGitIgnorePatternWindow()
    {
        ViewModel = new AddGitIgnorePatternViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public string? GetResult() => string.IsNullOrWhiteSpace(ViewModel.Pattern) ? null : ViewModel.Pattern;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
