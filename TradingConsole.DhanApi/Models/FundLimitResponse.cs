using System.Text.Json.Serialization;

namespace TradingConsole.DhanApi.Models
{
    public class FundLimitResponse
    {
        [JsonPropertyName("availabelBalance")]
        public decimal AvailableBalance { get; set; }

        [JsonPropertyName("utilizedAmount")]
        public decimal UtilizedAmount { get; set; }

        [JsonPropertyName("collateralAmount")]
        public decimal CollateralAmount { get; set; }

        [JsonPropertyName("withdrawableBalance")]
        public decimal WithdrawableBalance { get; set; }
    }
}
