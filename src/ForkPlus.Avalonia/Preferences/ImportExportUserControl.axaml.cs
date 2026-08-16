using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

public partial class ImportExportUserControl : UserControl
{
    public ImportExportUserControl()
    {
        InitializeComponent();
        DataContext = new ImportExportViewModel();
    }
    public ImportExportViewModel ViewModel => (ImportExportViewModel)DataContext!;
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
