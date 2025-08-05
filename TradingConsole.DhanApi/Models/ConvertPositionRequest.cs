using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class ConvertPositionRequest
    {
        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("securityId")]
        public string SecurityId { get; set; } = string.Empty;

        [JsonPropertyName("productType")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("convertTo")]
        public string ConvertTo { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }
}
