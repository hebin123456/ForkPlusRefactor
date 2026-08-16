using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class AddSubmoduleWindow : Window
{
    public AddSubmoduleViewModel ViewModel { get; } = new();

    public AddSubmoduleWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Url, string Path, bool FetchNested)? GetResult() =>
        !ViewModel.IsValid
            ? null
            : (ViewModel.RepositoryUrl, ViewModel.LocalPath, ViewModel.FetchNestedSubmodulesChecked);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
