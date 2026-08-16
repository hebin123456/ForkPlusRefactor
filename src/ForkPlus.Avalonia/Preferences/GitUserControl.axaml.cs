using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-B：WPF 原版 <c>Preferences/GitUserControl.xaml</c> 的 Avalonia 等价物。
/// </summary>
public partial class GitUserControl : UserControl
{
    public GitUserControl()
    {
        InitializeComponent();
        DataContext = new GitPreferencesViewModel();
    }

    public GitPreferencesViewModel ViewModel => (GitPreferencesViewModel)DataContext!;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
