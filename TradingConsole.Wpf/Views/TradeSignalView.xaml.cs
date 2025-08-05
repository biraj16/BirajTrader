using System.Windows.Controls;
using System.Windows.Input;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Views
{
    /// <summary>
    /// Interaction logic for TradeSignalView.xaml
    /// </summary>
    public partial class TradeSignalView : UserControl
    {
        public TradeSignalView()
        {
            InitializeComponent();
        }

        // --- REMOVED: This event handler was conflicting with the ToggleButton's own click handling.
        // The ToggleButton's IsChecked property is now solely responsible for expanding/collapsing the row details via data binding.
        // private void DataGridRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        // {
        //     if (sender is DataGridRow row && row.DataContext is AnalysisResult result)
        //     {
        //         result.IsExpanded = !result.IsExpanded;
        //     }
        // }
    }

    // --- A simple converter to hide the driver sections if the list is empty ---
    public class CountToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count && count > 0)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
