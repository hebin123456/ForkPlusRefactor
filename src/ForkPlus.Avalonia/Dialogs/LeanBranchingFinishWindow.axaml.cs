using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class LeanBranchingFinishWindow : Window
{
    public LeanBranchingFinishViewModel ViewModel { get; } = new();

    public LeanBranchingFinishWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Current, string Main)? GetResult() => (ViewModel.CurrentBranchText, ViewModel.MainBranchText);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
