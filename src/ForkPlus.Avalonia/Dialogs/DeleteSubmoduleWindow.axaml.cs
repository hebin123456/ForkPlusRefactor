using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class DeleteSubmoduleWindow : Window
{
    public DeleteSubmoduleViewModel ViewModel { get; } = new();

    public DeleteSubmoduleWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Name, bool Force)? GetResult() =>
        ViewModel.IsValid ? (ViewModel.Name, ViewModel.ForceDelete) : null;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
