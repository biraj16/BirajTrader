// TradingConsole.Wpf/Services/Analysis/ThesisSynthesizer.cs
// --- MODIFIED: Added a strategic trend filter based on user's trading experience ---
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

            // --- NEW: Apply the user's strategic trend filter ---
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

        /// <summary>
        /// NEW: Applies a strategic filter based on the overall market trend, as per user's experience.
        /// This acts as a final gatekeeper for signals.
        /// </summary>
        private int ApplyTrendFilter(AnalysisResult result, int currentConviction)
        {
            bool isAtSupport = result.CustomLevelSignal == "At Key Support" || result.DayRangeSignal == "Near Low" || result.VwapBandSignal == "At Lower Band";
            bool isAtResistance = result.CustomLevelSignal == "At Key Resistance" || result.DayRangeSignal == "Near High" || result.VwapBandSignal == "At Upper Band";

            // Rule 1: In a bullish market ("Trending Up")
            if (result.MarketStructure == "Trending Up")
            {
                // Veto any sell signals (counter-trend shorts)
                if (currentConviction < 0)
                {
                    return 0; // Force to neutral
                }
                // Reward buy signals that occur at support levels (buy the dip)
                if (currentConviction > 0 && isAtSupport)
                {
                    return currentConviction + 2; // Add a bonus to the conviction score
                }
            }

            // Rule 2: In a bearish market ("Trending Down")
            if (result.MarketStructure == "Trending Down")
            {
                // Veto any buy signals (counter-trend longs)
                if (currentConviction > 0)
                {
                    return 0; // Force to neutral
                }
                // Reward sell signals that occur at resistance levels (sell the rip)
                if (currentConviction < 0 && isAtResistance)
                {
                    return currentConviction - 2; // Add a bonus to the conviction score (make it more negative)
                }
            }

            // In a balancing/choppy market, no filter is applied. Return the original score.
            return currentConviction;
        }

        private (List<string> BullishDrivers, List<string> BearishDrivers, int Score, bool IsChoppy) CalculateConfluenceScore(AnalysisResult r, MarketThesis thesis)
        {
            var bullDrivers = new List<string>();
            var bearDrivers = new List<string>();
            int score = 0;

            var allDrivers = _settingsViewModel.Strategy.TrendingBullDrivers
                .Concat(_settingsViewModel.Strategy.TrendingBearDrivers)
                .Concat(_settingsViewModel.Strategy.RangeBoundBullishDrivers)
                .Concat(_settingsViewModel.Strategy.RangeBoundBearishDrivers)
                .Concat(_settingsViewModel.Strategy.VolatileBullishDrivers)
                .Concat(_settingsViewModel.Strategy.VolatileBearishDrivers);

            foreach (var driver in allDrivers.Where(d => d.IsEnabled))
            {
                bool signalActive = IsSignalActive(r, driver.Name);
                if (signalActive)
                {
                    score += driver.Weight;
                    if (driver.Weight > 0)
                    {
                        bullDrivers.Add($"{driver.Name} (+{driver.Weight})");
                    }
                    else
                    {
                        bearDrivers.Add($"{driver.Name} ({driver.Weight})");
                    }
                }
            }

            bool isChoppy = r.GammaSignal == "Balanced OTM Gamma";

            return (bullDrivers, bearDrivers, score, isChoppy);
        }

        private bool IsSignalActive(AnalysisResult r, string driverName)
        {
            // This method maps the driver name from settings to the live analysis result
            switch (driverName)
            {
                case "Price above VWAP": return r.PriceVsVwapSignal == "Above VWAP";
                case "Price below VWAP": return r.PriceVsVwapSignal == "Below VWAP";
                case "5m VWAP EMA confirms bullish trend": return r.VwapEmaSignal5Min == "Bullish Cross";
                case "5m VWAP EMA confirms bearish trend": return r.VwapEmaSignal5Min == "Bearish Cross";
                case "OI confirms new longs": return r.OiSignal == "Long Buildup";
                case "OI confirms new shorts": return r.OiSignal == "Short Buildup";
                case "High OTM Call Gamma": return r.GammaSignal == "High OTM Call Gamma";
                case "High OTM Put Gamma": return r.GammaSignal == "High OTM Put Gamma";
                case "Bullish Pattern with Volume Confirmation": return r.CandleSignal5Min.Contains("Bullish") && r.VolumeSignal == "Volume Burst";
                case "Bearish Pattern with Volume Confirmation": return r.CandleSignal5Min.Contains("Bearish") && r.VolumeSignal == "Volume Burst";
                // Add more mappings here for all other drivers...
                default: return false;
            }
        }


        private MarketThesis UpdateIntradayThesis(AnalysisResult result) { DominantPlayer player = DetermineDominantPlayer(result); result.DominantPlayer = player; if (result.MarketStructure == "Trending Up") { if (player == DominantPlayer.Buyers) return MarketThesis.Bullish_Trend; if (player == DominantPlayer.Sellers) return MarketThesis.Bullish_Rotation; return MarketThesis.Bullish_Trend; } if (result.MarketStructure == "Trending Down") { if (player == DominantPlayer.Sellers) return MarketThesis.Bearish_Trend; if (player == DominantPlayer.Buyers) return MarketThesis.Bearish_Rotation; return MarketThesis.Bearish_Trend; } return MarketThesis.Balancing; }
        private DominantPlayer DetermineDominantPlayer(AnalysisResult result) { int buyerEvidence = 0; int sellerEvidence = 0; if (result.PriceVsVwapSignal == "Above VWAP") buyerEvidence++; if (result.PriceVsVwapSignal == "Below VWAP") sellerEvidence++; if (result.EmaSignal5Min == "Bullish Cross") buyerEvidence++; if (result.EmaSignal5Min == "Bearish Cross") sellerEvidence++; if (result.OiSignal == "Long Buildup") buyerEvidence++; if (result.OiSignal == "Short Buildup") sellerEvidence++; if (buyerEvidence > sellerEvidence) return DominantPlayer.Buyers; if (sellerEvidence > buyerEvidence) return DominantPlayer.Sellers; return DominantPlayer.Balance; }
        private string GenerateMarketNarrative(AnalysisResult r) { return $"Thesis: {r.MarketThesis}. Dominant Player: {r.DominantPlayer}. Open: {r.OpenTypeSignal}. vs VWAP: {r.PriceVsVwapSignal}."; }
    }
}
