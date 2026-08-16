using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class IntegrationUserControl : UserControl
{
    public IntegrationUserControl()
    {
        InitializeComponent();
        DataContext = new IntegrationViewModel();
    }
    public IntegrationViewModel ViewModel => (IntegrationViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
