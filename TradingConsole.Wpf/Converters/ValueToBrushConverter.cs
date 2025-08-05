using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TradingConsole.Wpf.Converters
{
    public class ValueToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter checks if a numeric value is positive, negative, or zero,
            // and returns the appropriate color brush for the UI.
            if (value is decimal decValue)
            {
                if (decValue > 0) return Brushes.Green;
                if (decValue < 0) return Brushes.Red;
            }

            if (value is double dblValue)
            {
                if (dblValue > 0) return Brushes.Green;
                if (dblValue < 0) return Brushes.Red;
            }

            // Return the default foreground color (or black) if zero or not a number
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This method is not needed for our one-way binding.
            throw new NotImplementedException();
        }
    }
}
