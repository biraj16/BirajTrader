using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class PositionResponse
    {
        [JsonPropertyName("securityId")]
        public string? SecurityId { get; set; }

        [JsonPropertyName("tradingSymbol")]
        public string? TradingSymbol { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("productType")]
        public string? ProductType { get; set; }

        [JsonPropertyName("positionType")]
        public string? PositionType { get; set; }

        [JsonPropertyName("netQty")]
        public int NetQuantity { get; set; }

        [JsonPropertyName("buyAvg")]
        public decimal BuyAverage { get; set; }

        [JsonPropertyName("sellAvg")]
        public decimal SellAverage { get; set; }

        [JsonPropertyName("buyQty")]
        public int BuyQuantity { get; set; }

        [JsonPropertyName("sellQty")]
        public int SellQuantity { get; set; }

        [JsonPropertyName("costPrice")]
        public decimal CostPrice { get; set; }

        [JsonPropertyName("ltp")]
        public decimal LastTradedPrice { get; set; }

        [JsonPropertyName("unrealizedProfit")]
        public decimal UnrealizedProfit { get; set; }

        [JsonPropertyName("realizedProfit")]
        public decimal RealizedProfit { get; set; }
    }
}
