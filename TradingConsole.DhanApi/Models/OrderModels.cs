using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class OrderRequest
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

        [JsonPropertyName("triggerPrice")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? TriggerPrice { get; set; }
    }

    public class SliceOrderRequest
    {
        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = string.Empty;

        [JsonPropertyName("exchangeSegment")]
        public string ExchangeSegment { get; set; } = string.Empty;

        [JsonPropertyName("productType")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("orderType")]
        public string OrderType { get; set; } = string.Empty;

        [JsonPropertyName("securityId")]
        public string SecurityId { get; set; } = string.Empty;

        [JsonPropertyName("totalQuantity")]
        public int TotalQuantity { get; set; }

        [JsonPropertyName("sliceQuantity")]
        public int SliceQuantity { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }

        [JsonPropertyName("price")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Price { get; set; }
    }

    public class ModifyOrderRequest
    {
        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("orderType")]
        public string OrderType { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("triggerPrice")]
        public decimal TriggerPrice { get; set; }

        [JsonPropertyName("validity")]
        public string Validity { get; set; } = "DAY";
    }

    public class OrderResponse
    {
        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        [JsonPropertyName("orderStatus")]
        public string? OrderStatus { get; set; }
    }

    public class OrderBookEntry : INotifyPropertyChanged
    {
        private string _orderStatus = string.Empty;
        private int _filledQuantity;

        [JsonPropertyName("dhanClientId")]
        public string DhanClientId { get; set; } = string.Empty;

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("exchangeSegment")]
        public string ExchangeSegment { get; set; } = string.Empty;

        [JsonPropertyName("productType")]
        public string ProductType { get; set; } = string.Empty;

        [JsonPropertyName("orderType")]
        public string OrderType { get; set; } = string.Empty;

        [JsonPropertyName("orderStatus")]
        public string OrderStatus
        {
            get => _orderStatus;
            set { _orderStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPending)); }
        }

        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = string.Empty;

        [JsonPropertyName("securityId")]
        public string SecurityId { get; set; } = string.Empty;

        [JsonPropertyName("tradingSymbol")]
        public string TradingSymbol { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("filledQty")]
        public int FilledQuantity
        {
            get => _filledQuantity;
            set { _filledQuantity = value; OnPropertyChanged(); }
        }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("triggerPrice")]
        public decimal TriggerPrice { get; set; }

        [JsonPropertyName("averageTradedPrice")]
        public decimal AverageTradedPrice { get; set; }

        [JsonPropertyName("createTime")]
        public string CreateTime { get; set; } = string.Empty;

        [JsonPropertyName("updateTime")]
        public string UpdateTime { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsPending => OrderStatus == "PENDING" || OrderStatus == "TRIGGER_PENDING" || OrderStatus == "AMO_RECEIVED";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
