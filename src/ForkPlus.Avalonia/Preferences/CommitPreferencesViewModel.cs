using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-C：从 WPF 原版 <c>Preferences/CommitPreferencesUserControl.xaml</c> 机械转 Avalonia。
/// </summary>
public sealed class CommitPreferencesViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public CommitPreferencesViewModel()
    {
        CommitSubjectLowLimit = 4;
        CommitSubjectHighLimit = 50;
        SpellChecking = "System";
        PageGuideColumn = 72;
        CommitMessageRegex = @"^(?:(?:(?:feat|fix|chore|docs|refactor|test|style|perf|build|ci)(?:\([a-z0-9\-_/]+\))?))(?::|\s).{3,72}$";
    }

    private int _commitSubjectLowLimit;
    public int CommitSubjectLowLimit
    {
        get => _commitSubjectLowLimit;
        set { if (_commitSubjectLowLimit != value) { _commitSubjectLowLimit = value; OnPropertyChanged(); } }
    }

    private int _commitSubjectHighLimit;
    public int CommitSubjectHighLimit
    {
        get => _commitSubjectHighLimit;
        set { if (_commitSubjectHighLimit != value) { _commitSubjectHighLimit = value; OnPropertyChanged(); } }
    }

    private string _spellChecking = "System";
    public string SpellChecking
    {
        get => _spellChecking;
        set { if (_spellChecking != value) { _spellChecking = value; OnPropertyChanged(); } }
    }

    public static readonly string[] SpellCheckingOptions = new[] { "Disable", "System", "English" };

    private int _pageGuideColumn;
    public int PageGuideColumn
    {
        get => _pageGuideColumn;
        set { if (_pageGuideColumn != value) { _pageGuideColumn = value; OnPropertyChanged(); } }
    }

    private string _commitMessageRegex = "";
    public string CommitMessageRegex
    {
        get => _commitMessageRegex;
        set { if (_commitMessageRegex != value) { _commitMessageRegex = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
