using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    public class OiToWidthConverter : IMultiValueConverter
    {
        private const double MaxBarWidth = 120.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            {
                return 0.0;
            }

            // --- BUG FIX: Use System.Convert.ToDecimal for robustness ---
            // This correctly handles cases where the bound OI value is an int, long, or decimal.
            try
            {
                decimal currentOi = System.Convert.ToDecimal(values[0]);
                decimal maxOi = System.Convert.ToDecimal(values[1]);

                if (maxOi > 0)
                {
                    double width = ((double)currentOi / (double)maxOi) * MaxBarWidth;
                    // Ensure the width is a valid, non-negative number
                    return Math.Max(0, width);
                }
            }
            catch (Exception)
            {
                // If conversion fails for any reason, return 0 width
                return 0.0;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}