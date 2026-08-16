using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Controls;

/// <summary>
/// Avalonia stub of the WPF <c>BindableGitPointView</c>: shows a single
/// IGitPoint row, optionally with subject and secondary line.
///
/// Usage modes:
/// 1. Set <see cref="Subject"/> / <see cref="Detail"/> directly on the control.
/// 2. Use as <c>ItemsControl.ItemTemplate</c>: each item's <c>DataContext</c>
///    is queried for <c>Subject</c> / <c>Detail</c> via <see cref="GitPoint"/>
///    (a dynamic accessor that falls back to <c>DataContext</c>).
/// </summary>
public partial class BindableGitPointView : UserControl
{
    public static readonly global::Avalonia.AvaloniaProperty<string?> SubjectProperty =
        global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, string?>(nameof(Subject));

    public static readonly global::Avalonia.AvaloniaProperty<string?> DetailProperty =
        global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, string?>(nameof(Detail));

    public static readonly global::Avalonia.AvaloniaProperty<object?> GitPointProperty =
        global::Avalonia.AvaloniaProperty.Register<BindableGitPointView, object?>(nameof(GitPoint));

    private TextBlock? _subjectText;
    private TextBlock? _detailText;

    public string? Subject
    {
        get => (string?)GetValue(SubjectProperty);
        set
        {
            SetValue(SubjectProperty, value);
            if (_subjectText != null && GitPoint == null) _subjectText.Text = value ?? string.Empty;
        }
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set
        {
            SetValue(DetailProperty, value);
            if (_detailText != null && GitPoint == null) _detailText.Text = value ?? string.Empty;
        }
    }

    /// <summary>
    /// 当作为 ItemsControl 模板时，DataContext 会被传给此属性；
    /// 此时通过反射取 <c>Subject</c> / <c>Detail</c>（或 <c>Name</c> / <c>Sha</c>），
    /// 并自动写回 <see cref="Subject"/> / <see cref="Detail"/> 以便外部读出。
    /// </summary>
    public object? GitPoint
    {
        get => GetValue(GitPointProperty);
        set
        {
            SetValue(GitPointProperty, value);
            // 同步到 Subject / Detail，使属性读取也可用
            if (value == null)
            {
                // 保留现有 Subject/Detail 不动
                RenderFromGitPoint(value);
                return;
            }
            var t = value.GetType();
            var subject = TryReadString(t, value, "Subject", "Name", "DisplayName", "FullName");
            var detail = TryReadString(t, value, "Detail", "Description", "Sha", "BranchName");
            if (subject != null) SetValue(SubjectProperty, subject);
            if (detail != null) SetValue(DetailProperty, detail);
            RenderFromGitPoint(value);
        }
    }

    public BindableGitPointView()
    {
        InitializeComponent();
        _subjectText = this.FindControl<TextBlock>("SubjectText");
        _detailText = this.FindControl<TextBlock>("DetailText");
        RenderFromGitPoint(GitPoint);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void RenderFromGitPoint(object? gp)
    {
        if (_subjectText == null || _detailText == null) return;
        if (gp == null)
        {
            // 直接属性模式：使用 Subject/Detail
            _subjectText.Text = Subject ?? string.Empty;
            _detailText.Text = Detail ?? string.Empty;
            return;
        }
        var t = gp.GetType();
        var subject = TryReadString(t, gp, "Subject", "Name", "DisplayName", "FullName");
        var detail = TryReadString(t, gp, "Detail", "Description", "Sha", "BranchName");
        _subjectText.Text = subject ?? string.Empty;
        _detailText.Text = detail ?? string.Empty;
    }

    private static string? TryReadString(System.Type t, object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = t.GetProperty(n);
            if (p != null && p.CanRead)
            {
                var v = p.GetValue(obj);
                if (v != null) return v.ToString();
            }
        }
        return null;
    }
}
