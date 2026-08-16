using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class CherryPickWindow : Window
{
    public CherryPickViewModel ViewModel { get; } = new();

    public CherryPickWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (bool CreateCommit, bool AppendOrigin, bool SignOff, string? ParentToSkip) GetResult() =>
        (ViewModel.CreateCommit, ViewModel.AppendOriginSha, ViewModel.SignOff,
         string.IsNullOrEmpty(ViewModel.ParentToSkipText) ? null : ViewModel.ParentToSkipText);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
