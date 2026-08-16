using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-A 阶段：从 WPF 原版 <c>Preferences/GeneralUserControl.xaml</c> 机械转 Avalonia 的 ViewModel。
/// 暂不接 <c>ForkPlusSettings.Default</c>（那需要完整设置系统），先用 Mock VM 提供 UI 绑定。
///
/// <para>对应 WPF 控件：</para>
/// <list type="bullet">
///   <item><c>SrcDirsListBox</c> ↔ <see cref="SourceDirs"/></item>
///   <item><c>ShowDiffChangeMarksCheckBox</c> ↔ <see cref="ShowDiffChangeMarks"/></item>
///   <item><c>CodeEditorFontSizeTextBox</c> ↔ <see cref="CodeEditorFontSize"/></item>
///   <item><c>DisableSyntaxHighlightingCheckBox</c> ↔ <see cref="DisableSyntaxHighlighting"/></item>
///   <item><c>TopologicalSortOrderRadioButton</c> / <c>DateSortOrderRadioButton</c> ↔ <see cref="RevisionSortOrder"/></item>
///   <item><c>FetchRemotesAutomaticallyCheckBox</c> ↔ <see cref="FetchRemotesAutomatically"/></item>
///   <item>...</item>
/// </list>
/// </summary>
public sealed class GeneralPreferencesViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public GeneralPreferencesViewModel()
    {
        SourceDirs = new List<SrcDirViewModel>
        {
            new SrcDirViewModel(@"C:\Projects"),
            new SrcDirViewModel(@"D:\GitHub"),
            new SrcDirViewModel(@"E:\Repos\work"),
        };
        // 默认值（与 WPF 原版 ForkPlusSettings.Default.* 对齐）
        ShowDiffChangeMarks = true;
        CodeEditorFontSize = 13.0;
        DisableSyntaxHighlighting = false;
        RevisionSortOrder = RevisionSortOrder.Topo;
        FetchRemotesAutomatically = true;
        FetchAllTags = false;
        UpdateRepoStatusAutomatically = true;
        UpdateSubmodulesAutomatically = true;
        PushAutomaticallyOnCommit = false;
        CompactBranchLabels = false;
        UndoRedoEnabled = false;
        SpaceCharacterReplacement = "-";
        Language = "en-US";
    }

    public IList<SrcDirViewModel> SourceDirs { get; }

    private bool _showDiffChangeMarks;
    public bool ShowDiffChangeMarks
    {
        get => _showDiffChangeMarks;
        set { if (_showDiffChangeMarks != value) { _showDiffChangeMarks = value; OnPropertyChanged(); } }
    }

    private double _codeEditorFontSize;
    public double CodeEditorFontSize
    {
        get => _codeEditorFontSize;
        set
        {
            // WPF 原版校验范围 [10, 40]
            if (value < 10) value = 10;
            if (value > 40) value = 40;
            if (Math.Abs(_codeEditorFontSize - value) > 0.001)
            {
                _codeEditorFontSize = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _disableSyntaxHighlighting;
    public bool DisableSyntaxHighlighting
    {
        get => _disableSyntaxHighlighting;
        set { if (_disableSyntaxHighlighting != value) { _disableSyntaxHighlighting = value; OnPropertyChanged(); } }
    }

    private RevisionSortOrder _revisionSortOrder;
    public RevisionSortOrder RevisionSortOrder
    {
        get => _revisionSortOrder;
        set { if (_revisionSortOrder != value) { _revisionSortOrder = value; OnPropertyChanged(); } }
    }

    private bool _fetchRemotesAutomatically;
    public bool FetchRemotesAutomatically
    {
        get => _fetchRemotesAutomatically;
        set { if (_fetchRemotesAutomatically != value) { _fetchRemotesAutomatically = value; OnPropertyChanged(); } }
    }

    private bool _fetchAllTags;
    public bool FetchAllTags
    {
        get => _fetchAllTags;
        set { if (_fetchAllTags != value) { _fetchAllTags = value; OnPropertyChanged(); } }
    }

    private bool _updateRepoStatusAutomatically;
    public bool UpdateRepoStatusAutomatically
    {
        get => _updateRepoStatusAutomatically;
        set { if (_updateRepoStatusAutomatically != value) { _updateRepoStatusAutomatically = value; OnPropertyChanged(); } }
    }

    private bool _updateSubmodulesAutomatically;
    public bool UpdateSubmodulesAutomatically
    {
        get => _updateSubmodulesAutomatically;
        set { if (_updateSubmodulesAutomatically != value) { _updateSubmodulesAutomatically = value; OnPropertyChanged(); } }
    }

    private bool _pushAutomaticallyOnCommit;
    public bool PushAutomaticallyOnCommit
    {
        get => _pushAutomaticallyOnCommit;
        set { if (_pushAutomaticallyOnCommit != value) { _pushAutomaticallyOnCommit = value; OnPropertyChanged(); } }
    }

    private bool _compactBranchLabels;
    public bool CompactBranchLabels
    {
        get => _compactBranchLabels;
        set { if (_compactBranchLabels != value) { _compactBranchLabels = value; OnPropertyChanged(); } }
    }

    private bool _undoRedoEnabled;
    public bool UndoRedoEnabled
    {
        get => _undoRedoEnabled;
        set { if (_undoRedoEnabled != value) { _undoRedoEnabled = value; OnPropertyChanged(); } }
    }

    /// <summary>WPF 原版 <c>Consts.Git.References.SpaceCharacterReplacements</c>，3 个候选项。</summary>
    public static readonly string[] SpaceCharacterReplacements = new[] { "-", "_", "%20" };

    private string _spaceCharacterReplacement = "-";
    public string SpaceCharacterReplacement
    {
        get => _spaceCharacterReplacement;
        set { if (_spaceCharacterReplacement != value) { _spaceCharacterReplacement = value; OnPropertyChanged(); } }
    }

    /// <summary>WPF 原版 <c>PreferencesLocalization.GetLanguages()</c> 的 mock：en-US / zh-CN / ja-JP / de-DE。</summary>
    public static readonly (string Code, string DisplayName)[] AvailableLanguages = new[]
    {
        ("en-US", "English (US)"),
        ("zh-CN", "中文（简体）"),
        ("ja-JP", "日本語"),
        ("de-DE", "Deutsch"),
    };

    private string _language = "en-US";
    public string Language
    {
        get => _language;
        set { if (_language != value) { _language = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum RevisionSortOrder
{
    Topo = 0,
    Date = 1,
}

/// <summary>WPF 原版 <c>SrcDirViewModel</c> 的 Avalonia 等价物（去掉 PlatformSpecific 路径处理）。</summary>
public sealed class SrcDirViewModel
{
    public SrcDirViewModel(string path) { Path = path; }
    public string Path { get; }
    public string Display => Path + (Path.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) ? "" : System.IO.Path.DirectorySeparatorChar.ToString());
}
