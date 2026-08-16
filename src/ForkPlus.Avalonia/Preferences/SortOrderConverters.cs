using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ForkPlus.Avalonia.Preferences;

/// <summary>
/// M9-A：把 <see cref="RevisionSortOrder"/> 映射到"是否选中"（用于 RadioButton.IsChecked 双向绑定）。
/// WPF 原版用两个独立的 Click 事件维护，这里用 Converter 让 XAML 保持声明式。
/// </summary>
public sealed class SortOrderToBoolTopoConverter : IValueConverter
{
    public static readonly SortOrderToBoolTopoConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RevisionSortOrder o && o == RevisionSortOrder.Topo;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? RevisionSortOrder.Topo : RevisionSortOrder.Date;
}

public sealed class SortOrderToBoolDateConverter : IValueConverter
{
    public static readonly SortOrderToBoolDateConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RevisionSortOrder o && o == RevisionSortOrder.Date;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? RevisionSortOrder.Date : RevisionSortOrder.Topo;
}
