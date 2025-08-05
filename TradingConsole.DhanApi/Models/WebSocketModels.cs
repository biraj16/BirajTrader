namespace TradingConsole.DhanApi.Models.WebSocket
{
    public class MarketFeedHeader
    {
        public byte FeedCode { get; set; }
        public int MessageLength { get; set; }
        public int Timestamp { get; set; }
        public string? SecurityId { get; set; }
        public byte ExchangeSegment { get; set; }
    }

    public class TickerPacket
    {
        public string? SecurityId { get; set; }
        public decimal LastPrice { get; set; }
        public int LastTradeTime { get; set; }
    }

    public class QuotePacket
    {
        public string? SecurityId { get; set; }
        public decimal LastPrice { get; set; }
        public int LastTradeQuantity { get; set; }
        public int LastTradeTime { get; set; }
        public decimal AvgTradePrice { get; set; }
        public long Volume { get; set; }
        public long TotalSellQuantity { get; set; }
        public long TotalBuyQuantity { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
    }

    public class PreviousClosePacket
    {
        public string? SecurityId { get; set; }
        public decimal PreviousClose { get; set; }
    }
}
