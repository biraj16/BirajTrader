// TradingConsole.Wpf/Converters/ScoreToColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TradingConsole.Wpf.Converters
{
    public class ScoreToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int score)
            {
                if (score >= 7) return new SolidColorBrush(Colors.LimeGreen);
                if (score >= 3) return new SolidColorBrush(Colors.PaleGreen);
                if (score <= -7) return new SolidColorBrush(Colors.Red);
                if (score <= -3) return new SolidColorBrush(Colors.Salmon);
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}