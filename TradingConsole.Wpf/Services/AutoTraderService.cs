// TradingConsole.Wpf/Services/AutoTraderService.cs

using System;
using System.Linq;
using System.Threading.Tasks;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi;
using TradingConsole.DhanApi.Models;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Manages the automated trading execution based on signals from the AnalysisService.
    /// This service is designed to be self-contained and robust for live trading.
    /// </summary>
    public class AutoTraderService
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ScripMasterService _scripMasterService;
        private readonly DhanApiClient _apiClient;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly NotificationService _notificationService;
        private string? _activeTradeInstrument;

        public AutoTraderService(
            MainViewModel mainViewModel,
            ScripMasterService scripMasterService,
            DhanApiClient apiClient,
            SettingsViewModel settingsViewModel,
            NotificationService notificationService)
        {
            _mainViewModel = mainViewModel;
            _scripMasterService = scripMasterService;
            _apiClient = apiClient;
            _settingsViewModel = settingsViewModel;
            _notificationService = notificationService;
        }

        /// <summary>
        /// This is the entry point for the service, called when a new analysis result is available.
        /// </summary>
        public async void OnAnalysisResultReceived(AnalysisResult result)
        {
            var settings = _settingsViewModel.AutomationSettings;

            // Safety Checks: Ensure automation is enabled and conditions are met.
            if (!settings.IsAutomationEnabled ||
                result.Symbol != settings.SelectedAutoTradeIndex ||
                result.InstrumentGroup != "INDEX" ||
                _mainViewModel.IsKillSwitchActive ||
                _activeTradeInstrument != null) // Do not take a new trade if one is already active.
            {
                return;
            }

            // Check if the signal has crossed the conviction threshold.
            bool isBuySignal = result.PrimarySignal == "Bullish" && result.ConvictionScore >= settings.MinConvictionScore;
            bool isSellSignal = result.PrimarySignal == "Bearish" && result.ConvictionScore <= -settings.MinConvictionScore;

            if (isBuySignal || isSellSignal)
            {
                await ExecuteTradeAsync(result, isBuySignal);
            }
            else if (result.PrimarySignal == "Neutral")
            {
                // Logic to handle exit signals can be added here if not using bracket orders.
                // For now, bracket orders handle exits automatically.
            }
        }

        private async Task ExecuteTradeAsync(AnalysisResult result, bool isBuy)
        {
            var settings = _settingsViewModel.AutomationSettings;
            _activeTradeInstrument = result.Symbol; // Mark that a trade is being attempted.

            try
            {
                await _mainViewModel.UpdateStatusAsync($"[AutoTrader] {result.PrimarySignal} signal received for {result.Symbol} with score {result.ConvictionScore}. Preparing trade...");

                // 1. Find the underlying index instrument to get its LTP.
                var underlyingInstrument = _mainViewModel.Dashboard.MonitoredInstruments.FirstOrDefault(i => i.DisplayName == result.Symbol);
                if (underlyingInstrument == null) throw new InvalidOperationException("Could not find underlying instrument in dashboard.");

                // --- THE FIX ---
                // 2. Programmatically determine the nearest expiry date instead of relying on UI.
                if (!_mainViewModel.TryGetNearestExpiry(underlyingInstrument.Symbol, out var expiryDate))
                {
                    throw new InvalidOperationException($"Could not determine nearest expiry for {underlyingInstrument.Symbol}.");
                }

                // 3. Determine the ATM strike price.
                int strikeStep = GetStrikePriceStep(underlyingInstrument.Symbol);
                decimal atmStrike = Math.Round(underlyingInstrument.LTP / strikeStep) * strikeStep;

                // 4. Find the correct option contract.
                var optionScrip = _scripMasterService.FindOptionScripInfo(
                    underlyingInstrument.UnderlyingSymbol,
                    expiryDate,
                    atmStrike,
                    isBuy ? "CE" : "PE");

                if (optionScrip == null || string.IsNullOrEmpty(optionScrip.SecurityId))
                {
                    throw new InvalidOperationException($"Could not find ATM {(isBuy ? "Call" : "Put")} option for strike {atmStrike}.");
                }

                // 5. Get the live price of the option to calculate SL/Target.
                var optionQuote = await _apiClient.GetQuoteAsync(optionScrip.SecurityId);
                if (optionQuote == null) throw new InvalidOperationException("Could not fetch live quote for the selected option.");
                decimal optionLtp = optionQuote.Ltp;

                // 6. Construct the Bracket Order (SuperOrder).
                var orderRequest = new SuperOrderRequest
                {
                    DhanClientId = _mainViewModel.DhanClientId,
                    TransactionType = "BUY", // We are always buying options.
                    ExchangeSegment = optionScrip.Segment,
                    ProductType = "INTRADAY",
                    OrderType = "MARKET", // Entry at market price for speed.
                    SecurityId = optionScrip.SecurityId,
                    Quantity = settings.LotsPerTrade * optionScrip.LotSize,
                    StopLossPrice = optionLtp - settings.StopLossPoints,
                    TargetPrice = optionLtp + settings.TargetPoints,
                    TrailingJump = settings.IsTrailingEnabled ? (decimal?)settings.TrailingStopLossJump : null
                };

                // Final safety check on calculated prices.
                if (orderRequest.StopLossPrice <= 0)
                {
                    throw new InvalidOperationException($"Calculated Stop Loss Price ({orderRequest.StopLossPrice}) is not valid.");
                }

                await _mainViewModel.UpdateStatusAsync($"[AutoTrader] Placing Bracket Order for {optionScrip.SemInstrumentName} at Market...");

                // 7. Place the order.
                var response = await _apiClient.PlaceSuperOrderAsync(orderRequest);

                if (response?.OrderId != null)
                {
                    string successMessage = $"[AutoTrader] SUCCESS: Bracket Order placed for {optionScrip.SemInstrumentName}. Order ID: {response.OrderId}";
                    await _mainViewModel.UpdateStatusAsync(successMessage);
                    await _notificationService.SendTelegramMessageAsync(successMessage);
                }
                else
                {
                    throw new DhanApiException("Order placement failed. Response did not contain an Order ID.");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"[AutoTrader] FAILED: {ex.Message}";
                await _mainViewModel.UpdateStatusAsync(errorMessage);
                await _notificationService.SendTelegramMessageAsync(errorMessage);
                _activeTradeInstrument = null; // Reset the lock on failure.
            }
        }

        private int GetStrikePriceStep(string underlyingSymbol)
        {
            string upperSymbol = underlyingSymbol.ToUpperInvariant();
            if (upperSymbol.Contains("SENSEX") || upperSymbol.Contains("BANKNIFTY"))
            {
                return 100;
            }
            return 50; // Default for NIFTY
        }
    }
}