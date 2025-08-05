using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TradingConsole.Wpf
{
    /// <summary>
    /// Interaction logic for OrderEntryWindow.xaml
    /// </summary>
    public partial class OrderEntryWindow : Window
    {
        public OrderEntryWindow()
        {
            InitializeComponent();
            // ADDED: Hook into the Closed event for cleanup
            this.Closed += OrderEntryWindow_Closed;
        }

        private void OrderEntryWindow_Closed(object? sender, EventArgs e)
        {
            // Ensure the ViewModel is disposed to unsubscribe from WebSocket events
            if (this.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
