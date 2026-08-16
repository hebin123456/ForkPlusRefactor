using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>M9-K：从 WPF <c>AddGitIgnorePatternWindow.xaml</c> 机械转。</summary>
public sealed class AddGitIgnorePatternViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public AddGitIgnorePatternViewModel()
    {
        Pattern = "*.tmp\nbin/\nobj/\n.vs/\n";
        Preview = "(匹配的 4 个模式：*.tmp / bin/ / obj/ / .vs/)";
    }

    private string _pattern = "";
    public string Pattern
    {
        get => _pattern;
        set
        {
            if (_pattern != value)
            {
                _pattern = value;
                UpdatePreview();
                OnPropertyChanged();
            }
        }
    }

    private string _preview = "";
    public string Preview
    {
        get => _preview;
        set { if (_preview != value) { _preview = value; OnPropertyChanged(); } }
    }

    private void UpdatePreview()
    {
        var lines = Pattern.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Preview = lines.Length == 0
            ? "(空)"
            : $"(匹配 {lines.Length} 个模式：{string.Join(" / ", lines.Take(4))}{(lines.Length > 4 ? " ..." : "")})";
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
