using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>
/// RadioButton ⇄ <see cref="LeanBranchingLocalChanges"/> 枚举互转：
/// - ConvertBack: 从 RadioButton.IsChecked（bool）+ ConverterParameter（枚举名）回写枚举
/// - Convert: 从枚举到 bool（判断是否匹配 ConverterParameter）
///
/// 用法：XAML 三个 RadioButton 共用同一 GroupName，每个绑到同一个枚举字段，
/// ConverterParameter 写枚举名（Avalonia XAML 不支持 enum 直接绑 RadioButton.IsChecked，
/// 这是 WPF 同样面临的问题）。
/// </summary>
public sealed class LeanBranchingRadioConverter : IValueConverter
{
    public static readonly LeanBranchingRadioConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LeanBranchingLocalChanges option && parameter is string s
            && Enum.TryParse<LeanBranchingLocalChanges>(s, out var p))
        {
            return option == p;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 只在 IsChecked=true 时回写枚举值
        if (value is true && parameter is string s
            && Enum.TryParse<LeanBranchingLocalChanges>(s, out var p))
        {
            return p;
        }
        return global::Avalonia.Data.BindingOperations.DoNothing;
    }
}
