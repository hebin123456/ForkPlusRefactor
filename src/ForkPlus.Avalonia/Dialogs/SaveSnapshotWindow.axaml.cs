using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class SaveSnapshotWindow : Window
{
    public SaveSnapshotViewModel ViewModel { get; } = new();

    public SaveSnapshotWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public (string Message, bool StageNewFiles) GetResult() =>
        (ViewModel.Message, ViewModel.StageNewFilesChecked);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(GetResult());
}
