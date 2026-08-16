using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-I：从 WPF <c>RepositorySettings/IssueTrackerUserControl.xaml</c> 机械转。</summary>
public sealed class IssueTrackerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IssueTrackerViewModel()
    {
        IsEnabled = true;
        Rules = new ObservableCollection<BugTrackerRule>
        {
            new BugTrackerRule { Name = "GitHub", Level = "Local", Regex = @"#(\d+)", Url = "https://github.com/user/repo/issues/$1" },
            new BugTrackerRule { Name = "Jira", Level = "Shared", Regex = @"\b[A-Z]+-\d+\b", Url = "https://jira.company.com/browse/$0" },
        };
        SelectedRule = Rules[0];
        SampleMessage = "Fix #42 in PROJ-123";
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<BugTrackerRule> Rules { get; }

    private BugTrackerRule? _selectedRule;
    public BugTrackerRule? SelectedRule
    {
        get => _selectedRule;
        set { if (_selectedRule != value) { _selectedRule = value; OnPropertyChanged(); } }
    }

    private string _sampleMessage = "";
    public string SampleMessage
    {
        get => _sampleMessage;
        set { if (_sampleMessage != value) { _sampleMessage = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class BugTrackerRule
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "Local";
    public string Regex { get; set; } = "";
    public string Url { get; set; } = "";
}
