using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class ApplyPatchWindow : Window
{
    public ApplyPatchViewModel ViewModel { get; } = new();

    public ApplyPatchWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Path, bool CreateCommit)? GetResult() =>
        string.IsNullOrWhiteSpace(ViewModel.Path) ? null : (ViewModel.Path, ViewModel.CreateCommit);

    private void OnBrowse(object? sender, RoutedEventArgs e)
    {
        // Hooked by caller via IFileDialogService.
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
