// In TradingConsole.Core/Models/FundDetails.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TradingConsole.Core.Models
{
    // --- REFACTORED: This class now inherits from ObservableModel to remove redundant code.
    public class FundDetails : ObservableModel
    {
        private decimal _availableBalance;
        public decimal AvailableBalance { get => _availableBalance; set => SetProperty(ref _availableBalance, value); }

        private decimal _utilizedMargin;
        public decimal UtilizedMargin { get => _utilizedMargin; set => SetProperty(ref _utilizedMargin, value); }

        private decimal _collateral;
        public decimal Collateral { get => _collateral; set => SetProperty(ref _collateral, value); }

        private decimal _withdrawableBalance;
        public decimal WithdrawableBalance { get => _withdrawableBalance; set => SetProperty(ref _withdrawableBalance, value); }

        // --- REFACTORED: The PropertyChanged event and OnPropertyChanged method are now inherited
        // from ObservableModel and have been removed from this class.
        // The SetProperty helper from the base class is now used in the property setters.
    }
}
