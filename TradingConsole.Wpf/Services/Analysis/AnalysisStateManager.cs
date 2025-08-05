// TradingConsole.Wpf/Services/Analysis/AnalysisStateManager.cs
// --- MODIFIED: Added MarketPhase state management ---
using System;
using System.Collections.Generic;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services.Analysis
{
    /// <summary>
    /// Manages the various state dictionaries required for real-time market analysis.
    /// This includes states for indicators, market profiles, IV, candles, and more.
    /// </summary>
    public class AnalysisStateManager
    {
        // --- NEW: Added state for the current market phase ---
        public MarketPhase CurrentMarketPhase { get; set; } = MarketPhase.PreOpen;

        public Dictionary<string, AnalysisResult> AnalysisResults { get; } = new();
        public Dictionary<string, MarketProfile> MarketProfiles { get; } = new();
        public Dictionary<string, List<MarketProfileData>> HistoricalMarketProfiles { get; } = new();

        public HashSet<string> BackfilledInstruments { get; } = new();
        public Dictionary<string, (decimal cumulativePriceVolume, long cumulativeVolume, List<decimal> ivHistory)> TickAnalysisState { get; } = new();

        public Dictionary<string, Dictionary<TimeSpan, List<Candle>>> MultiTimeframeCandles { get; } = new();
        public Dictionary<string, Dictionary<TimeSpan, EmaState>> MultiTimeframePriceEmaState { get; } = new();
        public Dictionary<string, Dictionary<TimeSpan, EmaState>> MultiTimeframeVwapEmaState { get; } = new();
        public Dictionary<string, Dictionary<TimeSpan, RsiState>> MultiTimeframeRsiState { get; } = new();
        public Dictionary<string, Dictionary<TimeSpan, AtrState>> MultiTimeframeAtrState { get; } = new();
        public Dictionary<string, Dictionary<TimeSpan, ObvState>> MultiTimeframeObvState { get; } = new();

        public Dictionary<string, IntradayIvState> IntradayIvStates { get; } = new();
        public Dictionary<string, IntradayIvState.CustomLevelState> CustomLevelStates { get; } = new();
        public Dictionary<string, (bool isBreakout, bool isBreakdown)> InitialBalanceState { get; } = new();

        public Dictionary<string, RelativeStrengthState> RelativeStrengthStates { get; } = new();
        public Dictionary<string, IvSkewState> IvSkewStates { get; } = new();
        public Dictionary<string, DateTime> LastSignalTime { get; } = new();

        public Dictionary<string, bool> IsInVolatilitySqueeze { get; } = new();


        private readonly List<TimeSpan> _timeframes = new()
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(3), // Added 3-min for OI analysis
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15)
        };

        public void InitializeStateForInstrument(string securityId, string symbol, string instrumentType)
        {
            if (BackfilledInstruments.Contains(securityId)) return;

            BackfilledInstruments.Add(securityId);
            AnalysisResults[securityId] = new AnalysisResult { SecurityId = securityId, Symbol = symbol, InstrumentGroup = instrumentType };
            TickAnalysisState[securityId] = (0, 0, new List<decimal>());
            MultiTimeframeCandles[securityId] = new Dictionary<TimeSpan, List<Candle>>();
            MultiTimeframePriceEmaState[securityId] = new Dictionary<TimeSpan, EmaState>();
            MultiTimeframeVwapEmaState[securityId] = new Dictionary<TimeSpan, EmaState>();
            MultiTimeframeRsiState[securityId] = new Dictionary<TimeSpan, RsiState>();
            MultiTimeframeAtrState[securityId] = new Dictionary<TimeSpan, AtrState>();
            MultiTimeframeObvState[securityId] = new Dictionary<TimeSpan, ObvState>();
            IsInVolatilitySqueeze[securityId] = false;

            if (instrumentType == "INDEX")
            {
                RelativeStrengthStates[securityId] = new RelativeStrengthState();
                IvSkewStates[securityId] = new IvSkewState();
                CustomLevelStates[symbol] = new IntradayIvState.CustomLevelState();
            }

            foreach (var tf in _timeframes)
            {
                MultiTimeframeCandles[securityId][tf] = new List<Candle>();
                MultiTimeframePriceEmaState[securityId][tf] = new EmaState();
                MultiTimeframeVwapEmaState[securityId][tf] = new EmaState();
                MultiTimeframeRsiState[securityId][tf] = new RsiState();
                MultiTimeframeAtrState[securityId][tf] = new AtrState();
                MultiTimeframeObvState[securityId][tf] = new ObvState();
            }
        }

        public List<Candle>? GetCandles(string securityId, TimeSpan timeframe)
        {
            if (MultiTimeframeCandles.TryGetValue(securityId, out var timeframes) &&
                timeframes.TryGetValue(timeframe, out var candles))
            {
                return candles;
            }
            return null;
        }

        public AnalysisResult GetResult(string securityId)
        {
            if (!AnalysisResults.ContainsKey(securityId))
            {
                AnalysisResults[securityId] = new AnalysisResult { SecurityId = securityId };
            }
            return AnalysisResults[securityId];
        }
    }
}
