using Avalonia.Controls;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-A：宿主窗体。WPF 原版是 <c>PreferencesWindow</c> 的 Tab，
/// Avalonia 端用独立 Window 方便单测 + 截图。
/// </summary>
public sealed class GeneralPreferencesWindow : Window
{
    public GeneralPreferencesWindow()
    {
        Title = "Preferences - General (M9-A migrated from WPF)";
        Width = 560;
        Height = 620;
        Content = new GeneralUserControl();
        // 测试用：固定窗口大小
        SizeToContent = SizeToContent.Manual;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
