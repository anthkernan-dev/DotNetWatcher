using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DotNetWatcher.Models;

namespace DotNetWatcher.Converters;

public class PriorityToColorConverter : IValueConverter
{
    // Modern color palette
    private static readonly SolidColorBrush ImpactBrush = new(Color.FromRgb(239, 68, 68));    // Red
    private static readonly SolidColorBrush PotentialImpactBrush = new(Color.FromRgb(245, 158, 11));     // Amber

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ImpactStatus priority)
        {
            return priority switch
            {
                ImpactStatus.Impact => ImpactBrush,
                ImpactStatus.Potential => PotentialImpactBrush,
                _ => PotentialImpactBrush
            };
        }

        // Risk-based approach: Default to amber for invalid/unexpected values
        // to ensure potential security issues remain visible rather than hidden
        return PotentialImpactBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}