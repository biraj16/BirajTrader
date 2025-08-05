using System;
using System.Globalization;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    public class ValueToSignConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // --- MODIFIED: Handle string contains check for candlestick patterns ---
            if (value is string strValue && parameter is string paramStr)
            {
                // Note: Using Contains is case-sensitive. For case-insensitivity, you'd use IndexOf with StringComparison.
                if (paramStr == "ContainsBreakout" && strValue.Contains("Breakout")) return true;
                if (paramStr == "ContainsBreakdown" && strValue.Contains("Breakdown")) return true;
                if (paramStr == "ContainsBullish" && strValue.Contains("Bullish")) return true;
                if (paramStr == "ContainsBearish" && strValue.Contains("Bearish")) return true;
                if (paramStr == "ContainsDoji" && strValue.Contains("Doji")) return true;
                return false;
            }

            // Existing numeric logic
            if (value is decimal decValue)
            {
                if (decValue > 0) return "Positive";
                if (decValue < 0) return "Negative";
            }

            if (value is double dblValue)
            {
                if (dblValue > 0) return "Positive";
                if (dblValue < 0) return "Negative";
            }

            if (value is int intValue)
            {
                if (intValue > 0) return "Positive";
                if (intValue < 0) return "Negative";
            }

            return "Zero";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This method is not needed for our one-way binding.
            throw new NotImplementedException();
        }
    }
}
