using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PointManager.Converters;

/// <summary>true -> Collapsed, false -> Visible.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        return b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Positive -> green brush, negative -> red brush, zero -> grey.</summary>
public class DeltaToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush Pos =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x43, 0xA0, 0x47));
    private static readonly System.Windows.Media.Brush Neg =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35));
    private static readonly System.Windows.Media.Brush Zero = System.Windows.Media.Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value is int i ? i : 0;
        return d > 0 ? Pos : d < 0 ? Neg : Zero;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats an int with an explicit + sign for positive values.</summary>
public class SignedIntConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value is int i ? i : 0;
        return d > 0 ? "+" + d : d.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

