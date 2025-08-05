// In TradingConsole.Core/Models/Position.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingConsole.Core.Models
{
    // --- REFACTORED: This class now inherits from ObservableModel to remove redundant code.
    public class Position : ObservableModel
    {
        private bool _isSelected;
        private decimal _lastTradedPrice;

        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        public string SecurityId { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal SellAverage { get; set; }
        public int BuyQuantity { get; set; }
        public int SellQuantity { get; set; }

        public decimal LastTradedPrice
        {
            get => _lastTradedPrice;
            // --- REFACTORED: Uses the SetProperty helper from the base class.
            set { if (SetProperty(ref _lastTradedPrice, value)) { OnPropertyChanged(nameof(UnrealizedPnl)); } }
        }

        public decimal UnrealizedPnl
        {
            get
            {
                if (Quantity > 0) // Long position
                {
                    return Quantity * (LastTradedPrice - AveragePrice);
                }
                else if (Quantity < 0) // Short position
                {
                    return System.Math.Abs(Quantity) * (AveragePrice - LastTradedPrice);
                }
                else
                {
                    return 0;
                }
            }
        }

        // --- REFACTORED: The PropertyChanged event and OnPropertyChanged method are now inherited
        // from ObservableModel and have been removed from this class.
    }
}
