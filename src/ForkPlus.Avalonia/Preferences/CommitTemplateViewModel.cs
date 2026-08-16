using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-H：从 WPF <c>RepositorySettings/CommitTemplateUserControl.xaml</c> 机械转。</summary>
public sealed class CommitTemplateViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public CommitTemplateViewModel()
    {
        UseGlobalCommitTemplate = true;
        CommitTemplatePath = "(inheriting from global ~/.gitconfig)";
        CommitTemplateText = "";
        AddSignedOff = false;
        SkipCommitMessage = false;
        CommitMessageRegex = "";
    }

    private bool _useGlobalCommitTemplate;
    public bool UseGlobalCommitTemplate
    {
        get => _useGlobalCommitTemplate;
        set { if (_useGlobalCommitTemplate != value) { _useGlobalCommitTemplate = value; OnPropertyChanged(); } }
    }

    private string _commitTemplatePath = "";
    public string CommitTemplatePath
    {
        get => _commitTemplatePath;
        set { if (_commitTemplatePath != value) { _commitTemplatePath = value; OnPropertyChanged(); } }
    }

    private string _commitTemplateText = "";
    public string CommitTemplateText
    {
        get => _commitTemplateText;
        set { if (_commitTemplateText != value) { _commitTemplateText = value; OnPropertyChanged(); } }
    }

    private bool _addSignedOff;
    public bool AddSignedOff
    {
        get => _addSignedOff;
        set { if (_addSignedOff != value) { _addSignedOff = value; OnPropertyChanged(); } }
    }

    private bool _skipCommitMessage;
    public bool SkipCommitMessage
    {
        get => _skipCommitMessage;
        set { if (_skipCommitMessage != value) { _skipCommitMessage = value; OnPropertyChanged(); } }
    }

    private string _commitMessageRegex = "";
    public string CommitMessageRegex
    {
        get => _commitMessageRegex;
        set { if (_commitMessageRegex != value) { _commitMessageRegex = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
