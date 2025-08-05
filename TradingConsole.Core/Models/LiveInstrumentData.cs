using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingConsole.Core.Models
{
    public class LiveInstrumentData : INotifyPropertyChanged
    {
        private decimal _ltp;
        private decimal _open;
        private decimal _high;
        private decimal _low;
        private decimal _close;
        private long _volume;
        private decimal _change;
        private decimal _changePercent;
        // ADDED: Missing properties that were previously in DashboardInstrument
        private int _lastTradedQuantity;
        private int _lastTradeTime;
        private decimal _avgTradePrice;

        public string SecurityId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string FeedType { get; set; } = "Ticker"; // Ticker or Quote

        public decimal LTP { get => _ltp; set { if (_ltp != value) { _ltp = value; OnPropertyChanged(); UpdateChange(); } } }
        public decimal Open { get => _open; set { if (_open != value) { _open = value; OnPropertyChanged(); } } }
        public decimal High { get => _high; set { if (_high != value) { _high = value; OnPropertyChanged(); } } }
        public decimal Low { get => _low; set { if (_low != value) { _low = value; OnPropertyChanged(); } } }
        public decimal Close { get => _close; set { if (_close != value) { _close = value; OnPropertyChanged(); UpdateChange(); } } }
        public long Volume { get => _volume; set { if (_volume != value) { _volume = value; OnPropertyChanged(); } } }

        // ADDED: Setters for the new properties
        public int LastTradedQuantity { get => _lastTradedQuantity; set { if (_lastTradedQuantity != value) { _lastTradedQuantity = value; OnPropertyChanged(); } } }
        public int LastTradeTime { get => _lastTradeTime; set { if (_lastTradeTime != value) { _lastTradeTime = value; OnPropertyChanged(); } } }
        public decimal AvgTradePrice { get => _avgTradePrice; set { if (_avgTradePrice != value) { _avgTradePrice = value; OnPropertyChanged(); } } }


        public decimal Change { get => _change; set { if (_change != value) { _change = value; OnPropertyChanged(); } } }
        public decimal ChangePercent { get => _changePercent; set { if (_changePercent != value) { _changePercent = value; OnPropertyChanged(); } } }

        private void UpdateChange()
        {
            if (Close > 0)
            {
                Change = LTP - Close;
                ChangePercent = (Change / Close);
            }
            else
            {
                Change = 0;
                ChangePercent = 0;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
