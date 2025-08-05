// TradingConsole.Wpf/Services/Analysis/SignalGenerationService.cs
// --- MODIFIED: Added ATR and OBV signal calculations ---
using System;
using System.Collections.Generic;
using System.Linq;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services.Analysis;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    public class SignalGenerationService
    {
        private readonly AnalysisStateManager _stateManager;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly HistoricalIvService _historicalIvService;
        private readonly IndicatorService _indicatorService;

        public SignalGenerationService(AnalysisStateManager stateManager, SettingsViewModel settingsViewModel, HistoricalIvService historicalIvService, IndicatorService indicatorService)
        {
            _stateManager = stateManager;
            _settingsViewModel = settingsViewModel;
            _historicalIvService = historicalIvService;
            _indicatorService = indicatorService;
        }

        public void GenerateAllSignals(DashboardInstrument instrument, DashboardInstrument instrumentForAnalysis, AnalysisResult result, System.Collections.ObjectModel.ObservableCollection<OptionChainRow> optionChain)
        {
            // VWAP calculation
            var tickState = _stateManager.TickAnalysisState[instrumentForAnalysis.SecurityId];
            tickState.cumulativePriceVolume += instrumentForAnalysis.AvgTradePrice * instrumentForAnalysis.LastTradedQuantity;
            tickState.cumulativeVolume += instrumentForAnalysis.LastTradedQuantity;
            result.Vwap = (tickState.cumulativeVolume > 0) ? tickState.cumulativePriceVolume / tickState.cumulativeVolume : 0;
            _stateManager.TickAnalysisState[instrumentForAnalysis.SecurityId] = tickState;

            var (priceVsVwap, priceVsClose, dayRange) = CalculatePriceActionSignals(instrument, result.Vwap);
            result.PriceVsVwapSignal = priceVsVwap;
            result.PriceVsCloseSignal = priceVsClose;
            result.DayRangeSignal = dayRange;

            // Use 3-minute candles for OI analysis
            var threeMinCandles = _stateManager.GetCandles(instrumentForAnalysis.SecurityId, TimeSpan.FromMinutes(3));
            if (threeMinCandles != null && threeMinCandles.Any())
            {
                result.OiSignal = CalculateOiSignal(threeMinCandles);
            }

            var oneMinCandles = _stateManager.GetCandles(instrumentForAnalysis.SecurityId, TimeSpan.FromMinutes(1));
            if (oneMinCandles != null && oneMinCandles.Any())
            {
                var (volSignal, currentVol, avgVol) = CalculateVolumeSignal(oneMinCandles);
                result.VolumeSignal = volSignal;
                result.CurrentVolume = currentVol;
                result.AvgVolume = avgVol;
                result.OpenTypeSignal = AnalyzeOpenType(instrument, oneMinCandles);
                var (vwapBandSignal, upperBand, lowerBand) = CalculateVwapBandSignal(instrument.LTP, oneMinCandles);
                result.VwapBandSignal = vwapBandSignal;
                result.VwapUpperBand = upperBand;
                result.VwapLowerBand = lowerBand;
                result.AnchoredVwap = CalculateAnchoredVwap(oneMinCandles);
            }

            var fiveMinCandles = _stateManager.GetCandles(instrumentForAnalysis.SecurityId, TimeSpan.FromMinutes(5));
            var fifteenMinCandles = _stateManager.GetCandles(instrumentForAnalysis.SecurityId, TimeSpan.FromMinutes(15));

            if (oneMinCandles != null && oneMinCandles.Any())
            {
                result.EmaSignal1Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, oneMinCandles, _stateManager.MultiTimeframePriceEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, false);
                result.VwapEmaSignal1Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, oneMinCandles, _stateManager.MultiTimeframeVwapEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, true);
            }
            if (fiveMinCandles != null && fiveMinCandles.Any())
            {
                result.EmaSignal5Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, fiveMinCandles, _stateManager.MultiTimeframePriceEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, false);
                result.VwapEmaSignal5Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, fiveMinCandles, _stateManager.MultiTimeframeVwapEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, true);
            }
            if (fifteenMinCandles != null && fifteenMinCandles.Any())
            {
                result.EmaSignal15Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, fifteenMinCandles, _stateManager.MultiTimeframePriceEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, false);
                result.VwapEmaSignal15Min = _indicatorService.CalculateEmaSignal(instrumentForAnalysis.SecurityId, fifteenMinCandles, _stateManager.MultiTimeframeVwapEmaState, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength, true);
            }

            if (oneMinCandles != null && oneMinCandles.Any())
            {
                var rsiState = _stateManager.MultiTimeframeRsiState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(1)];
                result.RsiValue1Min = _indicatorService.CalculateRsi(oneMinCandles, rsiState, _settingsViewModel.RsiPeriod);
                result.RsiSignal1Min = _indicatorService.DetectRsiDivergence(oneMinCandles, rsiState, _settingsViewModel.RsiDivergenceLookback);
            }
            if (fiveMinCandles != null && fiveMinCandles.Any())
            {
                var rsiState = _stateManager.MultiTimeframeRsiState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(5)];
                result.RsiValue5Min = _indicatorService.CalculateRsi(fiveMinCandles, rsiState, _settingsViewModel.RsiPeriod);
                result.RsiSignal5Min = _indicatorService.DetectRsiDivergence(fiveMinCandles, rsiState, _settingsViewModel.RsiDivergenceLookback);
            }

            // --- FIX: Added ATR and OBV calculations ---
            if (oneMinCandles != null && oneMinCandles.Any())
            {
                var atrState = _stateManager.MultiTimeframeAtrState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(1)];
                result.Atr1Min = _indicatorService.CalculateAtr(oneMinCandles, atrState, _settingsViewModel.AtrPeriod);

                var obvState = _stateManager.MultiTimeframeObvState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(1)];
                result.ObvValue1Min = _indicatorService.CalculateObv(oneMinCandles, obvState);
            }
            if (fiveMinCandles != null && fiveMinCandles.Any())
            {
                var atrState = _stateManager.MultiTimeframeAtrState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(5)];
                result.Atr5Min = _indicatorService.CalculateAtr(fiveMinCandles, atrState, _settingsViewModel.AtrPeriod);
                result.AtrSignal5Min = (atrState.AtrValues.Count > 2 && result.Atr5Min < atrState.AtrValues[^2]) ? "Vol Contracting" : "Vol Expanding";

                var obvState = _stateManager.MultiTimeframeObvState[instrumentForAnalysis.SecurityId][TimeSpan.FromMinutes(5)];
                result.ObvValue5Min = _indicatorService.CalculateObv(fiveMinCandles, obvState);
            }


            if (oneMinCandles != null) result.CandleSignal1Min = RecognizeCandlestickPattern(oneMinCandles, result);
            if (fiveMinCandles != null)
            {
                result.CandleSignal5Min = RecognizeCandlestickPattern(fiveMinCandles, result);
                result.MarketRegime = CalculateMarketRegime(fiveMinCandles, instrumentForAnalysis.SecurityId);
            }

            if (_stateManager.MarketProfiles.TryGetValue(instrument.SecurityId, out var liveProfile))
            {
                result.InitialBalanceSignal = GetInitialBalanceSignal(instrument.LTP, liveProfile, instrument.SecurityId);
                result.InitialBalanceHigh = liveProfile.InitialBalanceHigh;
                result.InitialBalanceLow = liveProfile.InitialBalanceLow;
                result.DevelopingPoc = liveProfile.DevelopingTpoLevels.PointOfControl;
                result.DevelopingVah = liveProfile.DevelopingTpoLevels.ValueAreaHigh;
                result.DevelopingVal = liveProfile.DevelopingTpoLevels.ValueAreaLow;
                result.DevelopingVpoc = liveProfile.DevelopingVolumeProfile.VolumePoc;
                RunMarketProfileAnalysis(instrument, liveProfile, result);
            }
            var yesterdayProfile = _stateManager.HistoricalMarketProfiles.GetValueOrDefault(instrument.SecurityId)?.FirstOrDefault(p => p.Date.Date < DateTime.Today);
            result.YesterdayProfileSignal = AnalyzePriceRelativeToYesterdayProfile(instrument.LTP, yesterdayProfile);
            if (instrument.InstrumentType == "INDEX") { result.InstitutionalIntent = RunTier1InstitutionalIntentAnalysis(instrument); }
            result.VolatilityStateSignal = GenerateVolatilityStateSignal(instrumentForAnalysis, result);

            result.IntradayIvSpikeSignal = CalculateIntradayIvSpikeSignal(instrument);
            result.GammaSignal = CalculateGammaSignal(instrument, result.LTP, optionChain);
        }

        private string CalculateGammaSignal(DashboardInstrument instrument, decimal underlyingPrice, System.Collections.ObjectModel.ObservableCollection<OptionChainRow> optionChain)
        {
            // Only run this analysis for the main indices that have an option chain view
            if (instrument.InstrumentType != "INDEX" || optionChain == null || !optionChain.Any())
            {
                return "N/A";
            }

            // 1. Find the ATM strike and its index in the sorted list
            var sortedStrikes = optionChain.OrderBy(r => r.StrikePrice).ToList();
            var atmStrike = sortedStrikes.OrderBy(r => Math.Abs(r.StrikePrice - underlyingPrice)).FirstOrDefault();
            if (atmStrike == null) return "N/A";

            int atmIndex = sortedStrikes.IndexOf(atmStrike);

            // 2. Select the 4 OTM strikes for calls (higher strikes) and puts (lower strikes)
            const int otmCount = 4;
            var otmCallStrikes = sortedStrikes.Skip(atmIndex + 1).Take(otmCount).ToList();
            var otmPutStrikes = sortedStrikes.Take(atmIndex).Reverse().Take(otmCount).ToList();

            if (otmCallStrikes.Count < otmCount || otmPutStrikes.Count < otmCount)
            {
                return "Insufficient OTM Strikes";
            }

            // 3. Sum the gamma for these strikes
            decimal totalOtmCallGamma = otmCallStrikes.Sum(s => s.CallOption?.Gamma ?? 0);
            decimal totalOtmPutGamma = otmPutStrikes.Sum(s => s.PutOption?.Gamma ?? 0);

            // 4. Compare and generate the signal
            decimal difference = totalOtmCallGamma - totalOtmPutGamma;
            decimal totalGamma = totalOtmCallGamma + totalOtmPutGamma;
            decimal ratio = totalGamma > 0 ? Math.Abs(difference) / totalGamma : 0;

            if (ratio > 0.5m) // If one side has more than 75% of the total gamma ( (X-Y)/(X+Y) > 0.5 => X > 3Y )
            {
                if (totalOtmCallGamma > totalOtmPutGamma)
                {
                    return "High OTM Call Gamma"; // Potential for upward gamma squeeze
                }
                else
                {
                    return "High OTM Put Gamma"; // Potential for downward gamma squeeze
                }
            }

            if (totalOtmCallGamma > _settingsViewModel.AtmGammaThreshold && totalOtmPutGamma > _settingsViewModel.AtmGammaThreshold)
            {
                return "Balanced OTM Gamma"; // Both sides have significant gamma
            }

            return "Neutral";
        }

        public void UpdateIvMetrics(DashboardInstrument instrument, decimal underlyingPrice)
        {
            if (!instrument.InstrumentType.StartsWith("OPT") || instrument.ImpliedVolatility <= 0) return;
            var ivKey = GetHistoricalIvKey(instrument, underlyingPrice);
            if (string.IsNullOrEmpty(ivKey)) return;
            if (!_stateManager.IntradayIvStates.ContainsKey(ivKey)) { _stateManager.IntradayIvStates[ivKey] = new IntradayIvState(); }
            var ivState = _stateManager.IntradayIvStates[ivKey];
            ivState.DayHighIv = Math.Max(ivState.DayHighIv, instrument.ImpliedVolatility);
            ivState.DayLowIv = Math.Min(ivState.DayLowIv, instrument.ImpliedVolatility);

            ivState.IvHistory.Add(instrument.ImpliedVolatility);
            if (ivState.IvHistory.Count > 15)
            {
                ivState.IvHistory.RemoveAt(0);
            }

            _historicalIvService.RecordDailyIv(ivKey, ivState.DayHighIv, ivState.DayLowIv);
            var (ivRank, ivPercentile) = CalculateIvRankAndPercentile(instrument.ImpliedVolatility, ivKey, ivState);

            var underlyingInstrument = _stateManager.AnalysisResults.Values.FirstOrDefault(r => r.Symbol == instrument.UnderlyingSymbol);
            if (underlyingInstrument != null)
            {
                var result = _stateManager.GetResult(underlyingInstrument.SecurityId);
                result.IvRank = ivRank;
                result.IvPercentile = ivPercentile;
            }
        }

        #region Signal Calculation Logic

        private string CalculateOiSignal(List<Candle> candles)
        {
            if (candles.Count < 2) return "Building History...";
            var currentCandle = candles.Last();
            var previousCandle = candles[candles.Count - 2];
            if (previousCandle.OpenInterest == 0 || currentCandle.OpenInterest == 0) return "Building History...";

            bool isPriceUp = currentCandle.Close > previousCandle.Close;
            bool isPriceDown = currentCandle.Close < previousCandle.Close;
            bool isOiUp = currentCandle.OpenInterest > previousCandle.OpenInterest;
            bool isOiDown = currentCandle.OpenInterest < previousCandle.OpenInterest;

            if (isPriceUp && isOiUp) return "Long Buildup";
            if (isPriceUp && isOiDown) return "Short Covering";
            if (isPriceDown && isOiUp) return "Short Buildup";
            if (isPriceDown && isOiDown) return "Long Unwinding";
            return "Neutral";
        }

        private string CalculateMarketRegime(List<Candle> fiveMinCandles, string securityId)
        {
            var atrState = _stateManager.MultiTimeframeAtrState[securityId][TimeSpan.FromMinutes(5)];
            if (atrState.AtrValues.Count < 10) return "Calculating...";

            decimal currentAtr = atrState.CurrentAtr;
            decimal avgAtr = atrState.AtrValues.TakeLast(10).Average();

            if (currentAtr > avgAtr * 1.5m) return "High Volatility";
            if (currentAtr < avgAtr * 0.7m) return "Low Volatility";
            return "Normal Volatility";
        }

        private string CalculateIntradayIvSpikeSignal(DashboardInstrument instrument)
        {
            var ivKey = GetHistoricalIvKey(instrument, 0);
            if (!_stateManager.IntradayIvStates.TryGetValue(ivKey, out var ivState) || ivState.IvHistory.Count < 10)
            {
                return "N/A";
            }

            decimal currentIv = instrument.ImpliedVolatility;
            decimal avgIv = ivState.IvHistory.Average();
            if (avgIv == 0) return "N/A";
            decimal ivChange = (currentIv - avgIv) / avgIv;

            if (ivChange > _settingsViewModel.IvSpikeThreshold) return "IV Spike Up";
            if (ivChange < -_settingsViewModel.IvSpikeThreshold) return "IV Spike Down";
            return "IV Stable";
        }

        public void UpdateMarketProfile(MarketProfile profile, Candle priceCandle, Candle volumeCandle)
        {
            profile.UpdateInitialBalance(priceCandle);
            var tpoPeriod = profile.GetTpoPeriod(priceCandle.Timestamp);

            var priceRange = new List<decimal>();
            for (decimal price = priceCandle.Low; price <= priceCandle.High; price += profile.TickSize)
            {
                priceRange.Add(profile.QuantizePrice(price));
            }

            if (priceRange.Any())
            {
                long volumePerTick = priceRange.Count > 0 ? volumeCandle.Volume / priceRange.Count : 0;
                foreach (var price in priceRange)
                {
                    if (!profile.TpoLevels.ContainsKey(price)) profile.TpoLevels[price] = new List<char>();
                    if (!profile.TpoLevels[price].Contains(tpoPeriod)) profile.TpoLevels[price].Add(tpoPeriod);

                    if (!profile.VolumeLevels.ContainsKey(price)) profile.VolumeLevels[price] = 0;
                    profile.VolumeLevels[price] += volumePerTick;
                }
            }
            profile.CalculateProfileMetrics();
        }

        private (string, long, long) CalculateVolumeSignal(List<Candle> candles) { if (!candles.Any()) return ("N/A", 0, 0); long currentCandleVolume = candles.Last().Volume; if (candles.Count < 2) return ("Building History...", currentCandleVolume, 0); var historyCandles = candles.Take(candles.Count - 1).ToList(); if (historyCandles.Count > _settingsViewModel.VolumeHistoryLength) { historyCandles = historyCandles.Skip(historyCandles.Count - _settingsViewModel.VolumeHistoryLength).ToList(); } if (!historyCandles.Any()) return ("Building History...", currentCandleVolume, 0); double averageVolume = historyCandles.Average(c => (double)c.Volume); if (averageVolume > 0 && currentCandleVolume > (averageVolume * _settingsViewModel.VolumeBurstMultiplier)) { return ("Volume Burst", currentCandleVolume, (long)averageVolume); } return ("Neutral", currentCandleVolume, (long)averageVolume); }
        private (string priceVsVwap, string priceVsClose, string dayRange) CalculatePriceActionSignals(DashboardInstrument instrument, decimal vwap) { string priceVsVwap = (vwap > 0) ? (instrument.LTP > vwap ? "Above VWAP" : "Below VWAP") : "Neutral"; string priceVsClose = (instrument.Close > 0) ? (instrument.LTP > instrument.Close ? "Above Close" : "Below Close") : "Neutral"; string dayRange = "Mid-Range"; decimal range = instrument.High - instrument.Low; if (range > 0) { decimal position = (instrument.LTP - instrument.Low) / range; if (position > 0.8m) dayRange = "Near High"; else if (position < 0.2m) dayRange = "Near Low"; } return (priceVsVwap, priceVsClose, dayRange); }
        private string RecognizeCandlestickPattern(List<Candle> candles, AnalysisResult analysisResult) { if (candles.Count < 3) return "N/A"; string pattern = IdentifyCandlePattern(candles); if (pattern == "N/A") return "N/A"; string context = GetPatternContext(analysisResult); string volumeInfo = GetVolumeConfirmation(candles.Last(), candles[^2]); return $"{pattern}{context}{volumeInfo}"; }
        private string IdentifyCandlePattern(List<Candle> candles) { var c1 = candles.Last(); var c2 = candles[^2]; var c3 = candles[^3]; decimal body1 = Math.Abs(c1.Open - c1.Close); decimal range1 = c1.High - c1.Low; if (range1 == 0) return "N/A"; decimal upperShadow1 = c1.High - Math.Max(c1.Open, c1.Close); decimal lowerShadow1 = Math.Min(c1.Open, c1.Close) - c1.Low; if (body1 / range1 < 0.15m) return "Neutral Doji"; if (lowerShadow1 > body1 * 1.8m && upperShadow1 < body1 * 0.9m) return c1.Close > c1.Open ? "Bullish Hammer" : "Bearish Hanging Man"; if (upperShadow1 > body1 * 1.8m && lowerShadow1 < body1 * 0.9m) return c1.Close > c1.Open ? "Bullish Inv Hammer" : "Bearish Shooting Star"; if (body1 / range1 > 0.85m) return c1.Close > c1.Open ? "Bullish Marubozu" : "Bearish Marubozu"; if (c1.Close > c2.Open && c1.Open < c2.Close && c1.Close > c1.Open && c2.Close < c2.Open) return "Bullish Engulfing"; if (c1.Open > c2.Close && c1.Close < c2.Open && c1.Close < c1.Open && c2.Close > c2.Open) return "Bearish Engulfing"; decimal c2BodyMidpoint = c2.Open + (c2.Close - c2.Open) / 2; if (c2.Close < c2.Open && c1.Open < c2.Low && c1.Close > c2BodyMidpoint && c1.Close < c2.Open) return "Bullish Piercing Line"; if (c2.Close > c2.Open && c1.Open > c2.High && c1.Close < c2BodyMidpoint && c1.Close > c2.Open) return "Bearish Dark Cloud Cover"; bool isMorningStar = c3.Close < c3.Open && Math.Max(c2.Open, c2.Close) < c3.Close && c1.Close > c1.Open && c1.Close > (c3.Open + c3.Close) / 2; if (isMorningStar) return "Bullish Morning Star"; bool isEveningStar = c3.Close > c3.Open && Math.Min(c2.Open, c2.Close) > c3.Close && c1.Close < c1.Open && c1.Close < (c3.Open + c3.Close) / 2; if (isEveningStar) return "Bearish Evening Star"; bool isThreeWhiteSoldiers = c3.Close > c3.Open && c2.Close > c2.Open && c1.Close > c1.Open && c2.Open > c3.Open && c2.Close > c3.Close && c1.Open > c2.Open && c1.Close > c2.Close; if (isThreeWhiteSoldiers) return "Three White Soldiers"; bool isThreeBlackCrows = c3.Close < c3.Open && c2.Close < c2.Open && c1.Close < c1.Open && c2.Open < c3.Open && c2.Close < c3.Close && c1.Open < c2.Open && c1.Close < c2.Close; if (isThreeBlackCrows) return "Three Black Crows"; return "N/A"; }
        private string GetPatternContext(AnalysisResult analysisResult) { if (analysisResult.DayRangeSignal == "Near Low" || analysisResult.VwapBandSignal == "At Lower Band" || analysisResult.MarketProfileSignal.Contains("VAL")) { return " at Key Support"; } if (analysisResult.DayRangeSignal == "Near High" || analysisResult.VwapBandSignal == "At Upper Band" || analysisResult.MarketProfileSignal.Contains("VAH")) { return " at Key Resistance"; } return string.Empty; }
        private string GetVolumeConfirmation(Candle current, Candle previous) { if (previous.Volume > 0) { decimal volChange = ((decimal)current.Volume - previous.Volume) / previous.Volume; if (volChange > 0.5m) { return " (+Vol)"; } } return ""; }
        private string AnalyzeOpenType(DashboardInstrument instrument, List<Candle> oneMinCandles) { if (oneMinCandles.Count < 3) return "Analyzing Open..."; var firstCandle = oneMinCandles[0]; bool isFirstCandleStrong = Math.Abs(firstCandle.Close - firstCandle.Open) > (firstCandle.High - firstCandle.Low) * 0.7m; if (isFirstCandleStrong && firstCandle.Close > firstCandle.Open) return "Open-Drive (Bullish)"; if (isFirstCandleStrong && firstCandle.Close < firstCandle.Open) return "Open-Drive (Bearish)"; return "Open-Auction (Rotational)"; }
        private (string, decimal, decimal) CalculateVwapBandSignal(decimal ltp, List<Candle> candles) { if (candles.Count < 2) return ("N/A", 0, 0); var vwap = candles.Last().Vwap; if (vwap == 0) return ("N/A", 0, 0); decimal sumOfSquares = candles.Sum(c => (c.Close - vwap) * (c.Close - vwap)); decimal stdDev = (decimal)Math.Sqrt((double)(sumOfSquares / candles.Count)); var upperBand = vwap + (stdDev * _settingsViewModel.VwapUpperBandMultiplier); var lowerBand = vwap - (stdDev * _settingsViewModel.VwapLowerBandMultiplier); string signal = "Inside Bands"; if (ltp > upperBand) signal = "Above Upper Band"; else if (ltp < lowerBand) signal = "Below Lower Band"; return (signal, upperBand, lowerBand); }
        private decimal CalculateAnchoredVwap(List<Candle> candles) { if (candles == null || !candles.Any()) return 0; decimal cumulativePriceVolume = candles.Sum(c => c.Close * c.Volume); long cumulativeVolume = candles.Sum(c => c.Volume); return (cumulativeVolume > 0) ? cumulativePriceVolume / cumulativeVolume : 0; }
        private string GetInitialBalanceSignal(decimal ltp, MarketProfile profile, string securityId) { if (!profile.IsInitialBalanceSet) return "IB Forming"; if (!_stateManager.InitialBalanceState.ContainsKey(securityId)) _stateManager.InitialBalanceState[securityId] = (false, false); var (isBreakout, isBreakdown) = _stateManager.InitialBalanceState[securityId]; if (ltp > profile.InitialBalanceHigh && !isBreakout) { _stateManager.InitialBalanceState[securityId] = (true, false); return "IB Breakout"; } if (ltp < profile.InitialBalanceLow && !isBreakdown) { _stateManager.InitialBalanceState[securityId] = (false, true); return "IB Breakdown"; } if (ltp > profile.InitialBalanceHigh && isBreakout) return "IB Extension Up"; if (ltp < profile.InitialBalanceLow && isBreakdown) return "IB Extension Down"; return "Inside IB"; }
        private string AnalyzePriceRelativeToYesterdayProfile(decimal ltp, MarketProfileData? previousDay) { if (previousDay == null || ltp == 0) return "N/A"; if (ltp > previousDay.TpoLevelsInfo.ValueAreaHigh) return "Trading Above Y-VAH"; if (ltp < previousDay.TpoLevelsInfo.ValueAreaLow) return "Trading Below Y-VAL"; return "Trading Inside Y-Value"; }
        private void RunMarketProfileAnalysis(DashboardInstrument instrument, MarketProfile currentProfile, AnalysisResult result) { var previousDayProfile = _stateManager.HistoricalMarketProfiles.GetValueOrDefault(instrument.SecurityId)?.FirstOrDefault(p => p.Date.Date < DateTime.Today.Date); if (previousDayProfile == null) { result.MarketProfileSignal = "Awaiting Previous Day Data"; return; } var prevVAH = previousDayProfile.TpoLevelsInfo.ValueAreaHigh; var currentVAL = currentProfile.DevelopingTpoLevels.ValueAreaLow; if (currentVAL > prevVAH) { result.MarketProfileSignal = "True Acceptance Above Y-VAH"; return; } result.MarketProfileSignal = "Trading Inside Y-Value"; }
        private string RunTier1InstitutionalIntentAnalysis(DashboardInstrument spotIndex) { return "Neutral"; }
        public void RunDailyBiasAnalysis(DashboardInstrument instrument, AnalysisResult result) { var profiles = _stateManager.HistoricalMarketProfiles.GetValueOrDefault(instrument.SecurityId); if (profiles == null || profiles.Count < 3) { result.DailyBias = "Insufficient History"; result.MarketStructure = "Unknown"; return; } var sortedProfiles = profiles.OrderByDescending(p => p.Date).ToList(); var p1 = sortedProfiles[0]; var p2 = sortedProfiles[1]; var p3 = sortedProfiles[2]; bool isP1Higher = p1.TpoLevelsInfo.ValueAreaLow > p2.TpoLevelsInfo.ValueAreaHigh; bool isP2Higher = p2.TpoLevelsInfo.ValueAreaLow > p3.TpoLevelsInfo.ValueAreaHigh; bool isP1OverlapHigher = p1.TpoLevelsInfo.PointOfControl > p2.TpoLevelsInfo.ValueAreaHigh; bool isP2OverlapHigher = p2.TpoLevelsInfo.PointOfControl > p3.TpoLevelsInfo.ValueAreaHigh; if ((isP1Higher && isP2Higher) || (isP1OverlapHigher && isP2OverlapHigher)) { result.MarketStructure = "Trending Up"; result.DailyBias = "Bullish"; return; } bool isP1Lower = p1.TpoLevelsInfo.ValueAreaHigh < p2.TpoLevelsInfo.ValueAreaLow; bool isP2Lower = p2.TpoLevelsInfo.ValueAreaHigh < p3.TpoLevelsInfo.ValueAreaLow; bool isP1OverlapLower = p1.TpoLevelsInfo.PointOfControl < p2.TpoLevelsInfo.ValueAreaLow; bool isP2OverlapLower = p2.TpoLevelsInfo.PointOfControl < p3.TpoLevelsInfo.ValueAreaLow; if ((isP1Lower && isP2Lower) || (isP1OverlapLower && isP2OverlapLower)) { result.MarketStructure = "Trending Down"; result.DailyBias = "Bearish"; return; } result.MarketStructure = "Balancing"; result.DailyBias = "Neutral / Rotational"; }
        public decimal GetTickSize(DashboardInstrument? instrument) => (instrument?.InstrumentType == "INDEX") ? 1.0m : 0.05m;
        private string GetHistoricalIvKey(DashboardInstrument instrument, decimal underlyingPrice) { return $"{instrument.UnderlyingSymbol}_ATM_CE"; }
        private (decimal ivRank, decimal ivPercentile) CalculateIvRankAndPercentile(decimal currentIv, string key, IntradayIvState ivState) { var (histHigh, histLow) = _historicalIvService.Get90DayIvRange(key); if (histHigh == 0 || histLow == 0) return (0m, 0m); decimal histRange = histHigh - histLow; decimal ivRank = (histRange > 0) ? ((currentIv - histLow) / histRange) * 100 : 0m; return (Math.Max(0, Math.Min(100, Math.Round(ivRank, 2))), 0m); }
        private string GenerateVolatilityStateSignal(DashboardInstrument instrument, AnalysisResult result) { bool isAtrContracting = result.AtrSignal5Min == "Vol Contracting"; bool isIvRankLow = result.IvRank < 30; if (isAtrContracting && isIvRankLow) { _stateManager.IsInVolatilitySqueeze[instrument.SecurityId] = true; return "IV Squeeze Setup"; } return "Normal Volatility"; }

        #endregion
    }
}