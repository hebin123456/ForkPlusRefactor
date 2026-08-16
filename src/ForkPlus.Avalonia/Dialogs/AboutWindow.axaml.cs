using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Dialogs;

public partial class AboutWindow : Window
{
    public AboutViewModel ViewModel { get; } = new();

    public AboutWindow()
    {
        DataContext = ViewModel;
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnOpenHomepage(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Avalonia doesn't have built-in cross-platform launcher; use process start on supported platforms.
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ViewModel.HomepageUrl,
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // ignore — best effort
        }
    }
}
