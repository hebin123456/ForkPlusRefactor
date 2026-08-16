using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>M9-G：从 WPF <c>RepositorySettings/GeneralUserControl.xaml</c> 机械转。</summary>
public sealed class RepositorySettingsGeneralViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public RepositorySettingsGeneralViewModel()
    {
        UseGlobalGitCredentials = true;
        UserName = "Repo User";
        Email = "repo@example.com";
        MainBranches = new ObservableCollection<MainBranchOption>
        {
            new MainBranchOption { Title = "main", ItemType = MainBranchItemKind.Normal },
            new MainBranchOption { Title = "──────────", ItemType = MainBranchItemKind.Separator },
            new MainBranchOption { Title = "develop", ItemType = MainBranchItemKind.Normal },
            new MainBranchOption { Title = "master", ItemType = MainBranchItemKind.Normal },
        };
        SelectedMainBranch = MainBranches[0];
        NoFastForward = false;
        TabWidth = 4;
    }

    private bool _useGlobalGitCredentials;
    public bool UseGlobalGitCredentials
    {
        get => _useGlobalGitCredentials;
        set { if (_useGlobalGitCredentials != value) { _useGlobalGitCredentials = value; OnPropertyChanged(); } }
    }

    private string _userName = "";
    public string UserName
    {
        get => _userName;
        set { if (_userName != value) { _userName = value; OnPropertyChanged(); } }
    }

    private string _email = "";
    public string Email
    {
        get => _email;
        set { if (_email != value) { _email = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<MainBranchOption> MainBranches { get; }

    private MainBranchOption? _selectedMainBranch;
    public MainBranchOption? SelectedMainBranch
    {
        get => _selectedMainBranch;
        set { if (_selectedMainBranch != value) { _selectedMainBranch = value; OnPropertyChanged(); } }
    }

    private bool _noFastForward;
    public bool NoFastForward
    {
        get => _noFastForward;
        set { if (_noFastForward != value) { _noFastForward = value; OnPropertyChanged(); } }
    }

    private int _tabWidth;
    public int TabWidth
    {
        get => _tabWidth;
        set { if (_tabWidth != value) { _tabWidth = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public enum MainBranchItemKind { Normal, Separator }

public sealed class MainBranchOption
{
    public string Title { get; set; } = "";
    public MainBranchItemKind ItemType { get; set; }
}
