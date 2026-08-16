using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>M9-J：从 WPF <c>AddCustomRefspecWindow.xaml</c> 机械转。</summary>
public sealed class AddCustomRefspecViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AddCustomRefspecViewModel()
    {
        RemoteName = "origin";
        BranchName = "";
    }

    private string _remoteName = "";
    public string RemoteName
    {
        get => _remoteName;
        set { if (_remoteName != value) { _remoteName = value; OnPropertyChanged(); } }
    }

    private string _branchName = "";
    public string BranchName
    {
        get => _branchName;
        set { if (_branchName != value) { _branchName = value; OnPropertyChanged(); } }
    }

    /// <summary>完整 refspec，例如 "refs/heads/feature/x"</summary>
    public string FullRef => $"refs/remotes/{RemoteName}/{BranchName}";

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
