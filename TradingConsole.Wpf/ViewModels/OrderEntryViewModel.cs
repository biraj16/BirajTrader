using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi;
using TradingConsole.DhanApi.Models;
using TradingConsole.DhanApi.Models.WebSocket;

namespace TradingConsole.Wpf.ViewModels
{
    public class OrderEntryViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Constants
        private const string OrderTypeLimit = "LIMIT";
        private const string OrderTypeMarket = "MARKET";
        private const string OrderTypeStopLoss = "STOP_LOSS";
        private const string OrderTypeBracket = "BRACKET";
        private const string OrderTypeBracketMarket = "BRACKET_MARKET";
        private const string OrderTypeCover = "COVER";
        private const string OrderTypeCO_Api = "CO";

        private const string ProductTypeIntraday = "INTRADAY";
        private const string ProductTypeMargin = "MARGIN";
        private const string ProductTypeCnc = "CNC";

        private const string TransactionTypeBuy = "BUY";
        private const string TransactionTypeSell = "SELL";
        #endregion

        #region Private Fields
        private readonly DhanApiClient _apiClient;
        private readonly DhanWebSocketClient _webSocketClient;
        private readonly ScripMasterService _scripMasterService;
        private readonly string _securityId;
        private readonly string _dhanClientId;
        private readonly int _lotSize;
        private readonly string _exchangeSegment;
        private readonly bool _isModification = false;
        private readonly string? _orderId;
        private readonly int _freezeLimit;
        // --- ADDED: Field to store the Kill Switch state ---
        private readonly bool _isKillSwitchActive;
        #endregion

        #region Public Properties
        public string InstrumentName { get; }
        public bool IsBuyOrder { get; }
        public string TransactionType => IsBuyOrder ? TransactionTypeBuy : TransactionTypeSell;
        public string WindowTitle => _isModification ? "Modify Order" : "Place Order";

        private decimal _liveLtp;
        public decimal LiveLtp { get => _liveLtp; set { _liveLtp = value; OnPropertyChanged(nameof(LiveLtp)); OnPropertyChanged(nameof(LiveLtpChange)); OnPropertyChanged(nameof(LiveLtpChangePercent)); } }
        public decimal PreviousClose { get; }
        public decimal LiveLtpChange => LiveLtp - PreviousClose;
        public decimal LiveLtpChangePercent => PreviousClose == 0 ? 0 : (LiveLtpChange / PreviousClose);


        private int _quantity = 1;
        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(nameof(Quantity)); OnPropertyChanged(nameof(TotalQuantity)); } }
        public int TotalQuantity => Quantity * _lotSize;

        private decimal _price;
        public decimal Price { get => _price; set { _price = value; OnPropertyChanged(nameof(Price)); } }

        private decimal _triggerPrice;
        public decimal TriggerPrice { get => _triggerPrice; set { _triggerPrice = value; OnPropertyChanged(nameof(TriggerPrice)); } }

        private decimal _targetPrice;
        public decimal TargetPrice { get => _targetPrice; set { _targetPrice = value; OnPropertyChanged(nameof(TargetPrice)); } }

        private decimal _stopLossPrice;
        public decimal StopLossPrice { get => _stopLossPrice; set { _stopLossPrice = value; OnPropertyChanged(nameof(StopLossPrice)); } }

        private bool _isTrailingStopLossEnabled;
        public bool IsTrailingStopLossEnabled { get => _isTrailingStopLossEnabled; set { _isTrailingStopLossEnabled = value; OnPropertyChanged(nameof(IsTrailingStopLossEnabled)); } }

        private decimal _trailingStopLossValue = 1;
        public decimal TrailingStopLossValue { get => _trailingStopLossValue; set { _trailingStopLossValue = value; OnPropertyChanged(nameof(TrailingStopLossValue)); } }

        private bool _isSliceOrderEnabled;
        public bool IsSliceOrderEnabled
        {
            get => _isSliceOrderEnabled;
            set
            {
                _isSliceOrderEnabled = value;
                OnPropertyChanged(nameof(IsSliceOrderEnabled));
                OnPropertyChanged(nameof(IsSliceOrderVisible));
            }
        }

        private int _sliceQuantity = 1;
        public int SliceQuantity { get => _sliceQuantity; set { _sliceQuantity = value; OnPropertyChanged(nameof(SliceQuantity)); } }

        private int _interval = 1;
        public int Interval { get => _interval; set { _interval = value; OnPropertyChanged(nameof(Interval)); } }

        public List<string> OrderTypes { get; } = new List<string> { OrderTypeLimit, OrderTypeMarket, OrderTypeStopLoss, OrderTypeBracket, OrderTypeBracketMarket, OrderTypeCover };
        private string _selectedOrderType = OrderTypeLimit;
        public string SelectedOrderType
        {
            get => _selectedOrderType;
            set
            {
                if (_selectedOrderType != value)
                {
                    _selectedOrderType = value;
                    OnPropertyChanged(nameof(SelectedOrderType));
                    OnPropertyChanged(nameof(IsLimitPriceVisible));
                    OnPropertyChanged(nameof(IsTriggerPriceVisible));
                    OnPropertyChanged(nameof(IsBracketOrderVisible));
                    OnPropertyChanged(nameof(IsProductTypeSelectionEnabled));
                    OnPropertyChanged(nameof(CanEnableSlicing));

                    if (!CanEnableSlicing)
                    {
                        IsSliceOrderEnabled = false;
                    }
                }
            }
        }

        public bool IsLimitPriceVisible => _selectedOrderType == OrderTypeLimit || _selectedOrderType == OrderTypeStopLoss || _selectedOrderType == OrderTypeBracket;
        public bool IsTriggerPriceVisible => _selectedOrderType == OrderTypeStopLoss || _selectedOrderType == OrderTypeCover;
        public bool IsBracketOrderVisible => _selectedOrderType == OrderTypeBracket || _selectedOrderType == OrderTypeBracketMarket;

        public bool IsSliceOrderVisible => IsSliceOrderEnabled;

        public bool CanEnableSlicing => SelectedOrderType == OrderTypeLimit || SelectedOrderType == OrderTypeMarket;

        public bool IsProductTypeSelectionEnabled => true;

        public List<string> ProductTypes { get; } = new List<string> { ProductTypeIntraday, ProductTypeMargin, ProductTypeCnc };
        private string _selectedProductType = ProductTypeIntraday;
        public string SelectedProductType
        {
            get => _selectedProductType;
            set { _selectedProductType = value; OnPropertyChanged(nameof(SelectedProductType)); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); } }

        public ICommand PlaceOrderCommand { get; }
        #endregion

        #region Constructors
        // --- MODIFIED: Constructor now accepts the killSwitchState ---
        public OrderEntryViewModel(string securityId, string instrumentName, string exchangeSegment, bool isBuy, decimal initialPrice, decimal previousClose, int lotSize, string productType, DhanApiClient apiClient, DhanWebSocketClient webSocketClient, string dhanClientId, ScripMasterService scripMasterService, int freezeLimit, bool isKillSwitchActive, OrderBookEntry? existingOrder = null)
        {
            _apiClient = apiClient;
            _webSocketClient = webSocketClient;
            _dhanClientId = dhanClientId;
            _scripMasterService = scripMasterService;

            _securityId = securityId;
            InstrumentName = instrumentName;
            _exchangeSegment = exchangeSegment;
            IsBuyOrder = isBuy;
            Price = initialPrice;
            TriggerPrice = initialPrice;
            _lotSize = lotSize;
            SelectedProductType = productType;
            _freezeLimit = freezeLimit;
            _isKillSwitchActive = isKillSwitchActive; // --- ADDED: Store the state ---

            LiveLtp = initialPrice;
            PreviousClose = previousClose;

            if (existingOrder != null)
            {
                _isModification = true;
                _orderId = existingOrder.OrderId;
                SelectedOrderType = existingOrder.OrderType;
                if (_lotSize > 0) { Quantity = existingOrder.Quantity / _lotSize; }
            }

            PlaceOrderCommand = new RelayCommand(async (p) => await ExecutePlaceOrModifyOrder(), (p) => CanPlaceOrder());

            _webSocketClient.OnQuoteUpdate += OnQuoteUpdateReceived;
            var subscription = new Dictionary<string, int> { { _securityId, _scripMasterService.GetSegmentIdFromName(_exchangeSegment) } };
            Task.Run(() => _webSocketClient.SubscribeToInstrumentsAsync(subscription, 17));
        }
        #endregion

        #region Command Execution
        private bool CanPlaceOrder()
        {
            // --- MODIFIED: Check the kill switch state first ---
            if (_isKillSwitchActive)
            {
                StatusMessage = "Kill Switch is active. Trading is disabled.";
                return false;
            }
            return !string.IsNullOrEmpty(_securityId) && _lotSize > 0;
        }

        private async Task ExecutePlaceOrModifyOrder()
        {
            try
            {
                if (_isModification)
                {
                    if (IsBracketOrderVisible) await ExecuteModifySuperOrder();
                    else await ExecuteModifyOrder();
                    return;
                }

                if (IsBracketOrderVisible && TotalQuantity > _freezeLimit)
                {
                    await ExecuteChunkedSuperOrder();
                }
                else if (IsSliceOrderEnabled)
                {
                    await ExecutePlaceSliceOrder();
                }
                else if (IsBracketOrderVisible)
                {
                    await ExecutePlaceSuperOrder();
                }
                else if (SelectedOrderType == OrderTypeCover)
                {
                    await ExecutePlaceCoverOrder();
                }
                else
                {
                    await ExecutePlaceOrder();
                }
            }
            catch (DhanApiException ex)
            {
                StatusMessage = $"Order Failed: {ex.Message}";
                MessageBox.Show(StatusMessage, "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusMessage = $"An unexpected error occurred: {ex.Message}";
                MessageBox.Show(StatusMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteChunkedSuperOrder()
        {
            int remainingQuantity = TotalQuantity;
            int chunkNumber = 1;
            int totalChunks = (int)Math.Ceiling((double)TotalQuantity / _freezeLimit);

            while (remainingQuantity > 0)
            {
                int chunkQuantity = Math.Min(remainingQuantity, _freezeLimit);

                await UpdateStatusAsync($"Placing chunk {chunkNumber} of {totalChunks} ({chunkQuantity} qty)...");

                var orderRequest = new SuperOrderRequest
                {
                    DhanClientId = _dhanClientId,
                    TransactionType = this.TransactionType,
                    ExchangeSegment = _exchangeSegment,
                    ProductType = this.SelectedProductType,
                    OrderType = SelectedOrderType == OrderTypeBracketMarket ? OrderTypeMarket : OrderTypeLimit,
                    SecurityId = this._securityId,
                    Quantity = chunkQuantity,
                    Price = SelectedOrderType == OrderTypeBracketMarket ? 0 : this.Price,
                    TargetPrice = this.TargetPrice,
                    StopLossPrice = this.StopLossPrice,
                    TrailingJump = IsTrailingStopLossEnabled ? (decimal?)TrailingStopLossValue : null
                };

                try
                {
                    var response = await _apiClient.PlaceSuperOrderAsync(orderRequest);
                    if (response?.OrderId == null)
                    {
                        throw new DhanApiException($"Failed to place chunk {chunkNumber}.");
                    }
                    await UpdateStatusAsync($"Chunk {chunkNumber} placed successfully. ID: {response.OrderId}");
                    remainingQuantity -= chunkQuantity;
                    chunkNumber++;
                    await Task.Delay(300);
                }
                catch (DhanApiException ex)
                {
                    StatusMessage = $"Error on chunk {chunkNumber}: {ex.Message}. Halting process.";
                    MessageBox.Show(StatusMessage, "Chunking Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            StatusMessage = $"All {totalChunks} bracket order chunks placed successfully.";
            MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ExecutePlaceSliceOrder()
        {
            if (this.SliceQuantity * this._lotSize > this.TotalQuantity)
            {
                StatusMessage = "Slice quantity cannot be greater than the total quantity.";
                MessageBox.Show(StatusMessage, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (this.SliceQuantity <= 0)
            {
                StatusMessage = "Slice quantity must be a positive value.";
                MessageBox.Show(StatusMessage, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusMessage = "Placing Slice Order...";

            var sliceRequest = new SliceOrderRequest
            {
                DhanClientId = _dhanClientId,
                TransactionType = this.TransactionType,
                ExchangeSegment = _exchangeSegment,
                ProductType = this.SelectedProductType,
                OrderType = this.SelectedOrderType,
                SecurityId = this._securityId,
                TotalQuantity = this.TotalQuantity,
                SliceQuantity = this.SliceQuantity * this._lotSize,
                Interval = this.Interval,
                Price = this.SelectedOrderType == OrderTypeLimit ? this.Price : 0
            };

            var response = await _apiClient.PlaceSliceOrderAsync(sliceRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Slice Order Placed Successfully! Main Order ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ExecutePlaceCoverOrder()
        {
            StatusMessage = "Placing Cover Order...";

            var orderRequest = new OrderRequest
            {
                DhanClientId = _dhanClientId,
                TransactionType = this.TransactionType,
                ExchangeSegment = _exchangeSegment,
                ProductType = ProductTypeIntraday,
                OrderType = OrderTypeCO_Api,
                SecurityId = this._securityId,
                Quantity = this.TotalQuantity,
                Price = 0,
                TriggerPrice = this.TriggerPrice
            };

            var response = await _apiClient.PlaceOrderAsync(orderRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Cover Order Placed Successfully! ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ExecutePlaceSuperOrder()
        {
            decimal entryPrice = (SelectedOrderType == OrderTypeBracket && Price > 0) ? Price : 0;

            if (SelectedOrderType == OrderTypeBracket && entryPrice <= 0)
            {
                MessageBox.Show("Please enter a valid price greater than 0 for a LIMIT Bracket Order.", "Invalid Price", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusMessage = "Placing Super Order...";

            var orderRequest = new SuperOrderRequest
            {
                DhanClientId = _dhanClientId,
                TransactionType = this.TransactionType,
                ExchangeSegment = _exchangeSegment,
                ProductType = this.SelectedProductType,
                OrderType = SelectedOrderType == OrderTypeBracketMarket ? OrderTypeMarket : OrderTypeLimit,
                SecurityId = this._securityId,
                Quantity = this.TotalQuantity,
                Price = SelectedOrderType == OrderTypeBracketMarket ? 0 : this.Price,
                TargetPrice = this.TargetPrice,
                StopLossPrice = this.StopLossPrice,
                TrailingJump = IsTrailingStopLossEnabled ? (decimal?)TrailingStopLossValue : null
            };

            var response = await _apiClient.PlaceSuperOrderAsync(orderRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Super Order Placed Successfully! ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ExecuteModifySuperOrder()
        {
            if (string.IsNullOrEmpty(_orderId)) return;

            StatusMessage = "Modifying Super Order...";
            var modifyRequest = new ModifySuperOrderRequest
            {
                DhanClientId = _dhanClientId,
                OrderId = _orderId,
                Quantity = this.TotalQuantity,
                Price = this.Price,
                TargetPrice = this.TargetPrice,
                StopLossPrice = this.StopLossPrice,
                TrailingJump = IsTrailingStopLossEnabled ? (decimal?)TrailingStopLossValue : null,
            };
            var response = await _apiClient.ModifySuperOrderAsync(modifyRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Super Order Modified Successfully! ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ExecutePlaceOrder()
        {
            StatusMessage = "Placing order...";
            var orderRequest = new OrderRequest
            {
                DhanClientId = _dhanClientId,
                TransactionType = this.TransactionType,
                ExchangeSegment = _exchangeSegment,
                ProductType = this.SelectedProductType,
                OrderType = this.SelectedOrderType,
                SecurityId = this._securityId,
                Quantity = this.TotalQuantity,
                Price = (SelectedOrderType == OrderTypeLimit || SelectedOrderType == OrderTypeStopLoss) ? this.Price : 0,
                TriggerPrice = (SelectedOrderType == OrderTypeStopLoss) ? this.TriggerPrice : 0
            };
            var response = await _apiClient.PlaceOrderAsync(orderRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Order Placed Successfully! ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ExecuteModifyOrder()
        {
            if (string.IsNullOrEmpty(_orderId)) return;
            StatusMessage = "Modifying order...";
            var modifyRequest = new ModifyOrderRequest
            {
                DhanClientId = _dhanClientId,
                OrderId = _orderId,
                OrderType = this.SelectedOrderType,
                Quantity = this.TotalQuantity,
                Price = this.Price,
                TriggerPrice = this.TriggerPrice,
                Validity = "DAY"
            };
            var response = await _apiClient.ModifyOrderAsync(modifyRequest);
            if (response?.OrderId != null)
            {
                StatusMessage = $"Order Modified Successfully! ID: {response.OrderId}";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private Task UpdateStatusAsync(string message)
        {
            return Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = message;
                OnPropertyChanged(nameof(StatusMessage));
            }).Task;
        }
        #endregion

        private void OnQuoteUpdateReceived(QuotePacket packet)
        {
            if (packet.SecurityId == _securityId)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    LiveLtp = packet.LastPrice;
                });
            }
        }

        #region IDisposable Implementation
        public void Dispose()
        {
            if (_webSocketClient != null)
            {
                _webSocketClient.OnQuoteUpdate -= OnQuoteUpdateReceived;
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
