using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>AutoTooltipTextBlock</c>: a TextBlock that
/// auto-shows a tooltip when the text is truncated. For now just a TextBlock;
/// tooltip wiring is best-effort.
/// </summary>
public partial class AutoTooltipTextBlock : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> TextProperty =
        global::Avalonia.AvaloniaProperty.Register<AutoTooltipTextBlock, string?>(nameof(Text));

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

    public AutoTooltipTextBlock()
    {
        InitializeComponent();
        _textBlock = this.FindControl<TextBlock>("AutoTooltipText");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
