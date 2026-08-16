using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RevertRevisionWindow : Window
{
    public RevertRevisionViewModel ViewModel { get; } = new();

    public RevertRevisionWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (bool Commit, int? ParentIndex) GetResult() =>
        (ViewModel.CommitChecked, ViewModel.SelectedParent is null
            ? null
            : ViewModel.Parents.IndexOf(ViewModel.SelectedParent) + 1);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
