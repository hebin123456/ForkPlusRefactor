using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Dialogs;

public sealed class AddSubmoduleViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DialogTitle { get; set; } = "Add Submodule";
    public string DialogDescription { get; set; } = "Add a git submodule";
    public string SubmitButtonTitle { get; set; } = "Add";

    public string UrlPlaceholder { get; set; } = "Git Repository Url";
    public string PathPlaceholder { get; set; } = "Relative Local Path";
    public string FetchNestedSubmodulesLabel { get; set; } = "Fetch nested submodules";

    private string _repositoryUrl = string.Empty;
    public string RepositoryUrl
    {
        get => _repositoryUrl;
        set { if (_repositoryUrl != value) { _repositoryUrl = value; OnChanged(); OnChanged(nameof(IsValid)); } }
    }

    private string _localPath = string.Empty;
    public string LocalPath
    {
        get => _localPath;
        set { if (_localPath != value) { _localPath = value; OnChanged(); OnChanged(nameof(IsValid)); } }
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(RepositoryUrl) && !string.IsNullOrWhiteSpace(LocalPath);

    private string? _finalPathHint;
    public string? FinalPathHint
    {
        get => _finalPathHint;
        set { if (_finalPathHint != value) { _finalPathHint = value; OnChanged(); OnChanged(nameof(IsFinalPathHintVisible)); } }
    }

    public bool IsFinalPathHintVisible => !string.IsNullOrEmpty(FinalPathHint);

    public bool FetchNestedSubmodulesChecked { get; set; }

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
