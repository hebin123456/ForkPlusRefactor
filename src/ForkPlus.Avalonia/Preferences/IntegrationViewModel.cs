using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-E：从 WPF <c>IntegrationUserControl.xaml</c> 机械转。</summary>
public sealed class IntegrationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IntegrationViewModel()
    {
        HighlightIssueTrackerLinks = true;
        MergeTools.Add(new ExternalTool { Name = "KDiff3", Path = "/usr/bin/kdiff3" });
        MergeTools.Add(new ExternalTool { Name = "Beyond Compare", Path = "/usr/bin/bcompare" });
        DiffTools.Add(new ExternalTool { Name = "Meld", Path = "/usr/bin/meld" });
        ShellTool = "Bash";
        ShellPath = "/bin/bash";
        ShellArguments = "-c {0}";
    }

    private bool _highlightIssueTrackerLinks;
    public bool HighlightIssueTrackerLinks
    {
        get => _highlightIssueTrackerLinks;
        set { if (_highlightIssueTrackerLinks != value) { _highlightIssueTrackerLinks = value; OnPropertyChanged(); } }
    }

    public System.Collections.ObjectModel.ObservableCollection<ExternalTool> MergeTools { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<ExternalTool> DiffTools { get; } = new();

    public static readonly string[] ShellTools = new[] { "Bash", "Zsh", "PowerShell", "Cmd" };

    private string _shellTool = "Bash";
    public string ShellTool
    {
        get => _shellTool;
        set { if (_shellTool != value) { _shellTool = value; OnPropertyChanged(); } }
    }

    private string _shellPath = "";
    public string ShellPath
    {
        get => _shellPath;
        set { if (_shellPath != value) { _shellPath = value; OnPropertyChanged(); } }
    }

    private string _shellArguments = "";
    public string ShellArguments
    {
        get => _shellArguments;
        set { if (_shellArguments != value) { _shellArguments = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ExternalTool
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}
