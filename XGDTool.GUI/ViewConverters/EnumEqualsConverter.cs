using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace XGDTool.GUI.ViewConverters;

public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? param, CultureInfo culture)
    {
        if (value == null || param == null)
            return false;

        return value.ToString() == param.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? param, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && param != null)
            return Enum.Parse(targetType, param.ToString()!);

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
