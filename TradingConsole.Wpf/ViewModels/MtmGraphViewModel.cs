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
        public ObservableCollection<PnlDataPoint> DrawdownHistory { get; } = new ObservableCollection<PnlDataPoint>();

        public MtmGraphViewModel(List<PnlDataPoint> pnlHistory)
        {
            if (pnlHistory == null || !pnlHistory.Any())
            {
                TotalMtm = 0;
                return;
            }

            var sortedHistory = pnlHistory.OrderBy(p => p.Timestamp).ToList();

            // --- FIX: Calculate summary metrics on the raw, sorted data ---
            CalculateSummaryMetrics(sortedHistory);

            // --- FIX: Populate the graph history with the raw, sorted data ---
            foreach (var point in sortedHistory)
            {
                PnlHistory.Add(point);
            }

            // --- FIX: Calculate the drawdown graph based on the raw, sorted data ---
            CalculateDrawdownGraph(sortedHistory);
        }

        private void CalculateSummaryMetrics(List<PnlDataPoint> rawSortedHistory)
        {
            if (!rawSortedHistory.Any()) return;

            TotalMtm = rawSortedHistory.Last().Pnl;
            MinMtmDataPoint = rawSortedHistory.OrderBy(p => p.Pnl).First();
            MaxMtmDataPoint = rawSortedHistory.OrderBy(p => p.Pnl).Last();

            decimal maxDrawdownValue = 0;
            decimal peakPnl = decimal.MinValue;

            foreach (var pnlPoint in rawSortedHistory)
            {
                if (pnlPoint.Pnl > peakPnl)
                {
                    peakPnl = pnlPoint.Pnl;
                }

                decimal currentDrawdown = peakPnl - pnlPoint.Pnl;

                if (currentDrawdown > maxDrawdownValue)
                {
                    maxDrawdownValue = currentDrawdown;
                }
            }

            MaxDrawdown = maxDrawdownValue;
        }

        private void CalculateDrawdownGraph(List<PnlDataPoint> sortedHistory)
        {
            DrawdownHistory.Clear();
            decimal peakPnl = decimal.MinValue;
            foreach (var pnlPoint in sortedHistory)
            {
                if (pnlPoint.Pnl > peakPnl)
                {
                    peakPnl = pnlPoint.Pnl;
                }
                decimal currentDrawdown = peakPnl - pnlPoint.Pnl;
                DrawdownHistory.Add(new PnlDataPoint { Timestamp = pnlPoint.Timestamp, Pnl = -currentDrawdown });
            }
        }
    }
}
