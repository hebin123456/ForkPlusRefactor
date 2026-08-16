using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class ApplyStashWindow : Window
{
    public ApplyStashViewModel ViewModel { get; } = new();

    public ApplyStashWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (StashItem? Stash, bool RestoreIndex)? GetResult() =>
        ViewModel.SelectedStash is null
            ? null
            : (ViewModel.SelectedStash, ViewModel.RestoreIndex);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
