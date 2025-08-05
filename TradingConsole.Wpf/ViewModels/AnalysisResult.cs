// TradingConsole.Wpf/ViewModels/AnalysisResult.cs
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    public class AnalysisResult : ObservableModel
    {
        /// <summary>
        /// --- REFACTORED: This method now uses reflection to update properties. ---
        /// This is more robust and maintainable than manually assigning each property.
        /// It automatically handles any new properties added to the class.
        /// </summary>
        /// <param name="source">The source object with the latest analysis data.</param>
        public void Update(AnalysisResult source)
        {
            // Get all public instance properties of the AnalysisResult class.
            PropertyInfo[] properties = typeof(AnalysisResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Ensure the property can be written to and is not an indexed property.
                if (property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    // Get the value from the source object.
                    var value = property.GetValue(source);
                    // Set the value on the current (destination) object.
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
        private string _symbol = string.Empty;
        private decimal _ltp;
        private decimal _priceChange;
        public decimal PriceChange { get => _priceChange; set => SetProperty(ref _priceChange, value); }
        private decimal _priceChangePercent;
        public decimal PriceChangePercent { get => _priceChangePercent; set => SetProperty(ref _priceChangePercent, value); }

        private decimal _vwap;
        private long _currentVolume;
        private long _avgVolume;
        private string _volumeSignal = "Neutral";
        private string _oiSignal = "N/A";
        private string _instrumentGroup = string.Empty;
        private string _underlyingGroup = string.Empty;
        private string _emaSignal1Min = "N/A";
        private string _emaSignal5Min = "N/A";
        private string _emaSignal15Min = "N/A";
        private string _vwapEmaSignal1Min = "N/A";
        private string _vwapEmaSignal5Min = "N/A";
        private string _vwapEmaSignal15Min = "N/A";
        private string _priceVsVwapSignal = "Neutral";
        private string _priceVsCloseSignal = "Neutral";
        private string _dayRangeSignal = "Neutral";
        private string _customLevelSignal = "N/A";
        private string _candleSignal1Min = "N/A";
        private string _candleSignal5Min = "N/A";
        private decimal _currentIv;
        private decimal _avgIv;
        private string _ivSignal = "N/A";
        private string _ivSkewSignal = "N/A";
        public string IvSkewSignal { get => _ivSkewSignal; set => SetProperty(ref _ivSkewSignal, value); }

        private decimal _rsiValue1Min;
        public decimal RsiValue1Min { get => _rsiValue1Min; set => SetProperty(ref _rsiValue1Min, value); }
        private string _rsiSignal1Min = "N/A";
        public string RsiSignal1Min { get => _rsiSignal1Min; set => SetProperty(ref _rsiSignal1Min, value); }
        private decimal _rsiValue5Min;
        public decimal RsiValue5Min { get => _rsiValue5Min; set => SetProperty(ref _rsiValue5Min, value); }
        private string _rsiSignal5Min = "N/A";
        public string RsiSignal5Min { get => _rsiSignal5Min; set => SetProperty(ref _rsiSignal5Min, value); }

        private decimal _obvValue1Min;
        public decimal ObvValue1Min { get => _obvValue1Min; set => SetProperty(ref _obvValue1Min, value); }
        private string _obvSignal1Min = "N/A";
        public string ObvSignal1Min { get => _obvSignal1Min; set => SetProperty(ref _obvSignal1Min, value); }
        private string _obvDivergenceSignal1Min = "N/A";
        public string ObvDivergenceSignal1Min { get => _obvDivergenceSignal1Min; set => SetProperty(ref _obvDivergenceSignal1Min, value); }

        private decimal _obvValue5Min;
        public decimal ObvValue5Min { get => _obvValue5Min; set => SetProperty(ref _obvValue5Min, value); }
        private string _obvSignal5Min = "N/A";
        public string ObvSignal5Min { get => _obvSignal5Min; set => SetProperty(ref _obvSignal5Min, value); }
        private string _obvDivergenceSignal5Min = "N/A";
        public string ObvDivergenceSignal5Min { get => _obvDivergenceSignal5Min; set => SetProperty(ref _obvDivergenceSignal5Min, value); }

        private decimal _atr1Min;
        public decimal Atr1Min { get => _atr1Min; set => SetProperty(ref _atr1Min, value); }
        private string _atrSignal1Min = "N/A";
        public string AtrSignal1Min { get => _atrSignal1Min; set => SetProperty(ref _atrSignal1Min, value); }

        private decimal _atr5Min;
        public decimal Atr5Min { get => _atr5Min; set => SetProperty(ref _atr5Min, value); }
        private string _atrSignal5Min = "N/A";
        public string AtrSignal5Min { get => _atrSignal5Min; set => SetProperty(ref _atrSignal5Min, value); }

        private decimal _ivRank;
        public decimal IvRank { get => _ivRank; set => SetProperty(ref _ivRank, value); }
        private decimal _ivPercentile;
        public decimal IvPercentile { get => _ivPercentile; set => SetProperty(ref _ivPercentile, value); }
        private string _ivTrendSignal = "N/A";
        public string IvTrendSignal { get => _ivTrendSignal; set => SetProperty(ref _ivTrendSignal, value); }

        public decimal CurrentIv { get => _currentIv; set => SetProperty(ref _currentIv, value); }
        public decimal AvgIv { get => _avgIv; set => SetProperty(ref _avgIv, value); }
        public string IvSignal { get => _ivSignal; set => SetProperty(ref _ivSignal, value); }

        private decimal _developingPoc;
        public decimal DevelopingPoc { get => _developingPoc; set => SetProperty(ref _developingPoc, value); }
        private decimal _developingVah;
        public decimal DevelopingVah { get => _developingVah; set => SetProperty(ref _developingVah, value); }
        private decimal _developingVal;
        public decimal DevelopingVal { get => _developingVal; set => SetProperty(ref _developingVal, value); }
        private decimal _developingVpoc;
        public decimal DevelopingVpoc { get => _developingVpoc; set => SetProperty(ref _developingVpoc, value); }

        private string _dailyBias = "Calculating...";
        public string DailyBias { get => _dailyBias; set => SetProperty(ref _dailyBias, value); }

        private string _marketStructure = "N/A";
        public string MarketStructure { get => _marketStructure; set => SetProperty(ref _marketStructure, value); }

        private decimal _initialBalanceHigh;
        public decimal InitialBalanceHigh { get => _initialBalanceHigh; set => SetProperty(ref _initialBalanceHigh, value); }
        private decimal _initialBalanceLow;
        public decimal InitialBalanceLow { get => _initialBalanceLow; set => SetProperty(ref _initialBalanceLow, value); }
        private string _initialBalanceSignal = "N/A";
        public string InitialBalanceSignal { get => _initialBalanceSignal; set => SetProperty(ref _initialBalanceSignal, value); }

        private string _marketProfileSignal = "N/A";
        public string MarketProfileSignal { get => _marketProfileSignal; set => SetProperty(ref _marketProfileSignal, value); }
        public string CandleSignal1Min { get => _candleSignal1Min; set => SetProperty(ref _candleSignal1Min, value); }
        public string CandleSignal5Min { get => _candleSignal5Min; set => SetProperty(ref _candleSignal5Min, value); }
        public string CustomLevelSignal { get => _customLevelSignal; set => SetProperty(ref _customLevelSignal, value); }
        public string SecurityId { get => _securityId; set => SetProperty(ref _securityId, value); }
        public string Symbol { get => _symbol; set => SetProperty(ref _symbol, value); }
        public decimal LTP { get => _ltp; set => SetProperty(ref _ltp, value); }
        public decimal Vwap { get => _vwap; set => SetProperty(ref _vwap, value); }
        public long CurrentVolume { get => _currentVolume; set => SetProperty(ref _currentVolume, value); }
        public long AvgVolume { get => _avgVolume; set => SetProperty(ref _avgVolume, value); }
        public string VolumeSignal { get => _volumeSignal; set => SetProperty(ref _volumeSignal, value); }
        public string OiSignal { get => _oiSignal; set => SetProperty(ref _oiSignal, value); }
        public string InstrumentGroup { get => _instrumentGroup; set => SetProperty(ref _instrumentGroup, value); }
        public string UnderlyingGroup { get => _underlyingGroup; set => SetProperty(ref _underlyingGroup, value); }
        public string EmaSignal1Min { get => _emaSignal1Min; set => SetProperty(ref _emaSignal1Min, value); }
        public string EmaSignal5Min { get => _emaSignal5Min; set => SetProperty(ref _emaSignal5Min, value); }
        public string EmaSignal15Min { get => _emaSignal15Min; set => SetProperty(ref _emaSignal15Min, value); }
        public string VwapEmaSignal1Min { get => _vwapEmaSignal1Min; set => SetProperty(ref _vwapEmaSignal1Min, value); }
        public string VwapEmaSignal5Min { get => _vwapEmaSignal5Min; set => SetProperty(ref _vwapEmaSignal5Min, value); }
        public string VwapEmaSignal15Min { get => _vwapEmaSignal15Min; set => SetProperty(ref _vwapEmaSignal15Min, value); }
        public string PriceVsVwapSignal { get => _priceVsVwapSignal; set => SetProperty(ref _priceVsVwapSignal, value); }
        public string PriceVsCloseSignal { get => _priceVsCloseSignal; set => SetProperty(ref _priceVsCloseSignal, value); }
        public string DayRangeSignal { get => _dayRangeSignal; set => SetProperty(ref _dayRangeSignal, value); }

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
        public string InstitutionalIntent { get => _institutionalIntent; set => SetProperty(ref _institutionalIntent, value); }

        private string _openTypeSignal = "N/A";
        public string OpenTypeSignal { get => _openTypeSignal; set => SetProperty(ref _openTypeSignal, value); }

        private string _yesterdayProfileSignal = "N/A";
        public string YesterdayProfileSignal { get => _yesterdayProfileSignal; set => SetProperty(ref _yesterdayProfileSignal, value); }

        private string _vwapBandSignal = "N/A";
        public string VwapBandSignal { get => _vwapBandSignal; set => SetProperty(ref _vwapBandSignal, value); }

        private decimal _vwapUpperBand;
        public decimal VwapUpperBand { get => _vwapUpperBand; set => SetProperty(ref _vwapUpperBand, value); }

        private decimal _vwapLowerBand;
        public decimal VwapLowerBand { get => _vwapLowerBand; set => SetProperty(ref _vwapLowerBand, value); }

        private decimal _anchoredVwap;
        public decimal AnchoredVwap { get => _anchoredVwap; set => SetProperty(ref _anchoredVwap, value); }

        private string _marketNarrative = "Analyzing...";
        public string MarketNarrative { get => _marketNarrative; set => SetProperty(ref _marketNarrative, value); }

        private MarketThesis _marketThesis = MarketThesis.Indeterminate;
        public MarketThesis MarketThesis { get => _marketThesis; set => SetProperty(ref _marketThesis, value); }

        private DominantPlayer _dominantPlayer = DominantPlayer.Indeterminate;
        public DominantPlayer DominantPlayer { get => _dominantPlayer; set => SetProperty(ref _dominantPlayer, value); }

        private string _volatilityStateSignal = "N/A";
        public string VolatilityStateSignal { get => _volatilityStateSignal; set => SetProperty(ref _volatilityStateSignal, value); }

        // --- NEW PROPERTIES ---
        private string _marketRegime = "N/A";
        public string MarketRegime { get => _marketRegime; set => SetProperty(ref _marketRegime, value); }

        private string _intradayIvSpikeSignal = "N/A";
        public string IntradayIvSpikeSignal { get => _intradayIvSpikeSignal; set => SetProperty(ref _intradayIvSpikeSignal, value); }

        // --- ADDED: Property to hold the Gamma signal ---
        private string _gammaSignal = "N/A";
        public string GammaSignal { get => _gammaSignal; set => SetProperty(ref _gammaSignal, value); }


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
