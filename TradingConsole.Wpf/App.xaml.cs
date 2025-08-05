using System.Globalization;
using System.Threading;
using System.Windows;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- SET GLOBAL CULTURE FOR INR (₹) ---
            var cultureInfo = new CultureInfo("en-IN");
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            // -----------------------------------------

            // --- SIMPLIFIED LOGIN FLOW ---
            // Create the login window and its view model.
            var loginWindow = new LoginWindow();
            loginWindow.DataContext = new LoginViewModel();

            // Show the login window. The LoginViewModel will handle everything from here.
            loginWindow.Show();
        }
    }
}

