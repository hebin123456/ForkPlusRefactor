using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class AiReviewPreferencesUserControl : UserControl
{
    public AiReviewPreferencesUserControl()
    {
        InitializeComponent();
        DataContext = new AiReviewPreferencesViewModel();
    }
    public AiReviewPreferencesViewModel ViewModel => (AiReviewPreferencesViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
