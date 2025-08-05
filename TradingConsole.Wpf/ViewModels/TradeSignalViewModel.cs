// In TradingConsole.Wpf/ViewModels/TradeSignalViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    /// <summary>
    /// Represents the sentiment of a market factor.
    /// </summary>
    public enum FactorSentiment
    {
        Bullish,
        Bearish,
        Neutral
    }

    /// <summary>
    /// A view model for a single market factor to be displayed in the UI.
    /// </summary>
    public class FactorViewModel : INotifyPropertyChanged
    {
        private string _factorName = string.Empty;
        public string FactorName { get => _factorName; set { _factorName = value; OnPropertyChanged(); } }

        private string _factorValue = string.Empty;
        public string FactorValue { get => _factorValue; set { _factorValue = value; OnPropertyChanged(); } }

        private FactorSentiment _sentiment;
        public FactorSentiment Sentiment { get => _sentiment; set { _sentiment = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class TradeSignalViewModel : INotifyPropertyChanged
    {
        private AnalysisResult? _niftyAnalysisResult;
        public AnalysisResult? NiftyAnalysisResult { get => _niftyAnalysisResult; set { _niftyAnalysisResult = value; OnPropertyChanged(); } }

        public ObservableCollection<FactorViewModel> BullishFactors { get; } = new ObservableCollection<FactorViewModel>();
        public ObservableCollection<FactorViewModel> BearishFactors { get; } = new ObservableCollection<FactorViewModel>();


        public TradeSignalViewModel()
        {
        }

        public void UpdateSignalResult(AnalysisResult newResult)
        {
            // This view is only for indices, specifically the first one it receives (assumed to be Nifty).
            if (newResult.Symbol != "Nifty 50")
            {
                return;
            }

            if (NiftyAnalysisResult == null)
            {
                NiftyAnalysisResult = newResult;
            }
            else
            {
                NiftyAnalysisResult.Update(newResult);
            }

            UpdateFactorLists(NiftyAnalysisResult);
        }

        /// <summary>
        /// Processes an AnalysisResult and categorizes its data points into Bullish and Bearish lists for the UI.
        /// </summary>
        private void UpdateFactorLists(AnalysisResult result)
        {
            BullishFactors.Clear();
            BearishFactors.Clear();

            var allFactors = new List<FactorViewModel>();

            // Market Structure & Context
            AddFactor(allFactors, "Multi-Day Structure", result.MarketStructure, s => s.Contains("Up") ? FactorSentiment.Bullish : s.Contains("Down") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Opening Type", result.OpenTypeSignal, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Daily Bias", result.DailyBias, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "vs. Yesterday's Profile", result.YesterdayProfileSignal, s => s.Contains("Above") || s.Contains("Lower Y-Value") ? FactorSentiment.Bullish : s.Contains("Below") || s.Contains("Upper Y-Value") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Initial Balance", result.InitialBalanceSignal, s => s.Contains("Breakout") ? FactorSentiment.Bullish : s.Contains("Breakdown") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Price Action & Key Levels
            AddFactor(allFactors, "Price vs. VWAP", result.PriceVsVwapSignal, s => s.Contains("Above") ? FactorSentiment.Bullish : s.Contains("Below") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "VWAP Bands", result.VwapBandSignal, s => s.Contains("Lower") ? FactorSentiment.Bullish : s.Contains("Upper") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "5m Candle Pattern", result.CandleSignal5Min, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Volume & Open Interest
            AddFactor(allFactors, "Volume", result.VolumeSignal, s => s.Contains("Burst") ? (result.LTP > result.Vwap ? FactorSentiment.Bullish : FactorSentiment.Bearish) : FactorSentiment.Neutral);
            AddFactor(allFactors, "Futures OI", result.OiSignal, s => s == "Long Buildup" || s == "Short Covering" ? FactorSentiment.Bullish : s == "Short Buildup" || s == "Long Unwinding" ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Institutional Intent", result.InstitutionalIntent, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Volatility Dynamics
            AddFactor(allFactors, "IV Rank", $"{result.IvRank:F2}%", v => result.IvRank < 50 ? FactorSentiment.Bullish : FactorSentiment.Bearish); // Low IV is bullish for option buyers
            AddFactor(allFactors, "Intraday Volatility", result.AtrSignal5Min, s => s.Contains("Expanding") ? (result.LTP > result.Vwap ? FactorSentiment.Bullish : FactorSentiment.Bearish) : FactorSentiment.Neutral);
            AddFactor(allFactors, "IV Skew", result.IvSkewSignal, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Momentum & Divergence
            AddFactor(allFactors, "5m RSI Divergence", result.RsiSignal5Min, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "5m OBV Divergence", result.ObvDivergenceSignal5Min, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "EMA Trend (5m/15m)", $"{result.EmaSignal5Min} / {result.EmaSignal15Min}", s => result.EmaSignal5Min == "Bullish Cross" && result.EmaSignal15Min == "Bullish Cross" ? FactorSentiment.Bullish : result.EmaSignal5Min == "Bearish Cross" && result.EmaSignal15Min == "Bearish Cross" ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Populate the final lists
            foreach (var factor in allFactors.Where(f => f.Sentiment == FactorSentiment.Bullish))
            {
                BullishFactors.Add(factor);
            }
            foreach (var factor in allFactors.Where(f => f.Sentiment == FactorSentiment.Bearish))
            {
                BearishFactors.Add(factor);
            }
        }

        /// <summary>
        /// Helper method to create and add a factor to the list.
        /// </summary>
        private void AddFactor(List<FactorViewModel> factors, string name, string value, Func<string, FactorSentiment> sentimentEvaluator)
        {
            if (string.IsNullOrEmpty(value) || value == "N/A" || value == "Neutral")
                return;

            var sentiment = sentimentEvaluator(value);
            if (sentiment != FactorSentiment.Neutral)
            {
                factors.Add(new FactorViewModel { FactorName = name, FactorValue = value, Sentiment = sentiment });
            }
        }

        /// <summary>
        /// Overload for numeric values.
        /// </summary>
        private void AddFactor<T>(List<FactorViewModel> factors, string name, string value, Func<T, FactorSentiment> sentimentEvaluator) where T : struct
        {
            if (string.IsNullOrEmpty(value) || value == "N/A" || value == "Neutral")
                return;

            // This is a simplified version; a real implementation would need to parse T from the string value.
            // For this use case, the string-based evaluation is sufficient.
            var sentiment = sentimentEvaluator(default(T)); // This part needs more robust logic if used.
            if (sentiment != FactorSentiment.Neutral)
            {
                factors.Add(new FactorViewModel { FactorName = name, FactorValue = value, Sentiment = sentiment });
            }
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
