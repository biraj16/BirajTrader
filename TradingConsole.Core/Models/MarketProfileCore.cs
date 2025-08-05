// In TradingConsole.Core/Models/MarketProfileCore.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradingConsole.Core.Models
{
    /// <summary>
    /// Represents the key levels derived from volume profile analysis.
    /// </summary>
    public class VolumeProfileInfo
    {
        public decimal VolumePoc { get; set; }
    }

    /// <summary>
    /// Represents the key levels derived from TPO (Time Price Opportunity) analysis.
    /// </summary>
    public class TpoInfo
    {
        public decimal PointOfControl { get; set; }
        public decimal ValueAreaHigh { get; set; }
        public decimal ValueAreaLow { get; set; }
    }

    /// <summary>
    /// A storable, serializable representation of a single day's market profile.
    /// </summary>
    public class MarketProfileData
    {
        public DateTime Date { get; set; }
        public TpoInfo TpoLevelsInfo { get; set; } = new TpoInfo();
        public VolumeProfileInfo VolumeProfileInfo { get; set; } = new VolumeProfileInfo();

        // These are made nullable and will be ignored by the JSON serializer if null,
        // which is key to our new pruning strategy.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<decimal, int>? TpoCounts { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<decimal, long>? VolumeLevels { get; set; }
    }
}
