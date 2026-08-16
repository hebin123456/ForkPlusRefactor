using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class IssueTrackerUserControl : UserControl
{
    public IssueTrackerUserControl()
    {
        InitializeComponent();
        DataContext = new IssueTrackerViewModel();
    }
    public IssueTrackerViewModel ViewModel => (IssueTrackerViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
