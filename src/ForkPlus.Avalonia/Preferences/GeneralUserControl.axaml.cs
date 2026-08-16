using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-A：WPF 原版 <c>Preferences/GeneralUserControl.xaml</c> 的 Avalonia 等价物。
/// 在 WPF 中这是 <c>PreferencesWindow</c> 的一个 Tab（"General"）；
/// Avalonia 端我们单独包成 Window 便于独立测试 + 截图。
/// </summary>
public partial class GeneralUserControl : UserControl
{
    public GeneralUserControl()
    {
        InitializeComponent();
        DataContext = new GeneralPreferencesViewModel();
        // 选 LanguageComboBox 初始项 + 监听选择变化写回 VM
        var langCb = this.FindControl<ComboBox>("LanguageComboBox");
        if (langCb != null)
        {
            string lang = ((GeneralPreferencesViewModel)DataContext!).Language;
            foreach (var o in langCb.Items)
            {
                if (o is ComboBoxItem { Tag: string code } && code == lang)
                {
                    langCb.SelectedItem = o;
                    break;
                }
            }
            langCb.SelectionChanged += (_, _) =>
            {
                if (langCb.SelectedItem is ComboBoxItem { Tag: string newCode })
                {
                    ((GeneralPreferencesViewModel)DataContext!).Language = newCode;
                }
            };
        }
    }

    public GeneralPreferencesViewModel ViewModel => (GeneralPreferencesViewModel)DataContext!;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
