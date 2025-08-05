using System;
using System.Windows;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // --- ADDED: Hook into the Closed event for graceful shutdown ---
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // --- ADDED: Ensure the ViewModel and its WebSocket are disposed ---
            if (this.DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}