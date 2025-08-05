using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class SuperOrderRequest
    {
        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("correlationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = string.Empty;

        [JsonPropertyName("exchangeSegment")]
        public string ExchangeSegment { get; set; } = string.Empty;

        [JsonPropertyName("productType")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("orderType")]
        public string OrderType { get; set; } = string.Empty;

        [JsonPropertyName("validity")]
        public string Validity { get; set; } = "DAY";

        [JsonPropertyName("securityId")]
        public string SecurityId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Price { get; set; }

        [JsonPropertyName("targetPrice")]
        public decimal TargetPrice { get; set; }

        [JsonPropertyName("stopLossPrice")]
        public decimal StopLossPrice { get; set; }

        [JsonPropertyName("trailingJump")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? TrailingJump { get; set; }
    }

    public class ModifySuperOrderRequest
    {
        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("targetPrice")]
        public decimal TargetPrice { get; set; }

        [JsonPropertyName("stopLossPrice")]
        public decimal StopLossPrice { get; set; }

        [JsonPropertyName("trailingJump")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? TrailingJump { get; set; }
    }
}
