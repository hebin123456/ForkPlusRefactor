using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ForkPlus.Avalonia.Dialogs;

namespace ForkPlus.Avalonia.Dialogs;

/// <summary>Radio button ⇄ <see cref="CreateBranchViewModel.LocalChangesOption"/> converters.</summary>
public static class LocalChangesOptionConverters
{
    public static readonly IValueConverter DoNotChange =
        new EnumEqualsConverter(CreateBranchViewModel.LocalChangesOption.DoNotChange);
    public static readonly IValueConverter StashAndReapply =
        new EnumEqualsConverter(CreateBranchViewModel.LocalChangesOption.StashAndReapply);
    public static readonly IValueConverter Discard =
        new EnumEqualsConverter(CreateBranchViewModel.LocalChangesOption.Discard);

    private sealed class EnumEqualsConverter : IValueConverter
    {
        private readonly object _expected;
        public EnumEqualsConverter(object expected) => _expected = expected;
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value?.Equals(_expected) ?? false;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is true) ? _expected : global::Avalonia.AvaloniaProperty.UnsetValue;
    }
}
