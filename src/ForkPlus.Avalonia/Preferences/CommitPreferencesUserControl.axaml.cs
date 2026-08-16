using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class CommitPreferencesUserControl : UserControl
{
    public CommitPreferencesUserControl()
    {
        InitializeComponent();
        DataContext = new CommitPreferencesViewModel();
    }

    public CommitPreferencesViewModel ViewModel => (CommitPreferencesViewModel)DataContext!;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
