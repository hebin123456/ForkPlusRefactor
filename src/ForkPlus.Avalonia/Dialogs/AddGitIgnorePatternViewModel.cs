namespace ForkPlus.Avalonia.Dialogs;

public sealed class AddGitIgnorePatternViewModel
{
    public string DialogTitle { get; set; } = "Add GitIgnore Pattern";
    public string DialogDescription { get; set; } = "Add a pattern to .gitignore";
    public string SubmitButtonTitle { get; set; } = "Add";

    public string PatternLabel { get; set; } = "Pattern:";
    public string PreviewLabel { get; set; } = "Preview:";
    public string PatternPlaceholder { get; set; } = "Enter gitignore pattern (supports multiple lines)";

    public string Pattern { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(Pattern);
}
