using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Dottle.ViewModels; // Assuming enums are in this namespace

namespace Dottle.Converters;

/// <summary>
/// Converts an Enum value to true if it matches the converter parameter, otherwise false.
/// Used for binding RadioButton IsChecked to an Enum property.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // Check if the enum value matches the parameter (which should be the enum value this radio button represents)
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If this radio button is checked (value is true), return its corresponding enum value (parameter)
        if (value is true)
            return parameter;

        // Otherwise, do nothing (Avalonia handles unchecking others in the group)
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}

/// <summary>
/// Converts an Enum value to true (Visible) if it matches the converter parameter, otherwise false (Collapsed).
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false; // Default to not visible if values are null

        // Check if the enum value matches the parameter (which should be the enum value that triggers visibility)
        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Conversion back is not needed for visibility
        throw new NotImplementedException();
    }
}


/// <summary>
/// Multi-value converter to determine if the "Export as Single File" checkbox should be enabled.
/// Enabled only if CanSelectFolder is true AND SelectedExportFormat is FullText.
/// Expects two bindings: CanSelectFolder (bool) and SelectedExportFormat (ExportFormatType).
/// </summary>
public class EnableSingleFileExportConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.Count == 2 &&
            values[0] is bool canSelectFolder &&
            values[1] is ExportFormatType selectedFormat)
        {
            return canSelectFolder && selectedFormat == ExportFormatType.FullText;
        }
        return false; // Default to disabled if bindings aren't ready or types mismatch
    }

    // ConvertBack is not needed for IsEnabled
    // public object[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    // {
    //     throw new NotImplementedException();
    // }
}
