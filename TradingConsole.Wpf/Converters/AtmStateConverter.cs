using System;
using System.Globalization;
using System.Windows.Data;

namespace TradingConsole.Wpf.Converters
{
    public class AtmStateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter determines if a strike is In-the-Money (ITM) for coloring.
            // It expects two values: [0] = StrikePrice, [1] = UnderlyingPrice
            if (values.Length < 2 || !(values[0] is decimal strikePrice) || !(values[1] is decimal underlyingPrice))
            {
                return "OTM"; // Default to OTM if data is not available
            }

            // A call is ITM if Strike < Spot. A put is ITM if Strike > Spot.
            // For the purpose of coloring the whole row, we'll use a simple threshold.
            // A more sophisticated approach could be passed in the 'parameter'.
            if (Math.Abs(strikePrice - underlyingPrice) < 10) return "ATM";

            // For now, let's consider a call-centric view for ITM coloring
            if (strikePrice < underlyingPrice) return "ITM_Call";

            // Or a put-centric view
            if (strikePrice > underlyingPrice) return "ITM_Put";

            return "OTM";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
