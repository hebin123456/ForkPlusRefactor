namespace ForkPlus.Avalonia.Dialogs;

public sealed class GenerateNewSshKeyViewModel
{
    public string DialogTitle { get; set; } = "Generate New SSH Key";
    public string DialogDescription { get; set; } = "Generate a new SSH key pair";
    public string SubmitButtonTitle { get; set; } = "Generate";

    public string KeyFileName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string KeyFileNamePlaceholder { get; set; } = "For example: fork_ssh_key";
    public string EmailPlaceholder { get; set; } = "email@domain.com";

    public bool IsValid => !string.IsNullOrWhiteSpace(KeyFileName) && !string.IsNullOrWhiteSpace(Email);
}
