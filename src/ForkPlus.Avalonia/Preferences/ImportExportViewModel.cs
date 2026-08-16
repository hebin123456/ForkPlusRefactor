using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-D：从 WPF <c>ImportExportUserControl.xaml</c> 机械转。</summary>
public sealed class ImportExportViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ImportExportViewModel()
    {
        IncludeAccounts = true;
        StatusText = "";
    }

    private bool _includeAccounts;
    public bool IncludeAccounts
    {
        get => _includeAccounts;
        set { if (_includeAccounts != value) { _includeAccounts = value; OnPropertyChanged(); } }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
