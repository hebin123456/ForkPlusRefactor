using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>PlaceholderTextBox</c>: a TextBox with a placeholder.
/// </summary>
public partial class PlaceholderTextBox : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> TextProperty =
        global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, string?>(nameof(Text));

    public static readonly global::Avalonia.AvaloniaProperty<string?> PlaceholderProperty =
        global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, string?>(nameof(Placeholder));

    public static readonly global::Avalonia.AvaloniaProperty<bool> AcceptsReturnProperty =
        global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, bool>(nameof(AcceptsReturn));

    public static readonly global::Avalonia.AvaloniaProperty<global::Avalonia.Media.TextWrapping> TextWrappingProperty =
        global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, global::Avalonia.Media.TextWrapping>(nameof(TextWrapping));

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

    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty);
        set
        {
            SetValue(AcceptsReturnProperty, value);
            if (_textBox != null) _textBox.AcceptsReturn = value;
        }
    }

    public global::Avalonia.Media.TextWrapping TextWrapping
    {
        get => (global::Avalonia.Media.TextWrapping)GetValue(TextWrappingProperty);
        set
        {
            SetValue(TextWrappingProperty, value);
            if (_textBox != null) _textBox.TextWrapping = value;
        }
    }

    private TextBox? _textBox;

    public PlaceholderTextBox()
    {
        InitializeComponent();
        _textBox = this.FindControl<TextBox>("PlaceholderTextBoxControl");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
