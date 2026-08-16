using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CreatePartialStashWindow : Window
{
    public CreatePartialStashViewModel ViewModel { get; } = new();

    public CreatePartialStashWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Message, bool IncludeUntracked, bool KeepIndex, System.Collections.Generic.List<string> Files) GetResult() =>
        (ViewModel.Message, ViewModel.IncludeUntracked, ViewModel.KeepIndex, ViewModel.SelectedFiles);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
