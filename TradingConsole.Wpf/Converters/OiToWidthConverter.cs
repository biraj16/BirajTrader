using System;
using System.Globalization;
using System.Linq;
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

            if (values[0] is decimal currentOi && values[1] is long maxOi)
            {
                if (maxOi > 0)
                {
                    double width = ((double)currentOi / maxOi) * MaxBarWidth;
                    return Math.Max(0, width);
                }
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}