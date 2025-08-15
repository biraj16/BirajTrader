// In TradingConsole.Core/Models/Position.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingConsole.Core.Models
{
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
            set { if (SetProperty(ref _lastTradedPrice, value)) { OnPropertyChanged(nameof(UnrealizedPnl)); } }
        }

        public decimal UnrealizedPnl
        {
            get
            {
                // --- THE FIX: Add a guard clause to prevent calculation with a zero LTP ---
                if (LastTradedPrice == 0)
                {
                    return 0; // Return zero until the first valid tick arrives
                }

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
    }
}