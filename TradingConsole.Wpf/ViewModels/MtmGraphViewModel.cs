// TradingConsole.Wpf/ViewModels/MtmGraphViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    public class MtmGraphViewModel : ObservableModel
    {
        private decimal _totalMtm;
        public decimal TotalMtm { get => _totalMtm; set => SetProperty(ref _totalMtm, value); }

        private PnlDataPoint? _minMtmDataPoint;
        public PnlDataPoint? MinMtmDataPoint { get => _minMtmDataPoint; set => SetProperty(ref _minMtmDataPoint, value); }

        private PnlDataPoint? _maxMtmDataPoint;
        public PnlDataPoint? MaxMtmDataPoint { get => _maxMtmDataPoint; set => SetProperty(ref _maxMtmDataPoint, value); }

        private decimal _maxDrawdown;
        public decimal MaxDrawdown { get => _maxDrawdown; set => SetProperty(ref _maxDrawdown, value); }

        public ObservableCollection<PnlDataPoint> PnlHistory { get; } = new ObservableCollection<PnlDataPoint>();

        public MtmGraphViewModel(List<PnlDataPoint> pnlHistory)
        {
            if (pnlHistory == null || !pnlHistory.Any())
            {
                TotalMtm = 0;
                return;
            }

            foreach (var point in pnlHistory)
            {
                PnlHistory.Add(point);
            }

            CalculateSummaryMetrics(pnlHistory);
        }

        private void CalculateSummaryMetrics(List<PnlDataPoint> pnlHistory)
        {
            TotalMtm = pnlHistory.Last().Pnl;

            MinMtmDataPoint = pnlHistory.OrderBy(p => p.Pnl).First();
            MaxMtmDataPoint = pnlHistory.OrderBy(p => p.Pnl).Last();

            decimal maxDrawdown = 0;
            decimal peakPnl = decimal.MinValue;

            foreach (var pnlPoint in pnlHistory)
            {
                if (pnlPoint.Pnl > peakPnl)
                {
                    peakPnl = pnlPoint.Pnl;
                }

                decimal currentDrawdown = peakPnl - pnlPoint.Pnl;

                if (currentDrawdown > maxDrawdown)
                {
                    maxDrawdown = currentDrawdown;
                }
            }

            MaxDrawdown = maxDrawdown;
        }
    }
}
