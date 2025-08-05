using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    public class ChangeOiToWidthConverter : IMultiValueConverter
    {
        public double MaxWidth { get; set; } = 75;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            {
                return 0.0;
            }

            // --- REVISED FIX: Handle multiple numeric types to be more robust ---
            try
            {
                decimal changeOi = System.Convert.ToDecimal(values[0]);
                decimal maxOiChange = System.Convert.ToDecimal(values[1]);

                if (maxOiChange > 0)
                {
                    double absoluteChange = (double)Math.Abs(changeOi);
                    double maxChange = (double)maxOiChange;
                    double width = (absoluteChange / maxChange) * MaxWidth;
                    return Math.Min(width, MaxWidth);
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
