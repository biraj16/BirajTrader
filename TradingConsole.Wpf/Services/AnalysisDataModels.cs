// TradingConsole.Wpf/Services/AnalysisDataModels.cs
// --- MODIFIED: Added the core logic to calculate POC and Value Area ---
using System;
using System.Collections.Generic;
using System.Linq;
using TradingConsole.Core.Models;

namespace TradingConsole.Wpf.Services
{
    #region Core Data Models

    /// <summary>
    /// NEW: Defines the current phase of the trading session.
    /// </summary>
    public enum MarketPhase
    {
        PreOpen,
        Opening, // First 30 minutes, signals are de-weighted
        Normal,
        Closing // Last 30 minutes
    }

    /// <summary>
    /// Represents a single candlestick with price, volume, and open interest data.
    /// </summary>
    public class Candle
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public long OpenInterest { get; set; }
        public decimal Vwap { get; set; }
        public decimal AnchoredVwap { get; set; }
        internal decimal CumulativePriceVolume { get; set; } = 0;
        internal long CumulativeVolume { get; set; } = 0;

        public override string ToString()
        {
            return $"T: {Timestamp:HH:mm:ss}, O: {Open}, H: {High}, L: {Low}, C: {Close}, V: {Volume}";
        }
    }

    #endregion

    #region Indicator State Models

    // EmaState, RsiState, AtrState, ObvState remain unchanged...
    public class EmaState { public decimal CurrentShortEma { get; set; } public decimal CurrentLongEma { get; set; } }
    public class RsiState { public decimal AvgGain { get; set; } public decimal AvgLoss { get; set; } public List<decimal> RsiValues { get; } = new List<decimal>(); }
    public class AtrState { public decimal CurrentAtr { get; set; } public List<decimal> AtrValues { get; } = new List<decimal>(); }
    public class ObvState { public decimal CurrentObv { get; set; } public List<decimal> ObvValues { get; } = new List<decimal>(); public decimal CurrentMovingAverage { get; set; } }

    #endregion

    #region Market Context Models

    // RelativeStrengthState, IvSkewState remain unchanged...
    public class RelativeStrengthState { public List<decimal> BasisDeltaHistory { get; } = new List<decimal>(); public List<decimal> OptionsDeltaHistory { get; } = new List<decimal>(); public string InstitutionalIntentSignal { get; set; } = "Neutral"; }
    public class IvSkewState { public List<decimal> AtmCallIvHistory { get; } = new List<decimal>(); public List<decimal> AtmPutIvHistory { get; } = new List<decimal>(); public List<decimal> OtmCallIvHistory { get; } = new List<decimal>(); public List<decimal> OtmPutIvHistory { get; } = new List<decimal>(); public List<decimal> PutSkewSlopeHistory { get; } = new List<decimal>(); public List<decimal> CallSkewSlopeHistory { get; } = new List<decimal>(); }


    /// <summary>
    /// Holds the state for intraday Implied Volatility (IV) analysis, including daily range and percentile history.
    /// </summary>
    public class IntradayIvState
    {
        public decimal DayHighIv { get; set; } = 0;
        public decimal DayLowIv { get; set; } = decimal.MaxValue;
        public List<decimal> IvPercentileHistory { get; } = new List<decimal>();

        /// <summary>
        /// NEW: Added a list to track recent IV values for spike detection.
        /// </summary>
        public List<decimal> IvHistory { get; } = new List<decimal>();

        internal enum PriceZone { Inside, Above, Below }
        public class CustomLevelState { public int BreakoutCount { get; set; } public int BreakdownCount { get; set; } internal PriceZone LastZone { get; set; } = PriceZone.Inside; }
    }

    #endregion

    #region Market Profile

    /// <summary>
    /// Represents and calculates the market profile for a trading session, including TPO and Volume profiles.
    /// </summary>
    public class MarketProfile
    {
        public SortedDictionary<decimal, List<char>> TpoLevels { get; } = new SortedDictionary<decimal, List<char>>();

        /// <summary>
        /// NEW: Added VolumeLevels to store actual traded volume at each price, enabling true Volume Profile analysis.
        /// </summary>
        public SortedDictionary<decimal, long> VolumeLevels { get; } = new SortedDictionary<decimal, long>();

        public TpoInfo TpoLevelsInfo { get; set; } = new TpoInfo();
        public VolumeProfileInfo VolumeProfileInfo { get; set; } = new VolumeProfileInfo();
        public decimal TickSize { get; }
        private readonly DateTime _sessionStartTime;
        private readonly DateTime _initialBalanceEndTime;

        public string LastMarketSignal { get; set; } = string.Empty;
        public DateTime Date { get; set; }

        public decimal InitialBalanceHigh { get; private set; }
        public decimal InitialBalanceLow { get; private set; }
        public bool IsInitialBalanceSet { get; private set; }

        public TpoInfo DevelopingTpoLevels { get; set; } = new TpoInfo();
        public VolumeProfileInfo DevelopingVolumeProfile { get; set; } = new VolumeProfileInfo();

        public MarketProfile(decimal tickSize, DateTime sessionStartTime)
        {
            TickSize = tickSize;
            _sessionStartTime = sessionStartTime;
            _initialBalanceEndTime = _sessionStartTime.AddMinutes(60); // IB is typically the first hour
            Date = sessionStartTime.Date;
            InitialBalanceLow = decimal.MaxValue;
        }

        public char GetTpoPeriod(DateTime timestamp)
        {
            var elapsed = timestamp - _sessionStartTime;
            int periodIndex = (int)(elapsed.TotalMinutes / 30);
            return (char)('A' + periodIndex);
        }

        public decimal QuantizePrice(decimal price)
        {
            return Math.Round(price / TickSize) * TickSize;
        }

        public void UpdateInitialBalance(Candle candle)
        {
            if (candle.Timestamp <= _initialBalanceEndTime)
            {
                InitialBalanceHigh = Math.Max(InitialBalanceHigh, candle.High);
                InitialBalanceLow = Math.Min(InitialBalanceLow, candle.Low);
            }
            else if (!IsInitialBalanceSet)
            {
                IsInitialBalanceSet = true;
            }
        }

        /// <summary>
        /// --- NEW: This method contains the core logic to calculate the derived profile metrics ---
        /// It finds the Point of Control and Value Area based on the collected TPO and Volume data.
        /// </summary>
        public void CalculateProfileMetrics()
        {
            if (TpoLevels.Any())
            {
                // Calculate TPO Point of Control (POC) and Value Area (VA)
                var pocPrice = TpoLevels.OrderByDescending(kvp => kvp.Value.Count).First().Key;
                DevelopingTpoLevels.PointOfControl = pocPrice;

                long totalTpos = TpoLevels.Sum(kvp => kvp.Value.Count);
                long tposInValueAreaTarget = (long)(totalTpos * 0.70);
                long currentTposCount = TpoLevels[pocPrice].Count;

                var sortedLevels = TpoLevels.Keys.ToList();
                int pocIndex = sortedLevels.IndexOf(pocPrice);
                int upperIndex = pocIndex;
                int lowerIndex = pocIndex;

                // Expand outwards from the POC, adding the level with the higher TPO count, until 70% of TPOs are included.
                while (currentTposCount < tposInValueAreaTarget && (lowerIndex > 0 || upperIndex < sortedLevels.Count - 1))
                {
                    decimal nextUpperPrice = (upperIndex + 1 < sortedLevels.Count) ? sortedLevels[upperIndex + 1] : decimal.MaxValue;
                    long upperTpos = (upperIndex + 1 < sortedLevels.Count) ? TpoLevels[nextUpperPrice].Count : 0;

                    decimal nextLowerPrice = (lowerIndex - 1 >= 0) ? sortedLevels[lowerIndex - 1] : decimal.MinValue;
                    long lowerTpos = (lowerIndex - 1 >= 0) ? TpoLevels[nextLowerPrice].Count : 0;

                    if (upperTpos > lowerTpos)
                    {
                        currentTposCount += upperTpos;
                        upperIndex++;
                    }
                    else if (lowerTpos > 0)
                    {
                        currentTposCount += lowerTpos;
                        lowerIndex--;
                    }
                    else if (upperTpos > 0) // Only upper levels are left to check
                    {
                        currentTposCount += upperTpos;
                        upperIndex++;
                    }
                    else // No more levels to check
                    {
                        break;
                    }
                }

                DevelopingTpoLevels.ValueAreaHigh = sortedLevels[upperIndex];
                DevelopingTpoLevels.ValueAreaLow = sortedLevels[lowerIndex];
            }

            if (VolumeLevels.Any())
            {
                // Calculate Volume Point of Control (VPOC)
                DevelopingVolumeProfile.VolumePoc = VolumeLevels.OrderByDescending(kvp => kvp.Value).First().Key;
            }
        }


        public MarketProfileData ToMarketProfileData()
        {
            var tpoCounts = this.TpoLevels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
            return new MarketProfileData { Date = this.Date, TpoLevelsInfo = this.DevelopingTpoLevels, VolumeProfileInfo = this.DevelopingVolumeProfile, TpoCounts = tpoCounts, VolumeLevels = new Dictionary<decimal, long>(this.VolumeLevels) };
        }
    }

    #endregion
}
