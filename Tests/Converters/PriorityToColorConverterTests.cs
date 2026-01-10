using System.Windows.Media;
using DotNetWatcher.Converters;
using DotNetWatcher.Models;

namespace Tests.Converters;

public class PriorityToColorConverterTests
{
    private readonly PriorityToColorConverter _converter = new();

    [Fact]
    public void Convert_ImpactStatus_ReturnsCorrectBrush()
    {
        // Act
        var result = _converter.Convert(ImpactStatus.Impact, typeof(Brush), null, null);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Color.FromRgb(239, 68, 68), brush.Color); // Red
    }

    [Fact]
    public void Convert_PotentialStatus_ReturnsCorrectBrush()
    {
        // Act
        var result = _converter.Convert(ImpactStatus.Potential, typeof(Brush), null, null);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Color.FromRgb(245, 158, 11), brush.Color); // Orange
    }

    [Fact]
    public void Convert_InvalidValue_ReturnsTransparent()
    {
        // Act
        var result = _converter.Convert("invalid", typeof(Brush), null, null);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Color.FromRgb(245, 158, 11), brush.Color); // Amber-Orange default if invalid to take risk based approach
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Act & Assert
        Assert.Throws<NotImplementedException>(() => 
            _converter.ConvertBack(Brushes.Red, typeof(ImpactStatus), null, null));
    }
}