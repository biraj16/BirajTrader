namespace TradingConsole.DhanApi.Models.WebSocket
{
    public class OiPacket
    {
        public string? SecurityId { get; set; }
        public int OpenInterest { get; set; }
    }
}
