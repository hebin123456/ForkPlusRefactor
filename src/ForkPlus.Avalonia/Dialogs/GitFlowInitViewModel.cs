namespace ForkPlus.Avalonia.Dialogs;

public sealed class GitFlowInitViewModel
{
    public string DialogTitle { get; set; } = "Initialize Git Flow";
    public string DialogDescription { get; set; } = "Configure git flow branch prefixes";
    public string SubmitButtonTitle { get; set; } = "Initialize";

    public string ProductionBranch { get; set; } = "master";
    public string DevelopmentBranch { get; set; } = "develop";
    public string FeaturePrefix { get; set; } = "feature/";
    public string ReleasePrefix { get; set; } = "release/";
    public string HotfixPrefix { get; set; } = "hotfix/";
    public string VersionTagPrefix { get; set; } = string.Empty;

    public string ProductionBranchPlaceholder { get; set; } = "master";
    public string DevelopmentBranchPlaceholder { get; set; } = "develop";
    public string FeaturePrefixPlaceholder { get; set; } = "feature/";
    public string ReleasePrefixPlaceholder { get; set; } = "release/";
    public string HotfixPrefixPlaceholder { get; set; } = "hotfix/";

    public bool IsValid => !string.IsNullOrWhiteSpace(ProductionBranch)
                        && !string.IsNullOrWhiteSpace(DevelopmentBranch);
}
