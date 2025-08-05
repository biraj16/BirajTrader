using System;
using System.Collections.Generic;

namespace TradingConsole.Core.Models
{
    /// <summary>
    /// Represents a single day's worth of IV data for a specific instrument type.
    /// </summary>
    public class DailyIvRecord
    {
        public DateTime Date { get; set; }
        public decimal HighIv { get; set; }
        public decimal LowIv { get; set; }
    }

    /// <summary>
    /// The main class that will be serialized to JSON, holding all historical IV records.
    /// The dictionary key will be a string like "NIFTY_ATM_CE".
    /// </summary>
    public class HistoricalIvDatabase
    {
        public Dictionary<string, List<DailyIvRecord>> Records { get; set; } = new Dictionary<string, List<DailyIvRecord>>();
    }
}
