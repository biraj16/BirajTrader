using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TradingConsole.Wpf.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _clientId = string.Empty;
        public string ClientId
        {
            get => _clientId;
            set { _clientId = value; OnPropertyChanged(nameof(ClientId)); }
        }

        private string _accessToken = string.Empty;
        public string AccessToken
        {
            get => _accessToken;
            set { _accessToken = value; OnPropertyChanged(nameof(AccessToken)); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            // Note: This assumes you have updated RelayCommand.cs as provided previously
            LoginCommand = new RelayCommand(ExecuteLoginCommand, CanExecuteLogin);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(AccessToken);
        }

        private void ExecuteLoginCommand(object? parameter)
        {
            try
            {
                MainViewModel mainViewModel = new MainViewModel(ClientId, AccessToken);
                MainWindow mainWindow = new MainWindow { DataContext = mainViewModel };

                // This logic correctly handles the window transition
                var currentWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                currentWindow?.Close();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Login Failed: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
