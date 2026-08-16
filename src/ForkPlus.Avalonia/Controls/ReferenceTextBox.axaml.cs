using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>ReferenceTextBox</c>: like <c>TextBox</c> but with
/// a leading icon and placeholder. Used for branch / tag / remote name inputs.
/// </summary>
public partial class ReferenceTextBox : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> TextProperty =
        global::Avalonia.AvaloniaProperty.Register<ReferenceTextBox, string?>(nameof(Text));

    public static readonly global::Avalonia.AvaloniaProperty<string?> PlaceholderProperty =
        global::Avalonia.AvaloniaProperty.Register<ReferenceTextBox, string?>(nameof(Placeholder));

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set
        {
            SetValue(TextProperty, value);
            if (_textBox != null) _textBox.Text = value ?? string.Empty;
        }
    }

    public string? Placeholder
    {
        get => (string?)GetValue(PlaceholderProperty);
        set
        {
            SetValue(PlaceholderProperty, value);
            if (_textBox != null) _textBox.PlaceholderText = value;
        }
    }

    private TextBox? _textBox;

    public ReferenceTextBox()
    {
        InitializeComponent();
        _textBox = this.FindControl<TextBox>("InnerTextBox");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
