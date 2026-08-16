using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>GitPointView</c>: shows a single line of git point
/// info (typically "branch · commitHash subject"). This is a placeholder; for
/// full functionality, host an <c>IGitPoint</c> presenter here.
/// </summary>
public partial class GitPointView : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> TextProperty =
        global::Avalonia.AvaloniaProperty.Register<GitPointView, string?>(nameof(Text));

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set
        {
            SetValue(TextProperty, value);
            if (_textBlock != null) _textBlock.Text = value ?? string.Empty;
        }
    }

    private TextBlock? _textBlock;

    public GitPointView()
    {
        InitializeComponent();
        _textBlock = this.FindControl<TextBlock>("GitPointText");
        if (_textBlock != null)
            _textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
