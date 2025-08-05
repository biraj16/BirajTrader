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
            var selectedIds = new System.Collections.Generic.HashSet<string>(OpenPositions.Where(p => p.IsSelected).Select(p => p.SecurityId));

            foreach (var position in OpenPositions)
            {
                position.PropertyChanged -= Position_PropertyChanged;
            }

            OpenPositions.Clear();
            ClosedPositions.Clear();

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
                        uiPosition.IsSelected = selectedIds.Contains(uiPosition.SecurityId);
                        OpenPositions.Add(uiPosition);
                        uiPosition.PropertyChanged += Position_PropertyChanged;
                    }
                    else
                    {
                        ClosedPositions.Add(uiPosition);
                    }
                }
            }

            OnPropertyChanged(nameof(OpenPnl));
            OnPropertyChanged(nameof(BookedPnl));
            OnPropertyChanged(nameof(NetPnl));
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
