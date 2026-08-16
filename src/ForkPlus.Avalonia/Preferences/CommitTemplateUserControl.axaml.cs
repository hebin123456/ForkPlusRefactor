using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class CommitTemplateUserControl : UserControl
{
    public CommitTemplateUserControl()
    {
        InitializeComponent();
        DataContext = new CommitTemplateViewModel();
    }
    public CommitTemplateViewModel ViewModel => (CommitTemplateViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
