// TradingConsole.DhanApi/Models/OptionChainModels.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TradingConsole.Core.Models;

namespace TradingConsole.DhanApi.Models
{
    public class Index
    {
        public string Name { get; set; } = string.Empty;
        public string ScripId { get; set; } = string.Empty;
        public string Segment { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string ExchId { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public class OptionChainResponse
    {
        [JsonPropertyName("data")]
        public OptionStrikeData? Data { get; set; }
    }

    public class OptionStrikeData
    {
        [JsonPropertyName("last_price")]
        public decimal UnderlyingLastPrice { get; set; }

        [JsonPropertyName("oc")]
        public Dictionary<string, OptionStrike>? OptionChain { get; set; }
    }

    public class OptionStrike
    {
        [JsonPropertyName("strikePrice")]
        public decimal StrikePrice { get; set; }

        [JsonPropertyName("ce")]
        public OptionData? CallOption { get; set; }

        [JsonPropertyName("pe")]
        public OptionData? PutOption { get; set; }
    }

    public class OptionData : ObservableModel
    {
        private decimal _lastPrice;
        private int _openInterest;
        private long _volume;
        private Greeks? _greeks;
        private string _securityId = string.Empty;

        [JsonPropertyName("securityId")]
        public string SecurityId { get => _securityId; set { _securityId = value; OnPropertyChanged(nameof(SecurityId)); } }

        [JsonPropertyName("last_price")]
        public decimal LastPrice { get => _lastPrice; set { if (_lastPrice != value) { _lastPrice = value; OnPropertyChanged(nameof(LastPrice)); OnPropertyChanged(nameof(LtpChange)); OnPropertyChanged(nameof(LtpChangePercent)); } } }

        [JsonPropertyName("previous_close_price")]
        public decimal PreviousClose { get; set; }

        [JsonPropertyName("oi")]
        public int OpenInterest { get => _openInterest; set { if (_openInterest != value) { _openInterest = value; OnPropertyChanged(nameof(OpenInterest)); OnPropertyChanged(nameof(OiChange)); OnPropertyChanged(nameof(OiChangePercent)); } } }

        [JsonPropertyName("previous_oi")]
        public int PreviousOpenInterest { get; set; }

        [JsonPropertyName("volume")]
        public long Volume { get => _volume; set { if (_volume != value) { _volume = value; OnPropertyChanged(nameof(Volume)); } } }

        [JsonPropertyName("implied_volatility")]
        public decimal ImpliedVolatility { get; set; }

        [JsonPropertyName("greeks")]
        public Greeks? Greeks { get => _greeks; set { if (_greeks != value) { _greeks = value; OnPropertyChanged(nameof(Greeks)); } } }

        public decimal LtpChange => LastPrice - PreviousClose;
        public decimal LtpChangePercent => PreviousClose == 0 ? 0 : (LtpChange / PreviousClose);
        public int OiChange => OpenInterest - PreviousOpenInterest;
        public decimal OiChangePercent => PreviousOpenInterest == 0 ? 0 : ((decimal)OiChange / PreviousOpenInterest);
    }

    public class Greeks : ObservableModel
    {
        private decimal _delta;
        // --- ADDED: Properties for Theta, Gamma, and Vega ---
        private decimal _theta;
        private decimal _gamma;
        private decimal _vega;

        [JsonPropertyName("delta")]
        public decimal Delta { get => _delta; set { if (_delta != value) { _delta = value; OnPropertyChanged(nameof(Delta)); } } }

        [JsonPropertyName("theta")]
        public decimal Theta { get => _theta; set { if (_theta != value) { _theta = value; OnPropertyChanged(nameof(Theta)); } } }

        [JsonPropertyName("gamma")]
        public decimal Gamma { get => _gamma; set { if (_gamma != value) { _gamma = value; OnPropertyChanged(nameof(Gamma)); } } }

        [JsonPropertyName("vega")]
        public decimal Vega { get => _vega; set { if (_vega != value) { _vega = value; OnPropertyChanged(nameof(Vega)); } } }
    }

    public class ExpiryListResponse
    {
        [JsonPropertyName("data")]
        public List<string>? ExpiryDates { get; set; }
    }
}
