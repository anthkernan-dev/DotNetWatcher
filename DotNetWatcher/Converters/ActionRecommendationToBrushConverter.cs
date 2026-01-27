using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DotNetWatcher.Models;

namespace DotNetWatcher.Converters;

public sealed class ActionRecommendationToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ActionRecommendation recommendation)
            return Brushes.Transparent;

        return recommendation switch
        {
            ActionRecommendation.Upgrade => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // red-ish
            ActionRecommendation.Mitigate => new SolidColorBrush(Color.FromRgb(239, 68, 68)), // red-ish
            ActionRecommendation.Monitor => new SolidColorBrush(Color.FromRgb(86, 202, 226)),    // neutral
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}