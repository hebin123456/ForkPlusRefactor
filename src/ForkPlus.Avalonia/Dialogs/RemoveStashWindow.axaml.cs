using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class RemoveStashWindow : Window
{
    public RemoveStashViewModel ViewModel { get; } = new();

    public RemoveStashWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>返回 true 表示用户确认（OK），false 表示取消。</summary>
    public bool GetResult() => true;

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);
}
