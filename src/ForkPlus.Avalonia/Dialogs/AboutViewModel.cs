namespace ForkPlus.Avalonia.Dialogs;

public sealed class AboutViewModel
{
    public string AppName { get; } = "ForkPlus";
    public string Version { get; set; } = "v3.9.0";
    public string Author { get; } = "Hebin";
    public string HomepageUrl { get; } = "https://hebin.me";
    public string Copyright { get; set; } = "Copyright © 2024";
}
