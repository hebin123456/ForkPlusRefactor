using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>M9-L：从 WPF <c>AskPassWindow.xaml</c> 机械转。统一的"问密码"对话框（Git 推送 / 拉取时触发）。</summary>
public sealed class AskPassViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AskPassViewModel()
    {
        Prompt = "Password:";
        Password = "";
        Remember = false;
        IsPasswordMode = true;
    }

    private string _prompt = "";
    public string Prompt
    {
        get => _prompt;
        set { if (_prompt != value) { _prompt = value; OnPropertyChanged(); } }
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set { if (_password != value) { _password = value; OnPropertyChanged(); } }
    }

    private bool _remember;
    public bool Remember
    {
        get => _remember;
        set { if (_remember != value) { _remember = value; OnPropertyChanged(); } }
    }

    private bool _isPasswordMode = true;
    public bool IsPasswordMode
    {
        get => _isPasswordMode;
        set { if (_isPasswordMode != value) { _isPasswordMode = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
