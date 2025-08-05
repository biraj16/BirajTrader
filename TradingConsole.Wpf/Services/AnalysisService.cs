// TradingConsole.Wpf/Services/AnalysisService.cs
// --- MODIFIED: Correctly passed the IndicatorService dependency ---
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi;
using TradingConsole.DhanApi.Models;
using TradingConsole.Wpf.Services.Analysis;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    public enum MarketThesis { Bullish_Trend, Bullish_Rotation, Bullish_Reversal_Attempt, Bearish_Trend, Bearish_Rotation, Bearish_Reversal_Attempt, Balancing, Indeterminate, Choppy }
    public enum DominantPlayer { Buyers, Sellers, Balance, Indeterminate }

    public class AnalysisService : INotifyPropertyChanged
    {
        #region Services and Parameters
        private readonly SettingsViewModel _settingsViewModel;
        private readonly DhanApiClient _apiClient;
        private readonly ScripMasterService _scripMasterService;
        private readonly MarketProfileService _marketProfileService;
        private readonly IndicatorStateService _indicatorStateService;
        private readonly AnalysisStateManager _stateManager;
        private readonly IndicatorService _indicatorService;
        private readonly SignalGenerationService _signalGenerationService;
        private readonly ThesisSynthesizer _thesisSynthesizer;
        private readonly Dictionary<string, DashboardInstrument> _instrumentCache = new();
        private readonly Dictionary<string, DateTime> _nearestExpiryDates = new();
        private readonly MainViewModel _mainViewModel;
        public event Action<AnalysisResult>? OnAnalysisUpdated;
        public event Action<string, Candle, TimeSpan>? CandleUpdated;
        #endregion

        public AnalysisService(SettingsViewModel settingsViewModel, DhanApiClient apiClient, ScripMasterService scripMasterService, HistoricalIvService historicalIvService, MarketProfileService marketProfileService, IndicatorStateService indicatorStateService, SignalLoggerService signalLoggerService, NotificationService notificationService, DashboardViewModel dashboardViewModel, MainViewModel mainViewModel)
        {
            _settingsViewModel = settingsViewModel;
            _apiClient = apiClient;
            _scripMasterService = scripMasterService;
            _marketProfileService = marketProfileService;
            _indicatorStateService = indicatorStateService;
            _stateManager = new AnalysisStateManager();
            _indicatorService = new IndicatorService(_stateManager);
            _signalGenerationService = new SignalGenerationService(_stateManager, settingsViewModel, historicalIvService, _indicatorService);
            _thesisSynthesizer = new ThesisSynthesizer(settingsViewModel, signalLoggerService, notificationService, _stateManager);
            _mainViewModel = mainViewModel;
        }

        private void UpdateMarketPhase()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var time = istNow.TimeOfDay;

            if (time < new TimeSpan(9, 15, 0)) _stateManager.CurrentMarketPhase = MarketPhase.PreOpen;
            else if (time < new TimeSpan(9, 45, 0)) _stateManager.CurrentMarketPhase = MarketPhase.Opening;
            else if (time > new TimeSpan(15, 0, 0)) _stateManager.CurrentMarketPhase = MarketPhase.Closing;
            else _stateManager.CurrentMarketPhase = MarketPhase.Normal;
        }

        public void OnInstrumentDataReceived(DashboardInstrument instrument, decimal underlyingPrice)
        {
            if (!IsMarketOpen()) return;

            var lastTradeDateTime = DateTimeOffset.FromUnixTimeSeconds(instrument.LastTradeTime).UtcDateTime;
            if ((DateTime.UtcNow - lastTradeDateTime).TotalSeconds > 15)
            {
                Debug.WriteLine($"[AnalysisService] Stale data received for {instrument.DisplayName}. Skipping analysis.");
                return;
            }

            UpdateMarketPhase();

            if (string.IsNullOrEmpty(instrument.SecurityId)) return;
            _instrumentCache[instrument.SecurityId] = instrument;

            if (!_stateManager.BackfilledInstruments.Contains(instrument.SecurityId))
            {
                InitializeNewInstrument(instrument);
            }

            _signalGenerationService.UpdateIvMetrics(instrument, underlyingPrice);

            bool newCandleFormed = false;
            var timeframes = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) };
            foreach (var timeframe in timeframes)
            {
                if (AggregateIntoCandle(instrument, timeframe))
                {
                    newCandleFormed = true;
                }
            }

            RunComplexAnalysis(instrument, newCandleFormed);
        }

        private bool AggregateIntoCandle(DashboardInstrument instrument, TimeSpan timeframe)
        {
            var candles = _stateManager.GetCandles(instrument.SecurityId, timeframe);
            if (candles == null) return false;
            var now = DateTime.UtcNow;
            var candleTimestamp = new DateTime(now.Ticks - (now.Ticks % timeframe.Ticks), now.Kind);
            var currentCandle = candles.LastOrDefault();

            if (currentCandle == null || currentCandle.Timestamp != candleTimestamp)
            {
                var newCandle = new Candle { Timestamp = candleTimestamp, Open = instrument.LTP, High = instrument.LTP, Low = instrument.LTP, Close = instrument.LTP, Volume = instrument.LastTradedQuantity, OpenInterest = (long)instrument.OpenInterest, Vwap = instrument.AvgTradePrice };
                candles.Add(newCandle);

                if (currentCandle != null)
                {
                    if (timeframe.TotalMinutes == 1) UpdateMarketProfileForCandle(instrument, currentCandle);
                }
                CandleUpdated?.Invoke(instrument.SecurityId, newCandle, timeframe);
                return true;
            }
            else
            {
                currentCandle.High = Math.Max(currentCandle.High, instrument.LTP);
                currentCandle.Low = Math.Min(currentCandle.Low, instrument.LTP);
                currentCandle.Close = instrument.LTP;
                currentCandle.Volume += instrument.LastTradedQuantity;
                currentCandle.OpenInterest = (long)instrument.OpenInterest;
                CandleUpdated?.Invoke(instrument.SecurityId, currentCandle, timeframe);
                return false;
            }
        }

        private void RunComplexAnalysis(DashboardInstrument instrument, bool newCandleFormed)
        {
            var result = _stateManager.GetResult(instrument.SecurityId);

            result.LTP = instrument.LTP;
            result.PriceChange = instrument.LTP - instrument.Close;
            result.PriceChangePercent = (instrument.Close > 0) ? (result.PriceChange / instrument.Close) : 0;

            DashboardInstrument instrumentForAnalysis = GetInstrumentForVolumeAnalysis(instrument);

            _signalGenerationService.GenerateAllSignals(instrument, instrumentForAnalysis, result, _mainViewModel.OptionChainRows);

            if (newCandleFormed)
            {
                _thesisSynthesizer.SynthesizeTradeSignal(result);
            }

            LinkFuturesDataToIndex();
            OnAnalysisUpdated?.Invoke(result);
        }

        #region Boilerplate and other methods
        private bool IsMarketOpen() { try { var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone); if (istNow.DayOfWeek == DayOfWeek.Saturday || istNow.DayOfWeek == DayOfWeek.Sunday) return false; if (_settingsViewModel.MarketHolidays.Contains(istNow.Date)) return false; var marketOpen = new TimeSpan(9, 15, 0); var marketClose = new TimeSpan(15, 30, 0); if (istNow.TimeOfDay < marketOpen || istNow.TimeOfDay > marketClose) return false; return true; } catch (TimeZoneNotFoundException) { Debug.WriteLine("WARNING: India Standard Time zone not found."); var now = DateTime.UtcNow; if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday) return false; return true; } }
        private void InitializeNewInstrument(DashboardInstrument instrument) { _stateManager.InitializeStateForInstrument(instrument.SecurityId, instrument.DisplayName, instrument.InstrumentType); _stateManager.HistoricalMarketProfiles[instrument.SecurityId] = _marketProfileService.GetHistoricalProfiles(instrument.SecurityId); if (!_stateManager.MarketProfiles.ContainsKey(instrument.SecurityId)) { decimal tickSize = _signalGenerationService.GetTickSize(instrument); var startTime = DateTime.Today.Add(new TimeSpan(9, 15, 0)); _stateManager.MarketProfiles[instrument.SecurityId] = new MarketProfile(tickSize, startTime); } LoadIndicatorStateFromStorage(instrument.SecurityId); Task.Run(() => BackfillAndSavePreviousDayProfileAsync(instrument)); Task.Run(() => BackfillCurrentDayCandlesAsync(instrument)); RunDailyBiasAnalysis(instrument); }
        public List<Candle>? GetCandles(string securityId, TimeSpan timeframe) => _stateManager.GetCandles(securityId, timeframe);
        public void SetNearestExpiryDates(Dictionary<string, string> expiryDates) { foreach (var kvp in expiryDates) { if (DateTime.TryParse(kvp.Value, out var date)) { _nearestExpiryDates[kvp.Key] = date.Date; } } }
        public void SaveIndicatorStates() { if (!IsMarketOpen()) return; var timeframes = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) }; foreach (var securityId in _stateManager.MultiTimeframePriceEmaState.Keys) { foreach (var timeframe in timeframes) { var key = $"{securityId}_{timeframe.TotalMinutes}"; var rsiState = _stateManager.MultiTimeframeRsiState[securityId][timeframe]; var atrState = _stateManager.MultiTimeframeAtrState[securityId][timeframe]; var obvState = _stateManager.MultiTimeframeObvState[securityId][timeframe]; var stateToSave = new IndicatorState { LastRsiAvgGain = rsiState.AvgGain, LastRsiAvgLoss = rsiState.AvgLoss, LastAtr = atrState.CurrentAtr, LastObv = obvState.CurrentObv, LastObvMovingAverage = obvState.CurrentMovingAverage }; _indicatorStateService.UpdateState(key, stateToSave); } } _indicatorStateService.SaveDatabase(); }
        public void SaveMarketProfileDatabase() { if (!IsMarketOpen()) return; _marketProfileService.SaveDatabase(); }
        private void RunDailyBiasAnalysis(DashboardInstrument instrument) { var result = _stateManager.GetResult(instrument.SecurityId); _signalGenerationService.RunDailyBiasAnalysis(instrument, result); OnAnalysisUpdated?.Invoke(result); }
        private async Task BackfillCurrentDayCandlesAsync(DashboardInstrument instrument) { if (!IsMarketOpen()) return; var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")); if (istNow.TimeOfDay < new TimeSpan(9, 15, 0)) return; try { var priceScripInfo = _scripMasterService.FindBySecurityId(instrument.SecurityId); if (priceScripInfo == null) return; var historicalData = await _apiClient.GetIntradayHistoricalDataAsync(priceScripInfo, "1", istNow.Date); if (historicalData?.Open == null || !historicalData.Open.Any()) return; var candles = new List<Candle>(); for (int i = 0; i < historicalData.Open.Count; i++) { var candle = new Candle { Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)historicalData.StartTime[i]).UtcDateTime, Open = historicalData.Open[i], High = historicalData.High[i], Low = historicalData.Low[i], Close = historicalData.Close[i], Volume = (long)historicalData.Volume[i], OpenInterest = (long)historicalData.OpenInterest[i] }; candles.Add(candle); } var timeframes = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) }; foreach (var timeframe in timeframes) { _stateManager.MultiTimeframeCandles[instrument.SecurityId][timeframe] = AggregateHistoricalCandles(candles, timeframe); _indicatorService.WarmupIndicators(instrument.SecurityId, timeframe, _settingsViewModel.ShortEmaLength, _settingsViewModel.LongEmaLength); } } catch (Exception ex) { Debug.WriteLine($"[BackfillCurrentDay] ERROR: {ex.Message}"); } }
        private async Task BackfillAndSavePreviousDayProfileAsync(DashboardInstrument instrument) { if (instrument.InstrumentType != "INDEX" && instrument.InstrumentType != "FUTIDX") return; var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")); DateTime dateToFetch = GetPreviousTradingDay(istNow); if (_stateManager.HistoricalMarketProfiles.GetValueOrDefault(instrument.SecurityId)?.Any(p => p.Date.Date == dateToFetch.Date) == true) return; try { var priceScripInfo = _scripMasterService.FindBySecurityId(instrument.SecurityId); if (priceScripInfo == null) return; var historicalData = await _apiClient.GetIntradayHistoricalDataAsync(priceScripInfo, "1", dateToFetch); if (historicalData?.Open == null || !historicalData.Open.Any()) return; decimal tickSize = _signalGenerationService.GetTickSize(instrument); var sessionStartTime = dateToFetch.Date.Add(new TimeSpan(9, 15, 0)); var historicalProfile = new MarketProfile(tickSize, sessionStartTime); for (int i = 0; i < historicalData.Open.Count; i++) { var priceCandle = new Candle { Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)historicalData.StartTime[i]).UtcDateTime, Open = historicalData.Open[i], High = historicalData.High[i], Low = historicalData.Low[i], Close = historicalData.Close[i] }; var volumeCandle = new Candle { Volume = (long)historicalData.Volume[i] }; _signalGenerationService.UpdateMarketProfile(historicalProfile, priceCandle, volumeCandle); } var profileDataToSave = historicalProfile.ToMarketProfileData(); _marketProfileService.UpdateProfile(instrument.SecurityId, profileDataToSave); if (!_stateManager.HistoricalMarketProfiles.ContainsKey(instrument.SecurityId)) { _stateManager.HistoricalMarketProfiles[instrument.SecurityId] = new List<MarketProfileData>(); } _stateManager.HistoricalMarketProfiles[instrument.SecurityId].Add(profileDataToSave); } catch (Exception ex) { Debug.WriteLine($"[BackfillPrevDay] UNEXPECTED ERROR: {ex.Message}"); } }
        private DateTime GetPreviousTradingDay(DateTime currentDate) { var date = currentDate.Date.AddDays(-1); while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday || _settingsViewModel.MarketHolidays.Contains(date.Date)) { date = date.AddDays(-1); } return date; }
        private List<Candle> AggregateHistoricalCandles(List<Candle> minuteCandles, TimeSpan timeframe) { return minuteCandles.GroupBy(c => new DateTime(c.Timestamp.Ticks - (c.Timestamp.Ticks % timeframe.Ticks), DateTimeKind.Utc)).Select(g => new Candle { Timestamp = g.Key, Open = g.First().Open, High = g.Max(c => c.High), Low = g.Min(c => c.Low), Close = g.Last().Close, Volume = g.Sum(c => c.Volume), OpenInterest = g.Last().OpenInterest }).ToList(); }
        private void LoadIndicatorStateFromStorage(string securityId) { var timeframes = new[] { TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) }; foreach (var tf in timeframes) { var key = $"{securityId}_{tf.TotalMinutes}"; var savedState = _indicatorStateService.GetState(key); if (savedState != null) { _stateManager.MultiTimeframeRsiState[securityId][tf].AvgGain = savedState.LastRsiAvgGain; _stateManager.MultiTimeframeRsiState[securityId][tf].AvgLoss = savedState.LastRsiAvgLoss; _stateManager.MultiTimeframeAtrState[securityId][tf].CurrentAtr = savedState.LastAtr; _stateManager.MultiTimeframeObvState[securityId][tf].CurrentObv = savedState.LastObv; _stateManager.MultiTimeframeObvState[securityId][tf].CurrentMovingAverage = savedState.LastObvMovingAverage; } } }
        private DashboardInstrument GetInstrumentForVolumeAnalysis(DashboardInstrument instrument) { if (instrument.InstrumentType == "INDEX") { var future = _instrumentCache.Values.FirstOrDefault(i => i.IsFuture && i.UnderlyingSymbol == instrument.Symbol); if (future != null) return future; } return instrument; }
        private void UpdateMarketProfileForCandle(DashboardInstrument instrument, Candle lastClosedCandle) { if (instrument.InstrumentType == "FUTIDX") { var underlyingIndex = _instrumentCache.Values.FirstOrDefault(i => i.InstrumentType == "INDEX" && i.UnderlyingSymbol == instrument.UnderlyingSymbol); if (underlyingIndex != null) { var indexCandles = _stateManager.GetCandles(underlyingIndex.SecurityId, TimeSpan.FromMinutes(1)); var matchingIndexCandle = indexCandles?.FirstOrDefault(c => c.Timestamp == lastClosedCandle.Timestamp); if (matchingIndexCandle != null && _stateManager.MarketProfiles.TryGetValue(underlyingIndex.SecurityId, out var profile)) { _signalGenerationService.UpdateMarketProfile(profile, matchingIndexCandle, lastClosedCandle); } } } else if (instrument.InstrumentType != "INDEX") { if (_stateManager.MarketProfiles.TryGetValue(instrument.SecurityId, out var profile)) { _signalGenerationService.UpdateMarketProfile(profile, lastClosedCandle, lastClosedCandle); } } }
        private void LinkFuturesDataToIndex() { var niftyIndex = _instrumentCache.Values.FirstOrDefault(i => i.Symbol == "Nifty 50"); var niftyFuture = _instrumentCache.Values.FirstOrDefault(i => i.IsFuture && i.UnderlyingSymbol == "NIFTY"); if (niftyIndex != null && niftyFuture != null && _stateManager.AnalysisResults.TryGetValue(niftyIndex.SecurityId, out var indexResult) && _stateManager.AnalysisResults.TryGetValue(niftyFuture.SecurityId, out var futureResult)) { indexResult.PriceVsVwapSignal = futureResult.PriceVsVwapSignal; indexResult.VwapBandSignal = futureResult.VwapBandSignal; indexResult.Vwap = futureResult.Vwap; } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        #endregion
    }
}
