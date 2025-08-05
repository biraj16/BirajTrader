using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Globalization;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    /// <summary>
    /// Converts a color string (e.g., "LightGray") into a SolidColorPaint object for LiveCharts.
    /// </summary>
    public class LvcSolidColorPaintConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string colorString)
            {
                if (SKColor.TryParse(colorString, out var color))
                {
                    return new SolidColorPaint(color);
                }
            }
            // Return a default color if parsing fails
            return new SolidColorPaint(SKColors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
