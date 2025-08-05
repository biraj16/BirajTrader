// In TradingConsole.Wpf/Converters/ExpanderGlyphConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    /// <summary>
    /// Converts a boolean value (IsExpanded) into a Segoe MDL2 Assets icon character.
    /// Used to show a right-facing or down-facing chevron in the UI.
    /// </summary>
    public class ExpanderGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the row is expanded, show a down-pointing chevron.
            if (value is bool isExpanded && isExpanded)
            {
                return "\uE70D"; // Segoe MDL2 Assets: ChevronDown
            }
            // Otherwise, show a right-pointing chevron.
            return "\uE70E"; // Segoe MDL2 Assets: ChevronRight
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
