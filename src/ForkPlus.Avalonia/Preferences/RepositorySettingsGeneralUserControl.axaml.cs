using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class RepositorySettingsGeneralUserControl : UserControl
{
    public RepositorySettingsGeneralUserControl()
    {
        InitializeComponent();
        DataContext = new RepositorySettingsGeneralViewModel();
    }
    public RepositorySettingsGeneralViewModel ViewModel => (RepositorySettingsGeneralViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
