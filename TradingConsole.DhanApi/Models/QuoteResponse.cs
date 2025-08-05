using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class QuoteResponse
    {
        [JsonPropertyName("securityId")]
        public string? SecurityId { get; set; }

        [JsonPropertyName("ltp")]
        public decimal Ltp { get; set; }

        [JsonPropertyName("prev_close")]
        public decimal PreviousClose { get; set; }
    }
}
