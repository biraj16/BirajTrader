// In TradingConsole.Core/Models/HistoricalMarketProfile.cs
using System.Collections.Generic;

namespace TradingConsole.Core.Models
{
    /// <summary>
    /// Represents the database of historical market profile data for all instruments,
    /// designed to be serialized to a file. The dictionary key is the instrument's SecurityId.
    /// </summary>
    public class HistoricalMarketProfileDatabase
    {
        public Dictionary<string, List<MarketProfileData>> Records { get; set; } = new Dictionary<string, List<MarketProfileData>>();
    }
}
