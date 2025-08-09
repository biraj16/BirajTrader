// TradingConsole.Wpf/ViewModels/AnalysisResult.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    public class AnalysisResult : ObservableModel
    {
        private readonly Dictionary<string, DateTime> _signalLastChanged = new Dictionary<string, DateTime>();

        private void SetSignalProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return;

            backingStore = value;
            _signalLastChanged[propertyName] = DateTime.UtcNow;
            OnPropertyChanged(propertyName);
            OnPropertyChanged(propertyName + "Stability");
        }

        private string GetStabilityText(string propertyName)
        {
            if (!_signalLastChanged.TryGetValue(propertyName, out var lastChanged))
            {
                return "(Initial)";
            }

            var elapsed = DateTime.UtcNow - lastChanged;

            if (elapsed.TotalSeconds < 60)
            {
                return "(Just Flipped)";
            }
            if (elapsed.TotalMinutes < 60)
            {
                return $"(Stable {(int)elapsed.TotalMinutes}m)";
            }
            return $"(Stable {(int)elapsed.TotalHours}h)";
        }

        public void Update(AnalysisResult source)
        {
            PropertyInfo[] properties = typeof(AnalysisResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    var value = property.GetValue(source);
                    property.SetValue(this, value);
                }
            }
        }

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

        private List<string> _bullishDrivers = new List<string>();
        public List<string> BullishDrivers { get => _bullishDrivers; set => SetProperty(ref _bullishDrivers, value); }

        private List<string> _bearishDrivers = new List<string>();
        public List<string> BearishDrivers { get => _bearishDrivers; set => SetProperty(ref _bearishDrivers, value); }

        public List<string> KeySignalDrivers => BullishDrivers.Concat(BearishDrivers).ToList();

        private string _securityId = string.Empty;
        public string SecurityId { get => _securityId; set => SetProperty(ref _securityId, value); }

        private string _symbol = string.Empty;
        public string Symbol { get => _symbol; set => SetProperty(ref _symbol, value); }

        private decimal _ltp;
        public decimal LTP { get => _ltp; set => SetProperty(ref _ltp, value); }

        private decimal _priceChange;
        public decimal PriceChange { get => _priceChange; set => SetProperty(ref _priceChange, value); }
        private decimal _priceChangePercent;
        public decimal PriceChangePercent { get => _priceChangePercent; set => SetProperty(ref _priceChangePercent, value); }

        private decimal _vwap;
        public decimal Vwap { get => _vwap; set => SetProperty(ref _vwap, value); }

        private long _currentVolume;
        public long CurrentVolume { get => _currentVolume; set => SetProperty(ref _currentVolume, value); }

        private long _avgVolume;
        public long AvgVolume { get => _avgVolume; set => SetProperty(ref _avgVolume, value); }

        private string _volumeSignal = "Neutral";
        public string VolumeSignal { get => _volumeSignal; set => SetSignalProperty(ref _volumeSignal, value); }
        public string VolumeSignalStability => GetStabilityText(nameof(VolumeSignal));

        private string _oiSignal = "N/A";
        public string OiSignal { get => _oiSignal; set => SetSignalProperty(ref _oiSignal, value); }
        public string OiSignalStability => GetStabilityText(nameof(OiSignal));

        private string _instrumentGroup = string.Empty;
        public string InstrumentGroup { get => _instrumentGroup; set => SetProperty(ref _instrumentGroup, value); }

        private string _underlyingGroup = string.Empty;
        public string UnderlyingGroup { get => _underlyingGroup; set => SetProperty(ref _underlyingGroup, value); }

        private string _emaSignal1Min = "N/A";
        public string EmaSignal1Min { get => _emaSignal1Min; set => SetSignalProperty(ref _emaSignal1Min, value); }

        private string _emaSignal5Min = "N/A";
        public string EmaSignal5Min { get => _emaSignal5Min; set => SetSignalProperty(ref _emaSignal5Min, value); }
        public string EmaSignal5MinStability => GetStabilityText(nameof(EmaSignal5Min));

        private string _emaSignal15Min = "N/A";
        public string EmaSignal15Min { get => _emaSignal15Min; set => SetSignalProperty(ref _emaSignal15Min, value); }
        public string EmaSignal15MinStability => GetStabilityText(nameof(EmaSignal15Min));

        private string _vwapEmaSignal1Min = "N/A";
        public string VwapEmaSignal1Min { get => _vwapEmaSignal1Min; set => SetSignalProperty(ref _vwapEmaSignal1Min, value); }

        private string _vwapEmaSignal5Min = "N/A";
        public string VwapEmaSignal5Min { get => _vwapEmaSignal5Min; set => SetSignalProperty(ref _vwapEmaSignal5Min, value); }

        private string _vwapEmaSignal15Min = "N/A";
        public string VwapEmaSignal15Min { get => _vwapEmaSignal15Min; set => SetSignalProperty(ref _vwapEmaSignal15Min, value); }

        private string _priceVsVwapSignal = "Neutral";
        public string PriceVsVwapSignal { get => _priceVsVwapSignal; set => SetSignalProperty(ref _priceVsVwapSignal, value); }
        public string PriceVsVwapSignalStability => GetStabilityText(nameof(PriceVsVwapSignal));

        private string _priceVsCloseSignal = "Neutral";
        public string PriceVsCloseSignal { get => _priceVsCloseSignal; set => SetSignalProperty(ref _priceVsCloseSignal, value); }
        public string PriceVsCloseSignalStability => GetStabilityText(nameof(PriceVsCloseSignal));

        private string _dayRangeSignal = "Neutral";
        public string DayRangeSignal { get => _dayRangeSignal; set => SetSignalProperty(ref _dayRangeSignal, value); }
        public string DayRangeSignalStability => GetStabilityText(nameof(DayRangeSignal));

        private string _customLevelSignal = "N/A";
        public string CustomLevelSignal { get => _customLevelSignal; set => SetSignalProperty(ref _customLevelSignal, value); }

        private string _candleSignal1Min = "N/A";
        public string CandleSignal1Min { get => _candleSignal1Min; set => SetSignalProperty(ref _candleSignal1Min, value); }

        private string _candleSignal5Min = "N/A";
        public string CandleSignal5Min { get => _candleSignal5Min; set => SetSignalProperty(ref _candleSignal5Min, value); }
        public string CandleSignal5MinStability => GetStabilityText(nameof(CandleSignal5Min));

        private decimal _currentIv;
        public decimal CurrentIv { get => _currentIv; set => SetProperty(ref _currentIv, value); }

        private decimal _avgIv;
        public decimal AvgIv { get => _avgIv; set => SetProperty(ref _avgIv, value); }

        private string _ivSignal = "N/A";
        public string IvSignal { get => _ivSignal; set => SetSignalProperty(ref _ivSignal, value); }

        private string _ivSkewSignal = "N/A";
        public string IvSkewSignal { get => _ivSkewSignal; set => SetSignalProperty(ref _ivSkewSignal, value); }
        public string IvSkewSignalStability => GetStabilityText(nameof(IvSkewSignal));

        private decimal _rsiValue1Min;
        public decimal RsiValue1Min { get => _rsiValue1Min; set => SetProperty(ref _rsiValue1Min, value); }

        private string _rsiSignal1Min = "N/A";
        public string RsiSignal1Min { get => _rsiSignal1Min; set => SetSignalProperty(ref _rsiSignal1Min, value); }

        private decimal _rsiValue5Min;
        public decimal RsiValue5Min { get => _rsiValue5Min; set => SetProperty(ref _rsiValue5Min, value); }

        private string _rsiSignal5Min = "N/A";
        public string RsiSignal5Min { get => _rsiSignal5Min; set => SetSignalProperty(ref _rsiSignal5Min, value); }
        public string RsiSignal5MinStability => GetStabilityText(nameof(RsiSignal5Min));

        private decimal _obvValue1Min;
        public decimal ObvValue1Min { get => _obvValue1Min; set => SetProperty(ref _obvValue1Min, value); }

        private string _obvSignal1Min = "N/A";
        public string ObvSignal1Min { get => _obvSignal1Min; set => SetSignalProperty(ref _obvSignal1Min, value); }

        private string _obvDivergenceSignal1Min = "N/A";
        public string ObvDivergenceSignal1Min { get => _obvDivergenceSignal1Min; set => SetSignalProperty(ref _obvDivergenceSignal1Min, value); }

        private decimal _obvValue5Min;
        public decimal ObvValue5Min { get => _obvValue5Min; set => SetProperty(ref _obvValue5Min, value); }

        private string _obvSignal5Min = "N/A";
        public string ObvSignal5Min { get => _obvSignal5Min; set => SetSignalProperty(ref _obvSignal5Min, value); }

        private string _obvDivergenceSignal5Min = "N/A";
        public string ObvDivergenceSignal5Min { get => _obvDivergenceSignal5Min; set => SetSignalProperty(ref _obvDivergenceSignal5Min, value); }
        public string ObvDivergenceSignal5MinStability => GetStabilityText(nameof(ObvDivergenceSignal5Min));

        private decimal _atr1Min;
        public decimal Atr1Min { get => _atr1Min; set => SetProperty(ref _atr1Min, value); }

        private string _atrSignal1Min = "N/A";
        public string AtrSignal1Min { get => _atrSignal1Min; set => SetSignalProperty(ref _atrSignal1Min, value); }

        private decimal _atr5Min;
        public decimal Atr5Min { get => _atr5Min; set => SetProperty(ref _atr5Min, value); }

        private string _atrSignal5Min = "N/A";
        public string AtrSignal5Min { get => _atrSignal5Min; set => SetSignalProperty(ref _atrSignal5Min, value); }
        public string AtrSignal5MinStability => GetStabilityText(nameof(AtrSignal5Min));

        private decimal _ivRank;
        public decimal IvRank { get => _ivRank; set => SetProperty(ref _ivRank, value); }

        private decimal _ivPercentile;
        public decimal IvPercentile { get => _ivPercentile; set => SetProperty(ref _ivPercentile, value); }

        private string _ivTrendSignal = "N/A";
        public string IvTrendSignal { get => _ivTrendSignal; set => SetSignalProperty(ref _ivTrendSignal, value); }

        private decimal _developingPoc;
        public decimal DevelopingPoc { get => _developingPoc; set => SetProperty(ref _developingPoc, value); }

        private decimal _developingVah;
        public decimal DevelopingVah { get => _developingVah; set => SetProperty(ref _developingVah, value); }

        private decimal _developingVal;
        public decimal DevelopingVal { get => _developingVal; set => SetProperty(ref _developingVal, value); }

        private decimal _developingVpoc;
        public decimal DevelopingVpoc { get => _developingVpoc; set => SetProperty(ref _developingVpoc, value); }

        private string _dailyBias = "Calculating...";
        public string DailyBias { get => _dailyBias; set => SetSignalProperty(ref _dailyBias, value); }
        public string DailyBiasStability => GetStabilityText(nameof(DailyBias));

        private string _marketStructure = "N/A";
        public string MarketStructure { get => _marketStructure; set => SetSignalProperty(ref _marketStructure, value); }
        public string MarketStructureStability => GetStabilityText(nameof(MarketStructure));

        private decimal _initialBalanceHigh;
        public decimal InitialBalanceHigh { get => _initialBalanceHigh; set => SetProperty(ref _initialBalanceHigh, value); }

        private decimal _initialBalanceLow;
        public decimal InitialBalanceLow { get => _initialBalanceLow; set => SetProperty(ref _initialBalanceLow, value); }

        private string _initialBalanceSignal = "N/A";
        public string InitialBalanceSignal { get => _initialBalanceSignal; set => SetSignalProperty(ref _initialBalanceSignal, value); }
        public string InitialBalanceSignalStability => GetStabilityText(nameof(InitialBalanceSignal));

        private string _marketProfileSignal = "N/A";
        public string MarketProfileSignal { get => _marketProfileSignal; set => SetSignalProperty(ref _marketProfileSignal, value); }
        public string MarketProfileSignalStability => GetStabilityText(nameof(MarketProfileSignal));

        private int _convictionScore;
        public int ConvictionScore { get => _convictionScore; set => SetProperty(ref _convictionScore, value); }

        private string _finalTradeSignal = "Analyzing...";
        public string FinalTradeSignal { get => _finalTradeSignal; set => SetProperty(ref _finalTradeSignal, value); }

        private string _primarySignal = "Initializing";
        public string PrimarySignal { get => _primarySignal; set => SetProperty(ref _primarySignal, value); }

        private decimal _stopLoss;
        public decimal StopLoss { get => _stopLoss; set => SetProperty(ref _stopLoss, value); }

        private decimal _targetPrice;
        public decimal TargetPrice { get => _targetPrice; set => SetProperty(ref _targetPrice, value); }

        private string _institutionalIntent = "N/A";
        public string InstitutionalIntent { get => _institutionalIntent; set => SetSignalProperty(ref _institutionalIntent, value); }
        public string InstitutionalIntentStability => GetStabilityText(nameof(InstitutionalIntent));

        private string _openTypeSignal = "N/A";
        public string OpenTypeSignal { get => _openTypeSignal; set => SetSignalProperty(ref _openTypeSignal, value); }
        public string OpenTypeSignalStability => GetStabilityText(nameof(OpenTypeSignal));

        private string _yesterdayProfileSignal = "N/A";
        public string YesterdayProfileSignal { get => _yesterdayProfileSignal; set => SetSignalProperty(ref _yesterdayProfileSignal, value); }
        public string YesterdayProfileSignalStability => GetStabilityText(nameof(YesterdayProfileSignal));

        private string _vwapBandSignal = "N/A";
        public string VwapBandSignal { get => _vwapBandSignal; set => SetSignalProperty(ref _vwapBandSignal, value); }
        public string VwapBandSignalStability => GetStabilityText(nameof(VwapBandSignal));

        private decimal _vwapUpperBand;
        public decimal VwapUpperBand { get => _vwapUpperBand; set => SetProperty(ref _vwapUpperBand, value); }

        private decimal _vwapLowerBand;
        public decimal VwapLowerBand { get => _vwapLowerBand; set => SetProperty(ref _vwapLowerBand, value); }

        private decimal _anchoredVwap;
        public decimal AnchoredVwap { get => _anchoredVwap; set => SetProperty(ref _anchoredVwap, value); }

        private string _marketNarrative = "Analyzing...";
        public string MarketNarrative { get => _marketNarrative; set => SetProperty(ref _marketNarrative, value); }

        private MarketThesis _marketThesis = MarketThesis.Indeterminate;
        public MarketThesis MarketThesis { get => _marketThesis; set => SetSignalProperty(ref _marketThesis, value); }
        public string MarketThesisStability => GetStabilityText(nameof(MarketThesis));

        private DominantPlayer _dominantPlayer = DominantPlayer.Indeterminate;
        public DominantPlayer DominantPlayer { get => _dominantPlayer; set => SetProperty(ref _dominantPlayer, value); }

        private string _volatilityStateSignal = "N/A";
        public string VolatilityStateSignal { get => _volatilityStateSignal; set => SetSignalProperty(ref _volatilityStateSignal, value); }

        private string _marketRegime = "N/A";
        public string MarketRegime { get => _marketRegime; set => SetSignalProperty(ref _marketRegime, value); }
        public string MarketRegimeStability => GetStabilityText(nameof(MarketRegime));

        private string _intradayIvSpikeSignal = "N/A";
        public string IntradayIvSpikeSignal { get => _intradayIvSpikeSignal; set => SetSignalProperty(ref _intradayIvSpikeSignal, value); }

        private string _gammaSignal = "N/A";
        public string GammaSignal { get => _gammaSignal; set => SetSignalProperty(ref _gammaSignal, value); }


        public string FullGroupIdentifier
        {
            get
            {
                if (InstrumentGroup == "OPTIDX" || InstrumentGroup == "OPTSTK")
                {
                    if (Symbol.ToUpper().Contains("NIFTY") && !Symbol.ToUpper().Contains("BANK")) return "Nifty Options";
                    if (Symbol.ToUpper().Contains("BANKNIFTY")) return "Banknifty Options";
                    if (Symbol.ToUpper().Contains("SENSEX")) return "Sensex Options";
                    return "Other Stock Options";
                }
                if (InstrumentGroup == "FUTIDX" || InstrumentGroup == "FUTSTK")
                {
                    if (Symbol.ToUpper().Contains("NIFTY") || Symbol.ToUpper().Contains("BANKNIFTY") || Symbol.ToUpper().Contains("SENSEX"))
                        return "Index Futures";
                    return "Stock Futures";
                }
                return InstrumentGroup;
            }
        }
    }
}