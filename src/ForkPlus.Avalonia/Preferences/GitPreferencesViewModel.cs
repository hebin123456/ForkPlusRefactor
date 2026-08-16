using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-B 阶段：从 WPF 原版 <c>Preferences/GitUserControl.xaml</c> 机械转 Avalonia 的 ViewModel。
///
/// <para>WPF 控件映射：</para>
/// <list type="bullet">
///   <item><c>GitInstanceComboBox</c> ↔ <see cref="GitInstances"/> + <see cref="SelectedGitInstance"/></item>
///   <item><c>GitMmInstanceComboBox</c> ↔ <see cref="GitMmInstances"/> + <see cref="SelectedGitMmInstance"/></item>
///   <item><c>VerboseGitOutputCheckBox</c> ↔ <see cref="VerboseGitOutput"/></item>
///   <item><c>UserNameTextBox</c> ↔ <see cref="UserName"/></item>
///   <item><c>EmailTextBox</c> ↔ <see cref="Email"/></item>
/// </list>
/// </summary>
public sealed class GitPreferencesViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public GitPreferencesViewModel()
    {
        // 模拟 3 个 Git 实例：第 2 个是"Separator"占位（WPF 用 GitInstanceType=Separator 触发样式变分隔线）
        var instances = new List<GitInstanceViewModel>
        {
            new GitInstanceViewModel("System Git (/usr/bin/git)", "/usr/bin/git", GitInstanceKind.Normal),
            new GitInstanceViewModel("──────────", "", GitInstanceKind.Separator),
            new GitInstanceViewModel("Bundled Biturbo (libbiturbo.so)", "libbiturbo.so", GitInstanceKind.Normal),
        };
        GitInstances = instances;
        SelectedGitInstance = instances[0];

        var mmInstances = new List<GitInstanceViewModel>
        {
            new GitInstanceViewModel("System git-mm", "git-mm", GitInstanceKind.Normal),
            new GitInstanceViewModel("──────────", "", GitInstanceKind.Separator),
            new GitInstanceViewModel("Bundled git-mm (libgitmm.so)", "libgitmm.so", GitInstanceKind.Normal),
        };
        GitMmInstances = mmInstances;
        SelectedGitMmInstance = mmInstances[0];

        VerboseGitOutput = false;
        UserName = "Fork User";
        Email = "user@example.com";
    }

    public IList<GitInstanceViewModel> GitInstances { get; }
    public IList<GitInstanceViewModel> GitMmInstances { get; }

    private GitInstanceViewModel? _selectedGitInstance;
    public GitInstanceViewModel? SelectedGitInstance
    {
        get => _selectedGitInstance;
        set { if (_selectedGitInstance != value) { _selectedGitInstance = value; OnPropertyChanged(); } }
    }

    private GitInstanceViewModel? _selectedGitMmInstance;
    public GitInstanceViewModel? SelectedGitMmInstance
    {
        get => _selectedGitMmInstance;
        set { if (_selectedGitMmInstance != value) { _selectedGitMmInstance = value; OnPropertyChanged(); } }
    }

    private bool _verboseGitOutput;
    public bool VerboseGitOutput
    {
        get => _verboseGitOutput;
        set { if (_verboseGitOutput != value) { _verboseGitOutput = value; OnPropertyChanged(); } }
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum GitInstanceKind { Normal, Separator }

public sealed class GitInstanceViewModel
{
    public GitInstanceViewModel(string displayName, string fileName, GitInstanceKind kind)
    {
        DisplayName = displayName;
        FileName = fileName;
        GitInstanceType = kind;
    }
    public string DisplayName { get; }
    public string FileName { get; }
    public GitInstanceKind GitInstanceType { get; }
}
