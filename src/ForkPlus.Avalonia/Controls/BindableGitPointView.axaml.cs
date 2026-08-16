using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>BindableGitPointView</c>: shows a single
/// IGitPoint row, optionally with subject and secondary line.
/// </summary>
public partial class BindableGitPointView : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> SubjectProperty =
        global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, string?>(nameof(Subject));

    public static readonly global::Avalonia.AvaloniaProperty<string?> DetailProperty =
        global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, string?>(nameof(Detail));

    public string? Subject
    {
        get => (string?)GetValue(SubjectProperty);
        set => SetValue(SubjectProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public BindableGitPointView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
