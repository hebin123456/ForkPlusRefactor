using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class LongOperationWindow : Window
{
    public LongOperationViewModel ViewModel { get; } = new();

    public LongOperationWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    public void SetMessage(string message) => ViewModel.Message = message;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
