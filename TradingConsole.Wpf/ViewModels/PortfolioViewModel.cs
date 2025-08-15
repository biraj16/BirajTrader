// In TradingConsole.Wpf/ViewModels/PortfolioViewModel.cs

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi.Models; // ADDED: To find PositionResponse

namespace TradingConsole.Wpf.ViewModels
{
    public class PortfolioViewModel : ObservableModel
    {
        public ObservableCollection<Position> OpenPositions { get; } = new();
        public ObservableCollection<Position> ClosedPositions { get; } = new();
        public FundDetails FundDetails { get; } = new();

        public decimal OpenPnl => OpenPositions.Sum(p => p.UnrealizedPnl);
        public decimal BookedPnl => ClosedPositions.Sum(p => p.RealizedPnl);
        public decimal NetPnl => OpenPnl + BookedPnl;

        private bool? _selectAllOpenPositions;
        public bool? SelectAllOpenPositions
        {
            get => _selectAllOpenPositions;
            set
            {
                if (_selectAllOpenPositions != value)
                {
                    _selectAllOpenPositions = value;
                    foreach (var pos in OpenPositions)
                    {
                        pos.IsSelected = _selectAllOpenPositions ?? false;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public PortfolioViewModel()
        {
            OpenPositions.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(OpenPnl)); OnPropertyChanged(nameof(NetPnl)); };
            ClosedPositions.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(BookedPnl)); OnPropertyChanged(nameof(NetPnl)); };
        }

        public void UpdatePositions(System.Collections.Generic.List<PositionResponse>? positionsFromApi)
        {
            var newOpenPositions = new List<Position>();
            var newClosedPositions = new List<Position>();

            if (positionsFromApi != null)
            {
                foreach (var posData in positionsFromApi)
                {
                    var uiPosition = new Position
                    {
                        SecurityId = posData.SecurityId ?? string.Empty,
                        Ticker = posData.TradingSymbol ?? string.Empty,
                        Quantity = posData.NetQuantity,
                        AveragePrice = posData.BuyAverage,
                        LastTradedPrice = posData.LastTradedPrice,
                        RealizedPnl = posData.RealizedProfit,
                        ProductType = posData.ProductType ?? string.Empty,
                        SellAverage = posData.SellAverage,
                        BuyQuantity = posData.BuyQuantity,
                        SellQuantity = posData.SellQuantity
                    };

                    if (posData.NetQuantity != 0)
                    {
                        newOpenPositions.Add(uiPosition);
                    }
                    else
                    {
                        newClosedPositions.Add(uiPosition);
                    }
                }
            }

            // --- THE FIX: Synchronize the collections instead of clearing them ---
            SynchronizeCollection(OpenPositions, newOpenPositions);
            SynchronizeCollection(ClosedPositions, newClosedPositions);

            OnPropertyChanged(nameof(OpenPnl));
            OnPropertyChanged(nameof(BookedPnl));
            OnPropertyChanged(nameof(NetPnl));
        }

        private void SynchronizeCollection(ObservableCollection<Position> uiCollection, List<Position> newPositions)
        {
            // Unsubscribe from old property changed events
            foreach (var pos in uiCollection)
            {
                pos.PropertyChanged -= Position_PropertyChanged;
            }

            var positionsToRemove = uiCollection.Where(p => !newPositions.Any(np => np.SecurityId == p.SecurityId)).ToList();
            foreach (var pos in positionsToRemove)
            {
                uiCollection.Remove(pos);
            }

            foreach (var newPos in newPositions)
            {
                var existingPos = uiCollection.FirstOrDefault(p => p.SecurityId == newPos.SecurityId);
                if (existingPos != null)
                {
                    // Update existing position properties
                    existingPos.Quantity = newPos.Quantity;
                    existingPos.AveragePrice = newPos.AveragePrice;
                    existingPos.LastTradedPrice = newPos.LastTradedPrice;
                    existingPos.RealizedPnl = newPos.RealizedPnl;
                    existingPos.ProductType = newPos.ProductType;
                    existingPos.SellAverage = newPos.SellAverage;
                    existingPos.BuyQuantity = newPos.BuyQuantity;
                    existingPos.SellQuantity = newPos.SellQuantity;
                }
                else
                {
                    // Add new position
                    uiCollection.Add(newPos);
                }
            }

            // Subscribe to new property changed events
            foreach (var pos in uiCollection)
            {
                pos.PropertyChanged += Position_PropertyChanged;
            }
        }

        private void Position_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Position.UnrealizedPnl))
            {
                OnPropertyChanged(nameof(OpenPnl));
                OnPropertyChanged(nameof(NetPnl));
            }
        }
    }
}
