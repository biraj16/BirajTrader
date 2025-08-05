// In TradingConsole.Wpf/Views/NotNullToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TradingConsole.Wpf.Views
{
    /// <summary>
    /// Converts an object to Visibility.Visible if it's not null, otherwise Visibility.Collapsed.
    /// If a parameter is provided, it checks if the object's string representation matches the parameter.
    /// </summary>
    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string expectedValue)
            {
                return value?.ToString() == expectedValue ? Visibility.Visible : Visibility.Collapsed;
            }

            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
