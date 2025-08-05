// TradingConsole.Wpf/Services/Analysis/IndicatorService.cs
// --- MODIFIED: Added RSI Divergence detection logic ---
using System;
using System.Collections.Generic;
using System.Linq;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services.Analysis;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Handles the calculation of all technical indicators like EMA, RSI, ATR, OBV.
    /// </summary>
    public class IndicatorService
    {
        private readonly AnalysisStateManager _stateManager;

        public IndicatorService(AnalysisStateManager stateManager)
        {
            _stateManager = stateManager;
        }

        public decimal CalculateRsi(List<Candle> candles, RsiState state, int period)
        {
            if (candles.Count <= period) return 0m;

            var lastCandle = candles.Last();
            var secondLastCandle = candles[candles.Count - 2];
            var change = lastCandle.Close - secondLastCandle.Close;
            var gain = Math.Max(0, change);
            var loss = Math.Max(0, -change);

            if (state.AvgGain == 0)
            {
                var initialChanges = candles.Skip(1).Select((c, i) => c.Close - candles[i].Close).ToList();
                state.AvgGain = initialChanges.Take(period).Where(ch => ch > 0).DefaultIfEmpty(0).Average();
                state.AvgLoss = initialChanges.Take(period).Where(ch => ch < 0).Select(ch => -ch).DefaultIfEmpty(0).Average();
            }
            else
            {
                state.AvgGain = ((state.AvgGain * (period - 1)) + gain) / period;
                state.AvgLoss = ((state.AvgLoss * (period - 1)) + loss) / period;
            }

            if (state.AvgLoss == 0) return 100m;

            var rs = state.AvgGain / state.AvgLoss;
            var rsi = 100 - (100 / (1 + rs));

            state.RsiValues.Add(rsi);
            if (state.RsiValues.Count > 50) state.RsiValues.RemoveAt(0);

            return Math.Round(rsi, 2);
        }

        /// <summary>
        /// --- NEW: Detects bullish or bearish divergence between price and RSI ---
        /// </summary>
        public string DetectRsiDivergence(List<Candle> candles, RsiState state, int lookbackPeriod)
        {
            if (candles.Count < lookbackPeriod || state.RsiValues.Count < lookbackPeriod)
            {
                return "N/A";
            }

            var recentCandles = candles.TakeLast(lookbackPeriod).ToList();
            var recentRsi = state.RsiValues.TakeLast(lookbackPeriod).ToList();

            // Find the lowest low in price and its index
            decimal lowestPrice = recentCandles.Min(c => c.Low);
            int lowestPriceIndex = recentCandles.FindLastIndex(c => c.Low == lowestPrice);

            // Find the highest high in price and its index
            decimal highestPrice = recentCandles.Max(c => c.High);
            int highestPriceIndex = recentCandles.FindLastIndex(c => c.High == highestPrice);

            // Check for Bullish Divergence (Lower Low in Price, Higher Low in RSI)
            if (lowestPriceIndex == lookbackPeriod - 1) // The most recent candle is part of the new low
            {
                var previousCandles = recentCandles.Take(lowestPriceIndex).ToList();
                if (previousCandles.Any())
                {
                    decimal previousLowPrice = previousCandles.Min(c => c.Low);
                    int previousLowPriceIndex = previousCandles.FindLastIndex(c => c.Low == previousLowPrice);

                    if (lowestPrice < previousLowPrice) // Confirmed lower low in price
                    {
                        decimal rsiAtCurrentLow = recentRsi[lowestPriceIndex];
                        decimal rsiAtPreviousLow = recentRsi[previousLowPriceIndex];

                        if (rsiAtCurrentLow > rsiAtPreviousLow)
                        {
                            return "Bullish Divergence";
                        }
                    }
                }
            }

            // Check for Bearish Divergence (Higher High in Price, Lower High in RSI)
            if (highestPriceIndex == lookbackPeriod - 1) // The most recent candle is part of the new high
            {
                var previousCandles = recentCandles.Take(highestPriceIndex).ToList();
                if (previousCandles.Any())
                {
                    decimal previousHighPrice = previousCandles.Max(c => c.High);
                    int previousHighPriceIndex = previousCandles.FindLastIndex(c => c.High == previousHighPrice);

                    if (highestPrice > previousHighPrice) // Confirmed higher high in price
                    {
                        decimal rsiAtCurrentHigh = recentRsi[highestPriceIndex];
                        decimal rsiAtPreviousHigh = recentRsi[previousHighPriceIndex];

                        if (rsiAtCurrentHigh < rsiAtPreviousHigh)
                        {
                            return "Bearish Divergence";
                        }
                    }
                }
            }

            return "No Divergence";
        }


        public decimal CalculateAtr(List<Candle> candles, AtrState state, int period)
        {
            if (candles.Count < period) return 0m;

            var trueRanges = new List<decimal>();
            for (int i = 1; i < candles.Count; i++)
            {
                var high = candles[i].High;
                var low = candles[i].Low;
                var prevClose = candles[i - 1].Close;

                var tr = Math.Max(high - low, Math.Abs(high - prevClose));
                tr = Math.Max(tr, Math.Abs(low - prevClose));
                trueRanges.Add(tr);
            }

            if (!trueRanges.Any()) return 0m;

            if (state.CurrentAtr == 0)
            {
                state.CurrentAtr = trueRanges.Take(period).Average();
            }
            else
            {
                var lastTr = trueRanges.Last();
                state.CurrentAtr = ((state.CurrentAtr * (period - 1)) + lastTr) / period;
            }

            state.AtrValues.Add(state.CurrentAtr);
            if (state.AtrValues.Count > 20) state.AtrValues.RemoveAt(0);

            return Math.Round(state.CurrentAtr, 2);
        }

        public decimal CalculateObv(List<Candle> candles, ObvState state)
        {
            if (candles.Count < 2) return 0m;

            var lastCandle = candles.Last();
            var secondLastCandle = candles[candles.Count - 2];

            if (lastCandle.Close > secondLastCandle.Close)
            {
                state.CurrentObv += lastCandle.Volume;
            }
            else if (lastCandle.Close < secondLastCandle.Close)
            {
                state.CurrentObv -= lastCandle.Volume;
            }

            state.ObvValues.Add(state.CurrentObv);
            if (state.ObvValues.Count > 50) state.ObvValues.RemoveAt(0);

            return state.CurrentObv;
        }

        public string CalculateEmaSignal(string securityId, List<Candle> candles, Dictionary<string, Dictionary<TimeSpan, EmaState>> stateDictionary, int shortEma, int longEma, bool useVwap)
        {
            if (candles.Count < 2) return "Building History...";

            var timeframe = candles[1].Timestamp - candles[0].Timestamp;
            var state = stateDictionary[securityId][timeframe];
            var lastCandle = candles.Last();
            var price = useVwap ? lastCandle.Vwap : lastCandle.Close;

            if (state.CurrentShortEma == 0 || state.CurrentLongEma == 0)
            {
                return "Warming Up...";
            }

            decimal shortMultiplier = 2.0m / (shortEma + 1);
            decimal longMultiplier = 2.0m / (longEma + 1);

            state.CurrentShortEma = (price - state.CurrentShortEma) * shortMultiplier + state.CurrentShortEma;
            state.CurrentLongEma = (price - state.CurrentLongEma) * longMultiplier + state.CurrentLongEma;

            if (state.CurrentShortEma > state.CurrentLongEma) return "Bullish Cross";
            if (state.CurrentShortEma < state.CurrentLongEma) return "Bearish Cross";
            return "Neutral";
        }

        public void WarmupIndicators(string securityId, TimeSpan timeframe, int shortEma, int longEma)
        {
            var candles = _stateManager.GetCandles(securityId, timeframe);
            if (candles == null || !candles.Any()) return;

            var priceState = _stateManager.MultiTimeframePriceEmaState[securityId][timeframe];
            var closePrices = candles.Select(c => c.Close).ToList();
            if (closePrices.Count >= longEma)
            {
                priceState.CurrentShortEma = CalculateFullEma(closePrices, shortEma);
                priceState.CurrentLongEma = CalculateFullEma(closePrices, longEma);
            }

            var vwapState = _stateManager.MultiTimeframeVwapEmaState[securityId][timeframe];
            var vwapPrices = candles.Select(c => c.Vwap).ToList();
            if (vwapPrices.Count >= longEma)
            {
                vwapState.CurrentShortEma = CalculateFullEma(vwapPrices, shortEma);
                vwapState.CurrentLongEma = CalculateFullEma(vwapPrices, longEma);
            }
        }

        private decimal CalculateFullEma(List<decimal> prices, int period)
        {
            if (prices.Count < period) return 0;

            decimal multiplier = 2.0m / (period + 1);
            decimal ema = prices.Take(period).Average();

            for (int i = period; i < prices.Count; i++)
            {
                ema = (prices[i] - ema) * multiplier + ema;
            }
            return ema;
        }
    }
}
