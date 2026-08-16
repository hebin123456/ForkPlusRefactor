using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-F：从 WPF <c>AiReviewPreferencesUserControl.xaml</c> 机械转（不含 AI 联线）。</summary>
public sealed class AiReviewPreferencesViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AiReviewPreferencesViewModel()
    {
        ServiceUrl = "https://api.openai.com";
        ApiKey = "";
        Model = "gpt-4o";
        AutoFetchModels = true;
        RetryCount = 3;
        Timeout = 60;
        StatusText = "";
        AvailableModels = new ObservableCollection<string> { "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo", "claude-3.5-sonnet" };
        Skills = new ObservableCollection<AiSkillEntry>
        {
            new AiSkillEntry { Name = "code-review", Content = "Review the diff carefully..." },
            new AiSkillEntry { Name = "commit-message", Content = "Write a Conventional Commits message..." },
        };
    }

    private string _serviceUrl = "";
    public string ServiceUrl
    {
        get => _serviceUrl;
        set { if (_serviceUrl != value) { _serviceUrl = value; OnPropertyChanged(); } }
    }

    private string _apiKey = "";
    public string ApiKey
    {
        get => _apiKey;
        set { if (_apiKey != value) { _apiKey = value; OnPropertyChanged(); } }
    }

    private string _model = "";
    public string Model
    {
        get => _model;
        set { if (_model != value) { _model = value; OnPropertyChanged(); } }
    }

    private bool _autoFetchModels;
    public bool AutoFetchModels
    {
        get => _autoFetchModels;
        set { if (_autoFetchModels != value) { _autoFetchModels = value; OnPropertyChanged(); } }
    }

    private int _retryCount;
    public int RetryCount
    {
        get => _retryCount;
        set { if (_retryCount != value) { _retryCount = value; OnPropertyChanged(); } }
    }

    private int _timeout;
    public int Timeout
    {
        get => _timeout;
        set { if (_timeout != value) { _timeout = value; OnPropertyChanged(); } }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { if (_statusText != value) { _statusText = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<string> AvailableModels { get; }
    public ObservableCollection<AiSkillEntry> Skills { get; }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class AiSkillEntry
{
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
}
