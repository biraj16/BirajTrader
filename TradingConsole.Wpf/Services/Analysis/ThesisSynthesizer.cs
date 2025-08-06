// TradingConsole.Wpf/Services/Analysis/ThesisSynthesizer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services.Analysis;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    public class ThesisSynthesizer
    {
        private readonly SettingsViewModel _settingsViewModel;
        private readonly SignalLoggerService _signalLoggerService;
        private readonly NotificationService _notificationService;
        private readonly AnalysisStateManager _stateManager;

        public ThesisSynthesizer(SettingsViewModel settingsViewModel, SignalLoggerService signalLoggerService, NotificationService notificationService, AnalysisStateManager stateManager)
        {
            _settingsViewModel = settingsViewModel;
            _signalLoggerService = signalLoggerService;
            _notificationService = notificationService;
            _stateManager = stateManager;
        }

        public void SynthesizeTradeSignal(AnalysisResult result)
        {
            if (result.InstrumentGroup != "INDEX") return;

            MarketThesis thesis = UpdateIntradayThesis(result);
            result.MarketThesis = thesis;

            var (bullDrivers, bearDrivers, conviction, isChoppy) = CalculateConfluenceScore(result, thesis);
            result.BullishDrivers = bullDrivers;
            result.BearishDrivers = bearDrivers;

            if (_stateManager.CurrentMarketPhase == MarketPhase.Opening)
            {
                conviction = (int)Math.Round(conviction * 0.5); // Reduce conviction by 50% during open
            }

            conviction = ApplyTrendFilter(result, conviction);
            result.ConvictionScore = conviction;

            string playbook;
            if (isChoppy)
            {
                playbook = "Choppy / Conflicting Signals";
                thesis = MarketThesis.Choppy;
                result.MarketThesis = thesis;
            }
            else if (conviction >= 7) playbook = "Strong Bullish Conviction";
            else if (conviction >= 3) playbook = "Moderate Bullish Conviction";
            else if (conviction <= -7) playbook = "Strong Bearish Conviction";
            else if (conviction <= -3) playbook = "Moderate Bearish Conviction";
            else playbook = "Neutral / Observe";

            string newPrimarySignal = "Neutral";
            if (!isChoppy)
            {
                if (conviction >= 3) newPrimarySignal = "Bullish";
                else if (conviction <= -3) newPrimarySignal = "Bearish";
            }

            string oldPrimarySignal = result.PrimarySignal;
            result.PrimarySignal = newPrimarySignal;
            result.FinalTradeSignal = playbook;
            result.MarketNarrative = GenerateMarketNarrative(result);

            if (result.PrimarySignal != oldPrimarySignal && oldPrimarySignal != "Initializing")
            {
                if (_stateManager.LastSignalTime.TryGetValue(result.SecurityId, out var lastTime) && (DateTime.UtcNow - lastTime).TotalSeconds < 60)
                {
                    return;
                }
                _stateManager.LastSignalTime[result.SecurityId] = DateTime.UtcNow;

                _signalLoggerService.LogSignal(result);
                Task.Run(() => _notificationService.SendTelegramSignalAsync(result, oldPrimarySignal));
            }
        }

        private int ApplyTrendFilter(AnalysisResult result, int currentConviction)
        {
            bool isAtSupport = result.CustomLevelSignal == "At Key Support" || result.DayRangeSignal == "Near Low" || result.VwapBandSignal == "At Lower Band";
            bool isAtResistance = result.CustomLevelSignal == "At Key Resistance" || result.DayRangeSignal == "Near High" || result.VwapBandSignal == "At Upper Band";

            if (result.MarketStructure == "Trending Up")
            {
                if (currentConviction < 0) return 0;
                if (currentConviction > 0 && isAtSupport) return currentConviction + 2;
            }

            if (result.MarketStructure == "Trending Down")
            {
                if (currentConviction > 0) return 0;
                if (currentConviction < 0 && isAtResistance) return currentConviction - 2;
            }

            return currentConviction;
        }

        /// <summary>
        /// --- REFACTORED LOGIC ---
        /// This method now implements a state-dependent "Playbook" approach. It selects the correct
        /// set of signal drivers based on the current market thesis (Trending, Range-Bound, etc.),
        /// making the analysis much more context-aware and robust.
        /// </summary>
        private (List<string> BullishDrivers, List<string> BearishDrivers, int Score, bool IsChoppy) CalculateConfluenceScore(AnalysisResult r, MarketThesis thesis)
        {
            var bullDrivers = new List<string>();
            var bearDrivers = new List<string>();
            int bullScore = 0;
            int bearScore = 0;

            // Step 1: Select the correct "Playbook" of drivers based on the market thesis
            IEnumerable<SignalDriver> driversToEvaluate;
            switch (thesis)
            {
                case MarketThesis.Bullish_Trend:
                case MarketThesis.Bearish_Trend:
                case MarketThesis.Bullish_Rotation:
                case MarketThesis.Bearish_Rotation:
                    driversToEvaluate = _settingsViewModel.Strategy.TrendingBullDrivers.Concat(_settingsViewModel.Strategy.TrendingBearDrivers);
                    break;
                case MarketThesis.Balancing:
                    driversToEvaluate = _settingsViewModel.Strategy.RangeBoundBullishDrivers.Concat(_settingsViewModel.Strategy.RangeBoundBearishDrivers);
                    break;
                default: // Indeterminate, Choppy, etc.
                    driversToEvaluate = Enumerable.Empty<SignalDriver>();
                    break;
            }

            // Also consider volatile drivers if the regime is volatile
            if (r.MarketRegime == "High Volatility")
            {
                driversToEvaluate = driversToEvaluate.Concat(_settingsViewModel.Strategy.VolatileBullishDrivers).Concat(_settingsViewModel.Strategy.VolatileBearishDrivers);
            }

            // Step 2: Evaluate only the selected drivers
            foreach (var driver in driversToEvaluate.Where(d => d.IsEnabled))
            {
                if (IsSignalActive(r, driver.Name))
                {
                    if (driver.Weight > 0)
                    {
                        bullScore += driver.Weight;
                        bullDrivers.Add($"{driver.Name} (+{driver.Weight})");
                    }
                    else
                    {
                        bearScore += driver.Weight;
                        bearDrivers.Add($"{driver.Name} ({driver.Weight})");
                    }
                }
            }

            // Step 3: Smarter Chop Detection
            // If there's strong conviction on BOTH sides, the market is conflicting. Stand aside.
            bool isChoppy = (bullScore >= 5 && Math.Abs(bearScore) >= 5) || r.GammaSignal == "Balanced OTM Gamma";

            int finalScore = bullScore + bearScore;
            return (bullDrivers, bearDrivers, finalScore, isChoppy);
        }

        private bool IsSignalActive(AnalysisResult r, string driverName)
        {
            // --- Helper booleans for context, mirroring the old logic ---
            bool isBullishPattern = r.CandleSignal5Min.Contains("Bullish");
            bool isBearishPattern = r.CandleSignal5Min.Contains("Bearish");
            bool atSupport = r.DayRangeSignal == "Near Low" || r.VwapBandSignal == "At Lower Band" || r.MarketProfileSignal.Contains("VAL");
            bool atResistance = r.DayRangeSignal == "Near High" || r.VwapBandSignal == "At Upper Band" || r.MarketProfileSignal.Contains("VAH");
            bool volumeConfirmed = r.VolumeSignal == "Volume Burst";
            bool isNotInStrongTrend = r.MarketThesis != MarketThesis.Bullish_Trend && r.MarketThesis != MarketThesis.Bearish_Trend;

            switch (driverName)
            {
                // --- Confluence Signals ---
                case "Confluence Momentum (Bullish)":
                    return r.PriceVsVwapSignal == "Above VWAP" && r.EmaSignal5Min == "Bullish Cross" && r.InstitutionalIntent.Contains("Bullish");
                case "Confluence Momentum (Bearish)":
                    return r.PriceVsVwapSignal == "Below VWAP" && r.EmaSignal5Min == "Bearish Cross" && r.InstitutionalIntent.Contains("Bearish");

                // --- Volatility Signals ---
                case "Option Breakout Setup":
                    return r.VolatilityStateSignal == "IV Squeeze Setup";
                case "Range Contraction":
                    return r.AtrSignal5Min == "Vol Contracting";

                // --- Market Profile Signals ---
                case "True Acceptance Above Y-VAH": return r.MarketProfileSignal == "True Acceptance Above Y-VAH";
                case "True Acceptance Below Y-VAL": return r.MarketProfileSignal == "True Acceptance Below Y-VAL";
                case "Look Above and Fail at Y-VAH": return r.MarketProfileSignal == "Look Above and Fail at Y-VAH";
                case "Look Below and Fail at Y-VAL": return r.MarketProfileSignal == "Look Below and Fail at Y-VAL";
                case "Initiative Buying Above Y-VAH": return r.MarketProfileSignal == "Initiative Buying Above Y-VAH";
                case "Initiative Selling Below Y-VAL": return r.MarketProfileSignal == "Initiative Selling Below Y-VAL";
                case "IB breakout is extending": return r.InitialBalanceSignal == "IB Extension Up";
                case "IB breakdown is extending": return r.InitialBalanceSignal == "IB Extension Down";

                // --- Standard Trend Signals ---
                case "Price above VWAP": return r.PriceVsVwapSignal == "Above VWAP";
                case "Price below VWAP": return r.PriceVsVwapSignal == "Below VWAP";
                case "5m VWAP EMA confirms bullish trend": return r.VwapEmaSignal5Min == "Bullish Cross";
                case "5m VWAP EMA confirms bearish trend": return r.VwapEmaSignal5Min == "Bearish Cross";
                case "OI confirms new longs": return r.OiSignal == "Long Buildup";
                case "OI confirms new shorts": return r.OiSignal == "Short Buildup";
                case "High OTM Call Gamma": return r.GammaSignal == "High OTM Call Gamma";
                case "High OTM Put Gamma": return r.GammaSignal == "High OTM Put Gamma";
                case "Institutional Intent is Bullish": return r.InstitutionalIntent.Contains("Bullish");
                case "Institutional Intent is Bearish": return r.InstitutionalIntent.Contains("Bearish");

                // --- Context-Aware Signals from Old Logic ---
                case "Bullish Pattern with Volume Confirmation": return isBullishPattern && volumeConfirmed;
                case "Bearish Pattern with Volume Confirmation": return isBearishPattern && volumeConfirmed;
                case "Bullish Pattern at Key Support": return isBullishPattern && atSupport;
                case "Bearish Pattern at Key Resistance": return isBearishPattern && atResistance;
                case "Bullish Skew Divergence (Full)": return r.IvSkewSignal == "Bullish Skew Divergence (Full)" && isNotInStrongTrend;
                case "Bearish Skew Divergence (Full)": return r.IvSkewSignal == "Bearish Skew Divergence (Full)" && isNotInStrongTrend;
                case "Bullish OBV Div at range low": return r.ObvDivergenceSignal5Min.Contains("Bullish") && atSupport && isNotInStrongTrend;
                case "Bearish OBV Div at range high": return r.ObvDivergenceSignal5Min.Contains("Bearish") && atResistance && isNotInStrongTrend;
                case "Bullish RSI Div at range low": return r.RsiSignal5Min.Contains("Bullish") && atSupport && isNotInStrongTrend;
                case "Bearish RSI Div at range high": return r.RsiSignal5Min.Contains("Bearish") && atResistance && isNotInStrongTrend;
                case "Low volume suggests exhaustion (Bullish)": return r.VolumeSignal != "Volume Burst" && r.AtrSignal5Min == "Vol Contracting" && r.DayRangeSignal == "Near Low";
                case "Low volume suggests exhaustion (Bearish)": return r.VolumeSignal != "Volume Burst" && r.AtrSignal5Min == "Vol Contracting" && r.DayRangeSignal == "Near High";

                default: return false;
            }
        }


        private MarketThesis UpdateIntradayThesis(AnalysisResult result) { DominantPlayer player = DetermineDominantPlayer(result); result.DominantPlayer = player; if (result.MarketStructure == "Trending Up") { if (player == DominantPlayer.Buyers) return MarketThesis.Bullish_Trend; if (player == DominantPlayer.Sellers) return MarketThesis.Bullish_Rotation; return MarketThesis.Bullish_Trend; } if (result.MarketStructure == "Trending Down") { if (player == DominantPlayer.Sellers) return MarketThesis.Bearish_Trend; if (player == DominantPlayer.Buyers) return MarketThesis.Bearish_Rotation; return MarketThesis.Bearish_Trend; } return MarketThesis.Balancing; }
        private DominantPlayer DetermineDominantPlayer(AnalysisResult result) { int buyerEvidence = 0; int sellerEvidence = 0; if (result.PriceVsVwapSignal == "Above VWAP") buyerEvidence++; if (result.PriceVsVwapSignal == "Below VWAP") sellerEvidence++; if (result.EmaSignal5Min == "Bullish Cross") buyerEvidence++; if (result.EmaSignal5Min == "Bearish Cross") sellerEvidence++; if (result.OiSignal == "Long Buildup") buyerEvidence++; if (result.OiSignal == "Short Buildup") sellerEvidence++; if (buyerEvidence > sellerEvidence) return DominantPlayer.Buyers; if (sellerEvidence > buyerEvidence) return DominantPlayer.Sellers; return DominantPlayer.Balance; }
        private string GenerateMarketNarrative(AnalysisResult r) { return $"Thesis: {r.MarketThesis}. Dominant Player: {r.DominantPlayer}. Open: {r.OpenTypeSignal}. vs VWAP: {r.PriceVsVwapSignal}."; }
    }
}
