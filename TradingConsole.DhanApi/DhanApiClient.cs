// TradingConsole.DhanApi/DhanApiClient.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TradingConsole.DhanApi.Models;

namespace TradingConsole.DhanApi
{
    public class DhanApiClient
    {
        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri("https://api.dhan.co") };
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _clientId; // Store Client ID

        private readonly SemaphoreSlim _orderApiSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _optionChainApiSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _generalApiSemaphore = new SemaphoreSlim(1, 1);

        // REMOVED: Payload class is no longer needed as the parameter is in the URL.

        private class OptionChainRequestPayload
        {
            [JsonPropertyName("UnderlyingScrip")]
            public int UnderlyingScrip { get; set; }

            [JsonPropertyName("UnderlyingSeg")]
            public string UnderlyingSeg { get; set; } = string.Empty;

            [JsonPropertyName("Expiry")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Expiry { get; set; }
        }

        private class IntradayDataRequest
        {
            [JsonPropertyName("securityId")]
            public string SecurityId { get; set; } = string.Empty;

            [JsonPropertyName("exchangeSegment")]
            public string ExchangeSegment { get; set; } = string.Empty;

            [JsonPropertyName("instrument")]
            public string Instrument { get; set; } = string.Empty;

            [JsonPropertyName("interval")]
            public string Interval { get; set; } = "1";

            [JsonPropertyName("oi")]
            public bool Oi { get; set; } = true;

            [JsonPropertyName("fromDate")]
            public string FromDate { get; set; } = string.Empty;

            [JsonPropertyName("toDate")]
            public string ToDate { get; set; } = string.Empty;
        }

        public class HistoricalDataPoints
        {
            [JsonPropertyName("open")]
            public List<decimal> Open { get; set; } = new List<decimal>();

            [JsonPropertyName("high")]
            public List<decimal> High { get; set; } = new List<decimal>();

            [JsonPropertyName("low")]
            public List<decimal> Low { get; set; } = new List<decimal>();

            [JsonPropertyName("close")]
            public List<decimal> Close { get; set; } = new List<decimal>();

            [JsonPropertyName("volume")]
            public List<decimal> Volume { get; set; } = new List<decimal>();

            [JsonPropertyName("timestamp")]
            public List<decimal> StartTime { get; set; } = new List<decimal>();

            [JsonPropertyName("open_interest")]
            public List<decimal> OpenInterest { get; set; } = new List<decimal>();
        }


        public DhanApiClient(string clientId, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID cannot be null or empty.", nameof(clientId));
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));

            _clientId = clientId; // Store the client ID

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("access-token", accessToken);

            // --- FIX: Added the missing client-id header ---
            _httpClient.DefaultRequestHeaders.Add("client-id", clientId);

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        private async Task<T?> HandleResponse<T>(HttpResponseMessage response, string apiName) where T : class
        {
            string responseBody = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                if (apiName == "GetIntradayHistoricalData")
                {
                    Debug.WriteLine($"[DEBUG_HISTORICAL] Raw JSON Response for {apiName}: {responseBody}");
                }
                Debug.WriteLine($"SUCCESS ({apiName}): {response.StatusCode}");
                return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions);
            }
            else
            {
                Debug.WriteLine($"ERROR ({apiName}): {response.StatusCode} - {responseBody}");
                throw new DhanApiException($"API call '{apiName}' failed with status {response.StatusCode}: {responseBody}");
            }
        }

        private async Task<T?> ExecuteApiCall<T>(SemaphoreSlim semaphore, Func<Task<HttpResponseMessage>> apiCallFunc, string apiName, int delayMs = 250) where T : class
        {
            await semaphore.WaitAsync();
            try
            {
                var response = await apiCallFunc();
                if (delayMs > 0) await Task.Delay(delayMs);
                return await HandleResponse<T>(response, apiName);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is DhanApiException)
            {
                Debug.WriteLine($"[DhanApiClient] Exception in {apiName}: {ex.Message}");
                throw new DhanApiException($"Error in {apiName}. See inner exception for details.", ex);
            }
            finally
            {
                semaphore.Release();
            }
        }

        // --- CORRECTED: Method to activate the Kill Switch via Dhan's API ---
        public async Task<bool> ActivateKillSwitchAsync()
        {
            await _generalApiSemaphore.WaitAsync();
            try
            {
                Debug.WriteLine("[DhanApiClient] Sending Kill Switch activation request...");

                // Construct the URL with the query parameter as per the sandbox test
                var requestUrl = $"/v2/killswitch?killSwitchStatus=ACTIVATE";

                // Send a POST request with null content, as the data is in the URL
                var response = await _httpClient.PostAsync(requestUrl, null);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[DhanApiClient] Kill Switch API Error: {response.StatusCode} - {errorBody}");
                    throw new DhanApiException($"Failed to activate Kill Switch. API returned {response.StatusCode}: {errorBody}");
                }

                Debug.WriteLine("[DhanApiClient] Kill Switch activated successfully via API.");
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[DhanApiClient] Kill Switch Network Error: {ex.Message}");
                throw new DhanApiException("Network error while activating Kill Switch.", ex);
            }
            finally
            {
                _generalApiSemaphore.Release();
            }
        }

        public async Task<HistoricalDataPoints?> GetIntradayHistoricalDataAsync(ScripInfo scripInfo, string interval = "1", DateTime? customDate = null)
        {
            DateTime dateToRequest = customDate ?? DateTime.Now;

            Debug.WriteLine($"[DhanApiClient] Requesting intraday data for date: {dateToRequest:yyyy-MM-dd}");

            var fromDate = dateToRequest.ToString("yyyy-MM-dd");
            var toDate = dateToRequest.ToString("yyyy-MM-dd");

            var requestBody = new IntradayDataRequest
            {
                SecurityId = scripInfo.SecurityId,
                ExchangeSegment = scripInfo.Segment,
                Instrument = scripInfo.InstrumentType,
                Interval = interval,
                FromDate = fromDate,
                ToDate = toDate,
                Oi = true
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody, _jsonOptions);
            Debug.WriteLine($"[GetIntradayHistoricalData] Request Payload: {jsonPayload}");
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            return await ExecuteApiCall<HistoricalDataPoints>(_generalApiSemaphore, () => _httpClient.PostAsync("/v2/charts/intraday", content), "GetIntradayHistoricalData");
        }

        public async Task<List<PositionResponse>?> GetPositionsAsync()
        {
            return await ExecuteApiCall<List<PositionResponse>>(_generalApiSemaphore, () => _httpClient.GetAsync("/v2/positions"), "GetPositions");
        }

        public async Task<FundLimitResponse?> GetFundLimitAsync()
        {
            return await ExecuteApiCall<FundLimitResponse>(_generalApiSemaphore, () => _httpClient.GetAsync("/v2/fundlimit"), "GetFundLimit");
        }

        public async Task<ExpiryListResponse?> GetExpiryListAsync(string underlyingScripId, string segment)
        {
            var payload = new OptionChainRequestPayload { UnderlyingScrip = int.Parse(underlyingScripId), UnderlyingSeg = segment };
            string jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<ExpiryListResponse>(_generalApiSemaphore, () => _httpClient.PostAsync("/v2/optionchain/expirylist", content), "GetExpiryList");
        }

        public async Task<OptionChainResponse?> GetOptionChainAsync(string underlyingScripId, string segment, string expiryDate)
        {
            var payload = new OptionChainRequestPayload { UnderlyingScrip = int.Parse(underlyingScripId), UnderlyingSeg = segment, Expiry = expiryDate };
            string jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OptionChainResponse>(_optionChainApiSemaphore, () => _httpClient.PostAsync("/v2/optionchain", content), "GetOptionChain", 3100);
        }

        public async Task<QuoteResponse?> GetQuoteAsync(string securityId)
        {
            if (string.IsNullOrEmpty(securityId)) return null;
            return await ExecuteApiCall<QuoteResponse>(_generalApiSemaphore, () => _httpClient.GetAsync($"/v2/marketdata/quote/{securityId}"), "GetQuote");
        }

        public async Task<OrderResponse?> PlaceOrderAsync(OrderRequest orderRequest)
        {
            var jsonPayload = JsonSerializer.Serialize(orderRequest, _jsonOptions);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.PostAsync("/v2/orders", httpContent), "PlaceOrder");
        }

        public async Task<OrderResponse?> PlaceSliceOrderAsync(SliceOrderRequest sliceRequest)
        {
            var jsonPayload = JsonSerializer.Serialize(sliceRequest, _jsonOptions);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.PostAsync("/v2/orders/slice", httpContent), "PlaceSliceOrder");
        }

        public async Task<List<OrderBookEntry>?> GetOrderBookAsync()
        {
            return await ExecuteApiCall<List<OrderBookEntry>>(_generalApiSemaphore, () => _httpClient.GetAsync("/v2/orders"), "GetOrderBook");
        }

        public async Task<OrderResponse?> ModifyOrderAsync(ModifyOrderRequest modifyRequest)
        {
            var jsonPayload = JsonSerializer.Serialize(modifyRequest, _jsonOptions);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.PutAsync($"/v2/orders/{modifyRequest.OrderId}", httpContent), "ModifyOrder");
        }

        public async Task<OrderResponse?> CancelOrderAsync(string orderId)
        {
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.DeleteAsync($"/v2/orders/{orderId}"), "CancelOrder");
        }

        public async Task<bool> ConvertPositionAsync(ConvertPositionRequest request)
        {
            await _orderApiSemaphore.WaitAsync();
            try
            {
                var jsonPayload = JsonSerializer.Serialize(request, _jsonOptions);
                var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/v2/positions/convert", httpContent);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new DhanApiException($"Failed to convert position. API returned {response.StatusCode}: {errorBody}");
                }
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                throw new DhanApiException("Network error while converting position.", ex);
            }
            finally
            {
                _orderApiSemaphore.Release();
            }
        }

        public async Task<OrderResponse?> PlaceSuperOrderAsync(SuperOrderRequest orderRequest)
        {
            var jsonPayload = JsonSerializer.Serialize(orderRequest, _jsonOptions);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.PostAsync("/v2/super/orders", httpContent), "PlaceSuperOrder");
        }

        public async Task<OrderResponse?> ModifySuperOrderAsync(ModifySuperOrderRequest modifyRequest)
        {
            var jsonPayload = JsonSerializer.Serialize(modifyRequest, _jsonOptions);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.PutAsync($"/v2/super/orders/{modifyRequest.OrderId}", httpContent), "ModifySuperOrder");
        }

        public async Task<OrderResponse?> CancelSuperOrderAsync(string orderId)
        {
            return await ExecuteApiCall<OrderResponse>(_orderApiSemaphore, () => _httpClient.DeleteAsync($"/v2/super/orders/{orderId}"), "CancelSuperOrder");
        }
    }
}
