// In TradingConsole.Wpf/ViewModels/MainViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TradingConsole.Core.Models;
using TradingConsole.DhanApi;
using TradingConsole.DhanApi.Models;
using TradingConsole.DhanApi.Models.WebSocket;
using TradingConsole.Wpf.Services;
using TradingConsole.Wpf.Views;
using static TradingConsole.Wpf.Services.IntradayIvState;
using TickerIndex = TradingConsole.DhanApi.Models.Index;


namespace TradingConsole.Wpf.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Constants
        private const string ProductTypeIntraday = "INTRADAY";
        private const string ProductTypeMargin = "MARGIN";
        private const string OrderTypeMarket = "MARKET";
        private const string TransactionTypeBuy = "BUY";
        private const string TransactionTypeSell = "SELL";
        private const string ValidityDay = "DAY";
        private const string FeedTypeQuote = "Quote";
        private const string FeedTypeTicker = "Ticker";
        #endregion

        #region Private Fields
        private readonly DhanApiClient _apiClient;
        private readonly DhanWebSocketClient _webSocketClient;
        private readonly ScripMasterService _scripMasterService;
        private readonly AnalysisService _analysisService;
        private readonly HistoricalIvService _historicalIvService;
        private readonly MarketProfileService _marketProfileService;
        private readonly IndicatorStateService _indicatorStateService;
        private readonly SignalLoggerService _signalLoggerService;
        private readonly NotificationService _notificationService;
        private readonly PerformanceService _performanceService;
        private readonly AutoTraderService _autoTraderService;
        private readonly string _dhanClientId;
        private Timer? _optionChainRefreshTimer;
        private Timer? _ivRefreshTimer;
        private readonly Dictionary<string, OptionDetails> _optionScripMap = new();
        private readonly HashSet<string> _dashboardOptionsLoadedFor = new();
        private readonly Dictionary<string, string> _nearestExpiryDates = new();
        private bool _isDataLoading = false;
        private readonly SemaphoreSlim _optionChainLoadSemaphore = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, OptionData> _optionChainCache = new();
        private readonly SemaphoreSlim _ivCacheSemaphore = new SemaphoreSlim(1, 1);

        private Dictionary<string, DashboardInstrument> _dashboardInstrumentMap = new();
        private Dictionary<string, Position> _openPositionsMap = new();

        private readonly Timer _uiUpdateTimer;
        private readonly ConcurrentDictionary<string, TickerPacket> _pendingTickerUpdates = new();
        private readonly ConcurrentDictionary<string, QuotePacket> _pendingQuoteUpdates = new();
        private readonly ConcurrentDictionary<string, OiPacket> _pendingOiUpdates = new();
        private readonly ConcurrentDictionary<string, PreviousClosePacket> _pendingPreviousCloseUpdates = new();

        private bool _isKillSwitchActive;
        public bool IsKillSwitchActive
        {
            get => _isKillSwitchActive;
            set
            {
                if (_isKillSwitchActive != value)
                {
                    _isKillSwitchActive = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        #endregion

        #region Public Properties
        public DashboardViewModel Dashboard { get; }
        public PortfolioViewModel Portfolio { get; }
        public AnalysisService AnalysisService => _analysisService;
        public SettingsViewModel Settings { get; }
        public AnalysisTabViewModel AnalysisTab { get; }
        public TradeSignalViewModel TradeSignalTab { get; }
        public string DhanClientId => _dhanClientId;

        public ObservableCollection<OptionChainRow> OptionChainRows { get; }
        public ObservableCollection<TickerIndex> Indices { get; }
        public ObservableCollection<string> ExpiryDates { get; }
        public ObservableCollection<OrderBookEntry> Orders { get; }

        private TickerIndex? _selectedIndex;
        public TickerIndex? SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged(nameof(SelectedIndex));
                    _ = LoadExpiryAndOptionChainAsync();
                }
            }
        }
        private string? _selectedExpiry;
        public string? SelectedExpiry
        {
            get => _selectedExpiry;
            set
            {
                if (_selectedExpiry != value)
                {
                    _selectedExpiry = value;
                    OnPropertyChanged(nameof(SelectedExpiry));
                    if (!_isDataLoading && !string.IsNullOrEmpty(_selectedExpiry) && SelectedIndex != null)
                    {
                        _ = LoadOptionChainOnlyAsync(SelectedIndex.ScripId, MapSegmentForOptionChainApi(SelectedIndex.Segment));
                    }
                }
            }
        }
        public string StatusMessage { get; private set; } = string.Empty;

        private string _newInstrumentSymbol = string.Empty;
        public string NewInstrumentSymbol
        {
            get => _newInstrumentSymbol;
            set { _newInstrumentSymbol = value; OnPropertyChanged(nameof(NewInstrumentSymbol)); }
        }

        private decimal _underlyingPrice;
        public decimal UnderlyingPrice { get => _underlyingPrice; set { if (_underlyingPrice != value) { _underlyingPrice = value; OnPropertyChanged(nameof(UnderlyingPriceChange)); OnPropertyChanged(nameof(UnderlyingPriceChangePercent)); } } }
        private decimal _underlyingPreviousClose;
        public decimal UnderlyingPreviousClose { get => _underlyingPreviousClose; set { if (_underlyingPreviousClose != value) { _underlyingPreviousClose = value; OnPropertyChanged(nameof(UnderlyingPriceChange)); OnPropertyChanged(nameof(UnderlyingPriceChangePercent)); } } }
        public decimal UnderlyingPriceChange => UnderlyingPrice - UnderlyingPreviousClose;
        public decimal UnderlyingPriceChangePercent => UnderlyingPreviousClose == 0 ? 0 : (UnderlyingPriceChange / UnderlyingPreviousClose);

        private long _totalCallOi;
        public long TotalCallOi { get => _totalCallOi; set { _totalCallOi = value; OnPropertyChanged(nameof(TotalCallOi)); } }
        private long _totalPutOi;
        public long TotalPutOi { get => _totalPutOi; set { _totalPutOi = value; OnPropertyChanged(nameof(TotalPutOi)); } }
        private long _totalCallVolume;
        public long TotalCallVolume { get => _totalCallVolume; set { _totalCallVolume = value; OnPropertyChanged(nameof(TotalCallVolume)); } }
        private long _totalPutVolume;
        public long TotalPutVolume { get => _totalPutVolume; set { _totalPutVolume = value; OnPropertyChanged(nameof(TotalPutVolume)); } }
        private decimal _pcrOi;
        public decimal PcrOi { get => _pcrOi; set { _pcrOi = value; OnPropertyChanged(nameof(PcrOi)); } }

        private long _maxOi;
        public long MaxOi { get => _maxOi; set { if (_maxOi != value) { _maxOi = value; OnPropertyChanged(nameof(MaxOi)); } } }
        private decimal _maxOiChange;
        public decimal MaxOiChange { get => _maxOiChange; set { if (_maxOiChange != value) { _maxOiChange = value; OnPropertyChanged(nameof(MaxOiChange)); } } }

        public ICommand BuyCallCommand { get; }
        public ICommand SellCallCommand { get; }
        public ICommand BuyPutCommand { get; }
        public ICommand SellPutCommand { get; }
        public ICommand RefreshOrdersCommand { get; }
        public ICommand RefreshPortfolioCommand { get; }
        public ICommand AddPositionCommand { get; }
        public ICommand ExitPositionCommand { get; }
        public ICommand CloseSelectedPositionsCommand { get; }
        public ICommand ConvertPositionCommand { get; }
        public ICommand ModifyOrderCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand AddInstrumentCommand { get; }
        public ICommand RemoveInstrumentCommand { get; }
        public ICommand ShowMtmGraphCommand { get; }
        #endregion

        public MainViewModel(string clientId, string accessToken)
        {
            _dhanClientId = clientId;
            _apiClient = new DhanApiClient(clientId, accessToken);
            _webSocketClient = new DhanWebSocketClient(clientId, accessToken);
            _scripMasterService = new ScripMasterService();
            _historicalIvService = new HistoricalIvService();
            _marketProfileService = new MarketProfileService();
            _indicatorStateService = new IndicatorStateService();
            _signalLoggerService = new SignalLoggerService();

            var settingsService = new SettingsService();
            Settings = new SettingsViewModel(settingsService, this);
            Settings.SettingsSaved += Settings_SettingsSaved;
            _notificationService = new NotificationService(Settings);

            Dashboard = new DashboardViewModel();
            Portfolio = new PortfolioViewModel();
            Portfolio.PropertyChanged += Portfolio_PropertyChanged;

            _performanceService = new PerformanceService(Portfolio);

            _analysisService = new AnalysisService(Settings, _apiClient, _scripMasterService, _historicalIvService, _marketProfileService, _indicatorStateService, _signalLoggerService, _notificationService, Dashboard, this);

            _autoTraderService = new AutoTraderService(this, _scripMasterService, _apiClient, Settings, _notificationService);
            _analysisService.OnAnalysisUpdated += OnAnalysisResultUpdated;
            _analysisService.OnAnalysisUpdated += _autoTraderService.OnAnalysisResultReceived;

            Dashboard.MonitoredInstruments.CollectionChanged += (s, e) => RebuildDashboardMap();
            Portfolio.OpenPositions.CollectionChanged += (s, e) => RebuildPositionsMap();


            _webSocketClient.OnConnected += OnWebSocketConnected;
            _webSocketClient.OnDisconnected += () => UpdateStatusAsync("WebSocket Disconnected. Attempting to reconnect...");
            _webSocketClient.OnLtpUpdate += OnLtpUpdateReceived;
            _webSocketClient.OnPreviousCloseUpdate += OnPreviousCloseUpdateReceived;
            _webSocketClient.OnQuoteUpdate += OnQuoteUpdateReceived;
            _webSocketClient.OnOiUpdate += OnOiUpdateReceived;
            _webSocketClient.OnOrderUpdate += OnOrderUpdateReceived;

            OptionChainRows = new ObservableCollection<OptionChainRow>();
            ExpiryDates = new ObservableCollection<string>();
            Orders = new ObservableCollection<OrderBookEntry>();
            Indices = new ObservableCollection<TickerIndex>();
            AnalysisTab = new AnalysisTabViewModel();
            TradeSignalTab = new TradeSignalViewModel();

            BuyCallCommand = new RelayCommand(ExecuteBuyCall, (p) => !IsKillSwitchActive);
            SellCallCommand = new RelayCommand(ExecuteSellCall, (p) => !IsKillSwitchActive);
            BuyPutCommand = new RelayCommand(ExecuteBuyPut, (p) => !IsKillSwitchActive);
            SellPutCommand = new RelayCommand(ExecuteSellPut, (p) => !IsKillSwitchActive);
            RefreshOrdersCommand = new RelayCommand(async (p) => await LoadOrdersAsync());
            RefreshPortfolioCommand = new RelayCommand(async (p) => await LoadPortfolioAsync());
            AddPositionCommand = new RelayCommand(ExecuteAddPosition, (p) => !IsKillSwitchActive);
            ExitPositionCommand = new RelayCommand(ExecuteExitPosition, (p) => !IsKillSwitchActive);
            CloseSelectedPositionsCommand = new RelayCommand(async (p) => await ExecuteCloseSelectedPositionsAsync(), (p) => !IsKillSwitchActive);
            ConvertPositionCommand = new RelayCommand(async (p) => await ExecuteConvertPositionAsync(p), (p) => !IsKillSwitchActive);
            ModifyOrderCommand = new RelayCommand(ExecuteModifyOrder, (p) => !IsKillSwitchActive);
            CancelOrderCommand = new RelayCommand(async (p) => await ExecuteCancelOrderAsync(p));
            AddInstrumentCommand = new RelayCommand(async _ => await ExecuteAddInstrumentAsync(), _ => !string.IsNullOrWhiteSpace(NewInstrumentSymbol));
            RemoveInstrumentCommand = new RelayCommand(async p => await ExecuteRemoveInstrumentAsync(p));
            ShowMtmGraphCommand = new RelayCommand(ExecuteShowMtmGraph);

            _uiUpdateTimer = new Timer(ProcessPendingUiUpdates, null, Timeout.Infinite, Timeout.Infinite);

            _ = LoadDataOnStartupAsync();
        }

        private void Settings_SettingsSaved(object? sender, EventArgs e)
        {
        }

        private void OnAnalysisResultUpdated(AnalysisResult result)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_dashboardInstrumentMap.TryGetValue(result.SecurityId, out var instrumentToUpdate))
                {
                    instrumentToUpdate.TradingSignal = result.EmaSignal1Min;
                }
                AnalysisTab.UpdateAnalysisResult(result);
                TradeSignalTab.UpdateSignalResult(result);
            });
        }

        private void Portfolio_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Portfolio.NetPnl))
            {
                CheckAutoKillSwitch();
            }
        }

        #region Command Methods

        private void ExecuteShowMtmGraph(object? parameter)
        {
            try
            {
                var pnlHistory = _performanceService.LoadPnlHistory();
                if (pnlHistory == null || !pnlHistory.Any())
                {
                    MessageBox.Show("No P&L data has been recorded for today yet.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var viewModel = new MtmGraphViewModel(pnlHistory);
                var window = new MtmGraphWindow(viewModel)
                {
                    Owner = Application.Current.MainWindow
                };
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while opening the MTM graph: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task EngageKillSwitchAsync()
        {
            if (IsKillSwitchActive) return;

            var warningMessage = "WARNING: This will attempt to square off all open positions, cancel all pending orders, and then disable all trading functions for the rest of the day.\n\nAre you absolutely sure you want to proceed?";

            if (MessageBox.Show(warningMessage, "Confirm Kill Switch Activation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            await UpdateStatusAsync("ENGAGING KILL SWITCH...");

            try
            {
                await UpdateStatusAsync("Cancelling all pending orders...");
                var pendingOrders = Orders.Where(o => o.IsPending).ToList();
                foreach (var order in pendingOrders)
                {
                    await _apiClient.CancelOrderAsync(order.OrderId);
                    await Task.Delay(200);
                }
                await UpdateStatusAsync("All pending orders cancelled.");

                await UpdateStatusAsync("Squaring off all open positions...");
                var openPositions = Portfolio.OpenPositions.ToList();
                foreach (var pos in openPositions)
                {
                    var orderRequest = new OrderRequest
                    {
                        DhanClientId = _dhanClientId,
                        TransactionType = pos.Quantity > 0 ? TransactionTypeSell : TransactionTypeBuy,
                        ExchangeSegment = "NSE_FNO",
                        ProductType = pos.ProductType,
                        OrderType = OrderTypeMarket,
                        SecurityId = pos.SecurityId,
                        Quantity = Math.Abs(pos.Quantity),
                        Validity = ValidityDay
                    };
                    await _apiClient.PlaceOrderAsync(orderRequest);
                    await Task.Delay(200);
                }
                await UpdateStatusAsync("All positions squared off.");

                await UpdateStatusAsync("Activating final broker-level Kill Switch...");
                var success = await _apiClient.ActivateKillSwitchAsync();

                if (success)
                {
                    IsKillSwitchActive = true;
                    await UpdateStatusAsync("KILL SWITCH ACTIVATED. Trading disabled for the day.");
                    MessageBox.Show("Kill Switch has been successfully activated. All trading is now blocked.", "Kill Switch Active", MessageBoxButton.OK, MessageBoxImage.Information);

                    await Task.Delay(2000);
                    await LoadPortfolioAsync();
                    await LoadOrdersAsync();
                }
                else
                {
                    await UpdateStatusAsync("Failed to activate broker Kill Switch. Trading disabled locally.");
                    MessageBox.Show("Could not confirm Kill Switch activation with the broker, but trading has been disabled within this application to be safe.", "Kill Switch Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    IsKillSwitchActive = true;
                }
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"Kill Switch API Error: {ex.Message}. Trading disabled locally.");
                MessageBox.Show($"An API error occurred during the Kill Switch sequence: {ex.Message}\n\nFor safety, all trading functions in this app have been disabled.", "Kill Switch API Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsKillSwitchActive = true;
            }
        }

        private void CheckAutoKillSwitch()
        {
            if (IsKillSwitchActive || !Settings.IsAutoKillSwitchEnabled || Settings.MaxDailyLossLimit <= 0)
            {
                return;
            }

            decimal lossLimit = -Math.Abs(Settings.MaxDailyLossLimit);

            if (Portfolio.NetPnl <= lossLimit)
            {
                MessageBox.Show($"Automatic Kill Switch triggered! Your Net P&L of {Portfolio.NetPnl:C} has breached the maximum daily loss limit of {lossLimit:C}.", "Automatic Kill Switch", MessageBoxButton.OK, MessageBoxImage.Warning);
                _ = EngageKillSwitchAsync();
            }
        }


        private async Task ExecuteAddInstrumentAsync()
        {
            var userInput = NewInstrumentSymbol.Trim();
            if (string.IsNullOrEmpty(userInput)) return;

            await UpdateStatusAsync($"Searching for {userInput}...");

            var (instrument, prefixedSymbol) = await FindAndCreateDashboardInstrumentAsync(userInput);

            if (instrument == null || string.IsNullOrEmpty(instrument.SecurityId))
            {
                await UpdateStatusAsync($"Instrument '{userInput}' not found.");
                MessageBox.Show($"Could not find a matching instrument for '{userInput}'.\nPlease use Security ID or TYPE:SYMBOL format (e.g., EQ:RELIANCE, FUT:NIFTY).", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewInstrumentSymbol = string.Empty;
                return;
            }

            if (Dashboard.MonitoredInstruments.Any(i => i.SecurityId == instrument.SecurityId))
            {
                await UpdateStatusAsync($"Instrument '{instrument.DisplayName}' is already in the dashboard.");
                NewInstrumentSymbol = string.Empty;
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Dashboard.MonitoredInstruments.Add(instrument);
                Settings.AddMonitoredSymbol(prefixedSymbol);
            });

            await UpdateSubscriptionsAsync();
            await UpdateStatusAsync($"Added '{instrument.DisplayName}' to dashboard.");
            NewInstrumentSymbol = string.Empty;
        }

        private async Task ExecuteRemoveInstrumentAsync(object? parameter)
        {
            if (parameter is not DashboardInstrument instrumentToRemove) return;

            string? prefixedSymbolToRemove = Settings.MonitoredSymbols.FirstOrDefault(s =>
                s.EndsWith(":" + instrumentToRemove.UnderlyingSymbol, StringComparison.OrdinalIgnoreCase) ||
                s.EndsWith(":" + instrumentToRemove.Symbol, StringComparison.OrdinalIgnoreCase));

            if (prefixedSymbolToRemove == null)
            {
                prefixedSymbolToRemove = Settings.MonitoredSymbols.FirstOrDefault(s => s.Equals($"IDX:{instrumentToRemove.Symbol}", StringComparison.OrdinalIgnoreCase));
            }

            if (string.IsNullOrEmpty(prefixedSymbolToRemove))
            {
                await UpdateStatusAsync($"Could not find '{instrumentToRemove.DisplayName}' in settings to remove.");
                await Application.Current.Dispatcher.InvokeAsync(() => Dashboard.MonitoredInstruments.Remove(instrumentToRemove));
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Dashboard.MonitoredInstruments.Remove(instrumentToRemove);
                Settings.RemoveMonitoredSymbol(prefixedSymbolToRemove);
            });

            await UpdateStatusAsync($"Removed '{instrumentToRemove.DisplayName}' from dashboard.");
            await UpdateSubscriptionsAsync();
        }

        private void ExecuteBuyCall(object? p) => OpenOrderWindowForOption(p as OptionChainRow, true, true);
        private void ExecuteSellCall(object? p) => OpenOrderWindowForOption(p as OptionChainRow, false, true);
        private void ExecuteBuyPut(object? p) => OpenOrderWindowForOption(p as OptionChainRow, true, false);
        private void ExecuteSellPut(object? p) => OpenOrderWindowForOption(p as OptionChainRow, false, false);
        private void ExecuteAddPosition(object? p) { if (p is Position pos) OpenOrderWindowForPosition(pos, pos.Quantity > 0); }
        private void ExecuteExitPosition(object? p) { if (p is Position pos) OpenOrderWindowForPosition(pos, pos.Quantity < 0); }

        private async Task ExecuteCloseSelectedPositionsAsync()
        {
            var selectedPositions = Portfolio.OpenPositions.Where(pos => pos.IsSelected).ToList();
            if (!selectedPositions.Any()) { MessageBox.Show("No positions selected.", "Information", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (MessageBox.Show($"Are you sure you want to close {selectedPositions.Count} selected position(s) at market price?", "Confirm Close Positions", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;

            await UpdateStatusAsync($"Closing {selectedPositions.Count} position(s)...");
            try
            {
                foreach (var pos in selectedPositions)
                {
                    var orderRequest = new OrderRequest
                    {
                        DhanClientId = _dhanClientId,
                        TransactionType = pos.Quantity > 0 ? TransactionTypeSell : TransactionTypeBuy,
                        ExchangeSegment = "NSE_FNO",
                        ProductType = pos.ProductType,
                        OrderType = OrderTypeMarket,
                        SecurityId = pos.SecurityId,
                        Quantity = Math.Abs(pos.Quantity),
                        Validity = ValidityDay
                    };
                    await _apiClient.PlaceOrderAsync(orderRequest);
                }
                await Task.Delay(1500);
                await LoadPortfolioAsync();
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"Failed to close positions: {ex.Message}");
            }
        }

        private async Task ExecuteConvertPositionAsync(object? parameter)
        {
            if (parameter is Position position)
            {
                var newProductType = position.ProductType == ProductTypeIntraday ? ProductTypeMargin : ProductTypeIntraday;
                var convertRequest = new ConvertPositionRequest
                {
                    DhanClientId = _dhanClientId,
                    SecurityId = position.SecurityId,
                    ProductType = position.ProductType,
                    ConvertTo = newProductType,
                    Quantity = Math.Abs(position.Quantity)
                };
                try
                {
                    var success = await _apiClient.ConvertPositionAsync(convertRequest);
                    if (success)
                    {
                        await UpdateStatusAsync("Position conversion successful.");
                        await LoadPortfolioAsync();
                    }
                    else
                    {
                        await UpdateStatusAsync("Position conversion failed for an unknown reason.");
                    }
                }
                catch (DhanApiException ex)
                {
                    await UpdateStatusAsync($"Position conversion failed: {ex.Message}");
                }
            }
        }

        private void ExecuteModifyOrder(object? parameter)
        {
            if (parameter is not OrderBookEntry order) return;

            var dashboardInstrument = Dashboard.MonitoredInstruments.FirstOrDefault(i => i.SecurityId == order.SecurityId);
            var previousClose = dashboardInstrument?.Close ?? order.Price;
            var exchangeSegment = dashboardInstrument?.SegmentId == 1 ? "NSE_EQ" : "NSE_FNO";
            var freezeLimit = GetFreezeLimitForInstrument(order.TradingSymbol);

            var orderViewModel = new OrderEntryViewModel(
                order.SecurityId,
                order.TradingSymbol,
                exchangeSegment,
                order.TransactionType == TransactionTypeBuy,
                order.Price,
                previousClose,
                _scripMasterService.GetLotSizeForSecurity(order.SecurityId),
                order.ProductType,
                _apiClient,
                _webSocketClient,
                _dhanClientId,
                _scripMasterService,
                freezeLimit,
                IsKillSwitchActive,
                existingOrder: order
            );

            var orderWindow = new OrderEntryWindow { DataContext = orderViewModel, Title = "Modify Order" };
            orderWindow.Owner = Application.Current.MainWindow;
            orderWindow.ShowDialog();
            _ = LoadOrdersAsync();
        }

        private async Task ExecuteCancelOrderAsync(object? parameter)
        {
            if (parameter is OrderBookEntry order)
            {
                if (MessageBox.Show($"Are you sure you want to cancel order {order.OrderId}?", "Confirm Cancellation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await UpdateStatusAsync($"Cancelling order {order.OrderId}...");
                    try
                    {
                        var response = await _apiClient.CancelOrderAsync(order.OrderId);
                        if (response != null)
                        {
                            await UpdateStatusAsync($"Order {order.OrderId} cancellation processed.");
                        }
                    }
                    catch (DhanApiException ex)
                    {
                        await UpdateStatusAsync($"Failed to cancel order {order.OrderId}: {ex.Message}");
                    }
                    await LoadOrdersAsync();
                }
            }
        }

        private void OpenOrderWindowForOption(OptionChainRow? row, bool isBuy, bool isCall)
        {
            if (row == null) return;
            var option = isCall ? row.CallOption : row.PutOption;
            if (option == null || string.IsNullOrEmpty(option.SecurityId)) { MessageBox.Show("Cannot place order for an invalid instrument.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            string instrumentName = ConstructInstrumentName(row.StrikePrice, isCall);
            string exchangeSegment = SelectedIndex?.ExchId == "BSE" ? "BSE_FNO" : "NSE_FNO";
            var freezeLimit = GetFreezeLimitForInstrument(SelectedIndex?.Symbol ?? "");

            var orderViewModel = new OrderEntryViewModel(
                option.SecurityId,
                instrumentName,
                exchangeSegment,
                isBuy,
                option.LTP,
                option.PreviousClose,
                _scripMasterService.GetLotSizeForSecurity(option.SecurityId),
                ProductTypeIntraday,
                _apiClient,
                _webSocketClient,
                _dhanClientId,
                _scripMasterService,
                freezeLimit,
                IsKillSwitchActive
            );

            var orderWindow = new OrderEntryWindow { DataContext = orderViewModel, Owner = Application.Current.MainWindow };
            orderWindow.ShowDialog();
            _ = LoadPortfolioAsync();
        }

        private void OpenOrderWindowForPosition(Position position, bool isBuy)
        {
            var dashboardInstrument = Dashboard.MonitoredInstruments.FirstOrDefault(i => i.SecurityId == position.SecurityId);
            var previousClose = dashboardInstrument?.Close ?? position.LastTradedPrice;
            var exchangeSegment = dashboardInstrument?.SegmentId == 1 ? "NSE_EQ" : "NSE_FNO";
            var freezeLimit = GetFreezeLimitForInstrument(position.Ticker);

            var orderViewModel = new OrderEntryViewModel(
                position.SecurityId,
                position.Ticker,
                exchangeSegment,
                isBuy,
                position.LastTradedPrice,
                previousClose,
                _scripMasterService.GetLotSizeForSecurity(position.SecurityId),
                position.ProductType,
                _apiClient,
                _webSocketClient,
                _dhanClientId,
                _scripMasterService,
                freezeLimit,
                IsKillSwitchActive
            );

            var orderWindow = new OrderEntryWindow { DataContext = orderViewModel, Owner = Application.Current.MainWindow };
            orderWindow.ShowDialog();
            _ = LoadPortfolioAsync();
        }

        private int GetFreezeLimitForInstrument(string instrumentName)
        {
            if (string.IsNullOrEmpty(instrumentName)) return Settings.NiftyFreezeQuantity;

            if (instrumentName.Contains("BANKNIFTY")) return Settings.BankNiftyFreezeQuantity;
            if (instrumentName.Contains("FINNIFTY")) return Settings.FinNiftyFreezeQuantity;
            if (instrumentName.Contains("SENSEX")) return Settings.SensexFreezeQuantity;
            if (instrumentName.Contains("NIFTY")) return Settings.NiftyFreezeQuantity;

            return Settings.NiftyFreezeQuantity;
        }

        private string ConstructInstrumentName(decimal strike, bool isCall)
        {
            string formattedStrike = strike.ToString("G29", CultureInfo.InvariantCulture);
            return $"{SelectedIndex?.Symbol} {SelectedExpiry?.ToUpper()} {formattedStrike} {(isCall ? "CALL" : "PUT")}";
        }

        #endregion

        #region Data Loading and WebSocket Handling
        private async Task LoadDataOnStartupAsync()
        {
            try
            {
                await UpdateStatusAsync("Downloading Instrument Master...");
                await _scripMasterService.LoadScripMasterAsync();

                PopulateIndices();

                _ = _webSocketClient.ConnectAndMaintainAsync();
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync($"Fatal error during startup: {ex.Message}");
                MessageBox.Show($"A critical error occurred on startup: {ex.Message}\nThe application might not function correctly.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateIndices()
        {
            var indexSymbols = new[] { "Nifty 50", "Nifty Bank", "Sensex" };

            foreach (var indexName in indexSymbols)
            {
                ScripInfo? indexScripInfo = _scripMasterService.FindIndexScripInfo(indexName);

                if (indexScripInfo != null && !string.IsNullOrEmpty(indexScripInfo.SecurityId))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Indices.Add(new TickerIndex
                        {
                            Name = indexName,
                            Symbol = indexName,
                            ScripId = indexScripInfo.SecurityId,
                            Segment = indexScripInfo.Segment,
                            ExchId = indexScripInfo.ExchId
                        });
                    });
                }
            }
        }

        private async void OnWebSocketConnected()
        {
            try
            {
                await UpdateStatusAsync("WebSocket Connected. Initializing Dashboard and Subscribing...");

                await InitializeDashboardAsync();
                await PreloadNearestExpiriesAsync();

                _analysisService.SetNearestExpiryDates(_nearestExpiryDates);

                _ = Task.Run(LoadInitialOptionChainsAsync);

                await UpdateSubscriptionsAsync();
                await _webSocketClient.SubscribeToOrderFeedAsync();
                await LoadPortfolioAsync();
                await LoadOrdersAsync();

                _optionChainRefreshTimer = new Timer(async _ => await RefreshOptionChainDataAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15));
                _ivRefreshTimer = new Timer(async _ => await LoadInitialOptionChainsAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                _uiUpdateTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200));

                Application.Current.Dispatcher.InvokeAsync(() => { SelectedIndex = Indices.FirstOrDefault(i => i.Name == "Nifty 50"); });
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"API Error during initial load: {ex.Message}");
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync($"An unexpected error occurred during initial load: {ex.Message}");
            }
        }

        private async Task InitializeDashboardAsync()
        {
            await UpdateStatusAsync("Loading dashboard from settings...");
            await Application.Current.Dispatcher.InvokeAsync(() => Dashboard.MonitoredInstruments.Clear());

            var uniqueSymbols = new HashSet<string>(Settings.MonitoredSymbols, StringComparer.OrdinalIgnoreCase);

            var instrumentsToAdd = new List<DashboardInstrument>();
            foreach (var prefixedSymbol in uniqueSymbols)
            {
                var (instrument, _) = await FindAndCreateDashboardInstrumentAsync(prefixedSymbol);
                if (instrument != null)
                {
                    if (!instrumentsToAdd.Any(i => i.SecurityId == instrument.SecurityId))
                    {
                        instrumentsToAdd.Add(instrument);
                    }
                }
            }

            if (uniqueSymbols.Count < Settings.MonitoredSymbols.Count)
            {
                Settings.ReplaceMonitoredSymbols(uniqueSymbols.ToList());
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in instrumentsToAdd)
                    Dashboard.MonitoredInstruments.Add(item);
            });

            await UpdateStatusAsync("Dashboard loaded.");
        }
        private async Task<(DashboardInstrument?, string)> FindAndCreateDashboardInstrumentAsync(string userInput)
        {
            ScripInfo? info = null;
            string prefixedSymbolToSave = userInput;

            if (long.TryParse(userInput, out _))
            {
                info = _scripMasterService.FindBySecurityId(userInput);
            }

            if (info == null)
            {
                var parts = userInput.Split(new[] { ':' }, 2);
                if (parts.Length != 2) return (null, string.Empty);

                string type = parts[0].Trim().ToUpper();
                string symbol = parts[1].Trim();

                prefixedSymbolToSave = $"{type}:{symbol}";

                switch (type)
                {
                    case "IDX": info = _scripMasterService.FindIndexScripInfo(symbol); break;
                    case "EQ": info = _scripMasterService.FindEquityScripInfo(symbol); break;
                    case "FUT": info = _scripMasterService.FindNearMonthFutureSecurityId(symbol); break;
                }
            }

            if (info == null) return (null, string.Empty);

            if (long.TryParse(userInput, out _))
            {
                string typePrefix = GetTypePrefix(info.InstrumentType);
                string symbolPart = (typePrefix == "FUT" || typePrefix == "EQ") ? info.UnderlyingSymbol : info.SemInstrumentName;
                prefixedSymbolToSave = $"{typePrefix}:{symbolPart}";
            }

            var dashboardInstrument = CreateDashboardInstrument(info);

            return (dashboardInstrument, prefixedSymbolToSave);
        }

        private DashboardInstrument CreateDashboardInstrument(ScripInfo info)
        {
            string typePrefix = GetTypePrefix(info.InstrumentType);
            string underlying = (typePrefix == "FUT" || typePrefix == "EQ") ? info.UnderlyingSymbol : info.SemInstrumentName;

            var instrument = new DashboardInstrument
            {
                Symbol = info.TradingSymbol,
                DisplayName = info.SemInstrumentName,
                SecurityId = info.SecurityId,
                SegmentId = _scripMasterService.GetSegmentIdFromName(info.Segment),
                ExchId = info.ExchId,
                FeedType = FeedTypeQuote,
                IsFuture = (typePrefix == "FUT"),
                UnderlyingSymbol = underlying,
                InstrumentType = info.InstrumentType
            };

            if (typePrefix == "IDX")
            {
                instrument.Symbol = info.SemInstrumentName;
            }
            return instrument;
        }

        private string GetTypePrefix(string instrumentType)
        {
            return instrumentType switch
            {
                "EQUITY" => "EQ",
                "FUTIDX" => "FUT",
                "FUTSTK" => "FUT",
                "INDEX" => "IDX",
                "OPTIDX" => "OPT",
                "OPTSTK" => "OPT",
                _ => "UNKNOWN"
            };
        }

        private async Task UpdateSubscriptionsAsync()
        {
            var allInstruments = Dashboard.MonitoredInstruments
                .Where(i => !string.IsNullOrEmpty(i.SecurityId))
                .DistinctBy(i => i.SecurityId)
                .ToList();

            var quoteInstruments = allInstruments
                .Where(i => i.FeedType == FeedTypeQuote)
                .ToDictionary(i => i.SecurityId, i => i.SegmentId);

            var tickerInstruments = allInstruments
                .Where(i => i.FeedType == FeedTypeTicker && !quoteInstruments.ContainsKey(i.SecurityId))
                .ToDictionary(i => i.SecurityId, i => i.SegmentId);


            if (quoteInstruments.Any())
            {
                await _webSocketClient.SubscribeToInstrumentsAsync(quoteInstruments, 17); // 17 is for Quote+OI
            }
            if (tickerInstruments.Any())
            {
                await _webSocketClient.SubscribeToInstrumentsAsync(tickerInstruments, 15); // 15 is for Ticker (LTP only)
            }
        }

        private async Task PreloadNearestExpiriesAsync()
        {
            await UpdateStatusAsync("Fetching option expiry dates...");
            foreach (var indexInfo in Indices)
            {
                var expiryListResponse = await _apiClient.GetExpiryListAsync(indexInfo.ScripId, MapSegmentForOptionChainApi(indexInfo.Segment));
                string? nearestExpiry = expiryListResponse?.ExpiryDates?.FirstOrDefault();
                if (!string.IsNullOrEmpty(nearestExpiry))
                {
                    _nearestExpiryDates[indexInfo.Symbol] = nearestExpiry;
                }
            }
        }

        private int GetSegmentIdFromName(string segmentName)
        {
            return segmentName switch
            {
                "NSE_EQ" => 1,
                "NSE_FNO" => 2,
                "BSE_EQ" => 3,
                "BSE_FNO" => 8,
                "IDX_I" => 0,
                "I" => 0,
                _ => -1
            };
        }

        private string MapSegmentForOptionChainApi(string segmentName)
        {
            return segmentName switch
            {
                "I" => "IDX_I",
                _ => segmentName
            };
        }

        private void OnLtpUpdateReceived(TickerPacket packet)
        {
            if (packet?.SecurityId != null)
            {
                _pendingTickerUpdates[packet.SecurityId] = packet;
            }
        }

        private void OnPreviousCloseUpdateReceived(PreviousClosePacket packet)
        {
            if (packet?.SecurityId != null)
            {
                _pendingPreviousCloseUpdates[packet.SecurityId] = packet;
            }
        }

        private void OnQuoteUpdateReceived(QuotePacket packet)
        {
            if (packet?.SecurityId != null)
            {
                _pendingQuoteUpdates[packet.SecurityId] = packet;
            }
        }

        private void OnOiUpdateReceived(OiPacket packet)
        {
            if (packet?.SecurityId != null)
            {
                _pendingOiUpdates[packet.SecurityId] = packet;
            }
        }

        private void OnOrderUpdateReceived(OrderBookEntry updatedOrder)
        {
            Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var orderToUpdate = Orders.FirstOrDefault(o => o.OrderId == updatedOrder.OrderId);
                if (orderToUpdate != null)
                {
                    orderToUpdate.OrderStatus = updatedOrder.OrderStatus;
                    orderToUpdate.FilledQuantity = updatedOrder.FilledQuantity;
                    orderToUpdate.UpdateTime = updatedOrder.UpdateTime;
                    orderToUpdate.AverageTradedPrice = updatedOrder.AverageTradedPrice;

                    await UpdateStatusAsync($"Order {updatedOrder.OrderId} updated: {updatedOrder.OrderStatus}");
                }
                else
                {
                    Orders.Insert(0, updatedOrder);
                    await UpdateStatusAsync($"New order received: {updatedOrder.OrderId}");
                }

                if (updatedOrder.OrderStatus == "TRADED")
                {
                    await LoadPortfolioAsync();
                }
            });
        }

        private void ProcessPendingUiUpdates(object? state)
        {
            var tickerUpdates = _pendingTickerUpdates.Values.ToList();
            _pendingTickerUpdates.Clear();

            var quoteUpdates = _pendingQuoteUpdates.Values.ToList();
            _pendingQuoteUpdates.Clear();

            var oiUpdates = _pendingOiUpdates.Values.ToList();
            _pendingOiUpdates.Clear();

            var prevCloseUpdates = _pendingPreviousCloseUpdates.Values.ToList();
            _pendingPreviousCloseUpdates.Clear();

            if (!tickerUpdates.Any() && !quoteUpdates.Any() && !oiUpdates.Any() && !prevCloseUpdates.Any())
            {
                return;
            }

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                foreach (var packet in tickerUpdates)
                {
                    if (SelectedIndex != null && packet.SecurityId == SelectedIndex.ScripId)
                    {
                        UnderlyingPrice = packet.LastPrice;
                    }
                    Dashboard.UpdateLtp(packet);

                    if (!string.IsNullOrEmpty(packet.SecurityId) && _openPositionsMap.TryGetValue(packet.SecurityId, out var openPositionToUpdate))
                    {
                        openPositionToUpdate.LastTradedPrice = packet.LastPrice;
                    }
                }

                foreach (var packet in prevCloseUpdates)
                {
                    Dashboard.UpdatePreviousClose(packet);
                    if (SelectedIndex?.ScripId == packet.SecurityId)
                    {
                        UnderlyingPreviousClose = packet.PreviousClose;
                    }
                }

                foreach (var packet in quoteUpdates)
                {
                    if (string.IsNullOrEmpty(packet.SecurityId)) continue;

                    if (_dashboardInstrumentMap.TryGetValue(packet.SecurityId, out var instrumentToUpdate))
                    {
                        instrumentToUpdate.LTP = packet.LastPrice;
                        instrumentToUpdate.Open = packet.Open;
                        instrumentToUpdate.High = packet.High;
                        instrumentToUpdate.Low = packet.Low;
                        instrumentToUpdate.Close = packet.Close;
                        instrumentToUpdate.Volume = packet.Volume;
                        instrumentToUpdate.LastTradedQuantity = packet.LastTradeQuantity;
                        instrumentToUpdate.LastTradeTime = packet.LastTradeTime;
                        instrumentToUpdate.AvgTradePrice = packet.AvgTradePrice;

                        if (_optionChainCache.TryGetValue(packet.SecurityId, out var cachedOptionData))
                        {
                            instrumentToUpdate.ImpliedVolatility = cachedOptionData.ImpliedVolatility;
                        }

                        decimal underlyingLtp = 0;
                        if (instrumentToUpdate.InstrumentType.StartsWith("OPT") || instrumentToUpdate.InstrumentType.StartsWith("FUT"))
                        {
                            var underlyingInstrument = FindUnderlyingInstrument(instrumentToUpdate);
                            if (underlyingInstrument != null)
                            {
                                underlyingLtp = underlyingInstrument.LTP;
                            }
                        }
                        else
                        {
                            underlyingLtp = instrumentToUpdate.LTP;
                        }

                        if (underlyingLtp > 0)
                        {
                            _analysisService.OnInstrumentDataReceived(instrumentToUpdate, underlyingLtp);
                        }


                        if (SelectedIndex != null && SelectedIndex.ScripId == packet.SecurityId)
                        {
                            UnderlyingPrice = packet.LastPrice;
                        }

                        if (_optionScripMap.TryGetValue(packet.SecurityId, out var optionDetails))
                        {
                            optionDetails.LTP = packet.LastPrice;
                            optionDetails.Volume = packet.Volume;
                        }

                        if (instrumentToUpdate != null && instrumentToUpdate.SegmentId == 0 && !_dashboardOptionsLoadedFor.Contains(packet.SecurityId))
                        {
                            _dashboardOptionsLoadedFor.Add(packet.SecurityId);
                            _ = LoadDashboardOptionsForIndexAsync(instrumentToUpdate, packet.LastPrice);
                        }

                        if (_openPositionsMap.TryGetValue(packet.SecurityId, out var openPositionToUpdate))
                        {
                            openPositionToUpdate.LastTradedPrice = packet.LastPrice;
                        }
                    }
                }


                foreach (var packet in oiUpdates)
                {
                    if (!string.IsNullOrEmpty(packet.SecurityId))
                    {
                        if (_optionScripMap.TryGetValue(packet.SecurityId, out var optionDetails))
                        {
                            optionDetails.OI = packet.OpenInterest;
                        }
                        Dashboard.UpdateOi(packet);
                    }
                }
            });
        }

        public async Task LoadPortfolioAsync()
        {
            await UpdateStatusAsync("Fetching portfolio...");
            try
            {
                var positionsFromApi = await _apiClient.GetPositionsAsync();
                var fundLimitFromApi = await _apiClient.GetFundLimitAsync();

                var securityIdsToSubscribe = new Dictionary<string, int>();

                if (positionsFromApi != null)
                {
                    foreach (var posData in positionsFromApi.Where(p => p.NetQuantity != 0))
                    {
                        if (!string.IsNullOrEmpty(posData.SecurityId) && !string.IsNullOrEmpty(posData.Exchange))
                        {
                            int segmentId = _scripMasterService.GetSegmentIdFromName(posData.Exchange);
                            if (segmentId != -1)
                            {
                                securityIdsToSubscribe[posData.SecurityId] = segmentId;
                            }
                        }
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Portfolio.UpdatePositions(positionsFromApi);

                    if (fundLimitFromApi != null)
                    {
                        Portfolio.FundDetails.AvailableBalance = fundLimitFromApi.AvailableBalance;
                        Portfolio.FundDetails.UtilizedMargin = fundLimitFromApi.UtilizedAmount;
                        Portfolio.FundDetails.Collateral = fundLimitFromApi.CollateralAmount;
                        Portfolio.FundDetails.WithdrawableBalance = fundLimitFromApi.WithdrawableBalance;
                    }
                });

                if (securityIdsToSubscribe.Any())
                {
                    await _webSocketClient.SubscribeToInstrumentsAsync(securityIdsToSubscribe, 17);
                }

                await UpdateStatusAsync("Portfolio updated.");
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"API Error fetching portfolio: {ex.Message}");
                MessageBox.Show($"Error fetching portfolio: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync($"An unexpected error occurred while loading portfolio: {ex.Message}");
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task LoadOrdersAsync()
        {
            await UpdateStatusAsync("Fetching order book...");
            try
            {
                var orders = await _apiClient.GetOrderBookAsync();
                await Application.Current.Dispatcher.InvokeAsync(() => { Orders.Clear(); if (orders != null) foreach (var order in orders.OrderByDescending(o => DateTime.Parse(o.CreateTime))) Orders.Add(order); });
                await UpdateStatusAsync("Order book updated.");
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"API Error fetching orders: {ex.Message}");
            }
        }

        private async Task LoadExpiryAndOptionChainAsync()
        {
            if (SelectedIndex == null) return;

            await _optionChainLoadSemaphore.WaitAsync();
            try
            {
                _isDataLoading = true;
                _optionChainRefreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                string apiSecurityId = SelectedIndex.ScripId;
                string apiSegment = MapSegmentForOptionChainApi(SelectedIndex.Segment);

                var expiryListResponse = await _apiClient.GetExpiryListAsync(apiSecurityId, apiSegment);

                if (expiryListResponse?.ExpiryDates == null || !expiryListResponse.ExpiryDates.Any())
                {
                    await UpdateStatusAsync($"No expiry dates found for {SelectedIndex.Name}. This is common on non-trading days.");
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ExpiryDates.Clear();
                        OptionChainRows.Clear();
                    });
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ExpiryDates.Clear();
                    foreach (var expiry in expiryListResponse.ExpiryDates.OrderBy(d => DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
                    {
                        ExpiryDates.Add(expiry);
                    }
                    _selectedExpiry = ExpiryDates.FirstOrDefault();
                    OnPropertyChanged(nameof(SelectedExpiry));
                });

                if (!string.IsNullOrEmpty(_selectedExpiry))
                {
                    await LoadOptionChainOnlyInternalAsync(apiSecurityId, apiSegment);
                }
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"API Error loading option chain: {ex.Message}");
            }
            finally
            {
                _isDataLoading = false;
                _optionChainRefreshTimer?.Change(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
                _optionChainLoadSemaphore.Release();
            }
        }

        private async Task LoadOptionChainOnlyAsync(string apiSecurityId, string apiSegment)
        {
            if (SelectedIndex == null || string.IsNullOrWhiteSpace(SelectedExpiry) || string.IsNullOrEmpty(apiSecurityId) || string.IsNullOrEmpty(apiSegment)) return;

            await _optionChainLoadSemaphore.WaitAsync();
            try
            {
                await LoadOptionChainOnlyInternalAsync(apiSecurityId, apiSegment);
            }
            finally
            {
                _optionChainLoadSemaphore.Release();
            }
        }

        private async Task LoadOptionChainOnlyInternalAsync(string apiSecurityId, string apiSegment)
        {
            try
            {
                await UpdateStatusAsync($"Fetching option chain for {SelectedIndex?.Name ?? "Selected Index"} - {SelectedExpiry}...");
                var optionChainResponse = await _apiClient.GetOptionChainAsync(apiSecurityId, apiSegment, SelectedExpiry!);

                if (optionChainResponse?.Data?.OptionChain == null)
                {
                    await UpdateStatusAsync($"Failed to load option chain for {SelectedIndex?.Name ?? "Selected Index"} - {SelectedExpiry}.");
                    return;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    OptionChainRows.Clear();
                    _optionScripMap.Clear();
                    TotalCallOi = 0; TotalPutOi = 0; TotalCallVolume = 0; TotalPutVolume = 0;
                    PcrOi = 0; MaxOi = 0; MaxOiChange = 0;
                });

                var newSubscriptions = new Dictionary<string, int>();
                int optionSegmentId = (SelectedIndex?.ExchId == "BSE") ? GetSegmentIdFromName("BSE_FNO") : GetSegmentIdFromName("NSE_FNO");

                UnderlyingPrice = optionChainResponse.Data.UnderlyingLastPrice;

                var allStrikes = optionChainResponse.Data.OptionChain
                    .Select(kvp => decimal.TryParse(kvp.Key, out var p) ? new { Price = p, Data = kvp.Value } : null)
                    .Where(s => s != null)
                    .OrderBy(s => s!.Price).ToList();

                if (!allStrikes.Any()) return;

                var atmStrikeData = allStrikes.OrderBy(s => Math.Abs(s!.Price - UnderlyingPrice)).First();
                var atmIndex = allStrikes.IndexOf(atmStrikeData!);

                int displayRange = 15;
                int startIndex = Math.Max(0, atmIndex - displayRange);
                int endIndex = Math.Min(allStrikes.Count - 1, atmIndex + displayRange);

                var strikesToDisplay = allStrikes.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
                string scripMasterUnderlying = GetUnderlyingSymbolForScripMaster(SelectedIndex?.Name ?? string.Empty);

                foreach (var strikeInfo in strikesToDisplay)
                {
                    if (strikeInfo?.Data == null) continue;

                    var (callOptionDetails, _) = MapToOptionDetails(strikeInfo.Data.CallOption, strikeInfo.Price, "CE", scripMasterUnderlying, SelectedExpiry!);
                    var (putOptionDetails, _) = MapToOptionDetails(strikeInfo.Data.PutOption, strikeInfo.Price, "PE", scripMasterUnderlying, SelectedExpiry!);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrEmpty(callOptionDetails.SecurityId))
                        {
                            _optionScripMap[callOptionDetails.SecurityId] = callOptionDetails;
                            newSubscriptions[callOptionDetails.SecurityId] = optionSegmentId;
                        }
                        if (!string.IsNullOrEmpty(putOptionDetails.SecurityId))
                        {
                            _optionScripMap[putOptionDetails.SecurityId] = putOptionDetails;
                            newSubscriptions[putOptionDetails.SecurityId] = optionSegmentId;
                        }

                        OptionChainRows.Add(new OptionChainRow
                        {
                            StrikePrice = strikeInfo.Price,
                            CallOption = callOptionDetails,
                            PutOption = putOptionDetails,
                            IsAtm = strikeInfo.Price == atmStrikeData.Price,
                            CallState = strikeInfo.Price < UnderlyingPrice ? OptionState.ITM : OptionState.OTM,
                            PutState = strikeInfo.Price > UnderlyingPrice ? OptionState.ITM : OptionState.OTM
                        });
                    });
                }

                await Application.Current.Dispatcher.InvokeAsync(CalculateOptionChainAggregates);

                if (newSubscriptions.Any())
                {
                    await _webSocketClient.SubscribeToInstrumentsAsync(newSubscriptions, 17);
                }
                await UpdateStatusAsync($"Option chain for {SelectedIndex?.Name ?? "Selected Index"} - {SelectedExpiry} loaded.");
            }
            catch (DhanApiException ex)
            {
                await UpdateStatusAsync($"API Error loading option chain: {ex.Message}");
            }
        }

        private async Task LoadDashboardOptionsForIndexAsync(DashboardInstrument indexInstrument, decimal livePrice)
        {
            try
            {
                await UpdateStatusAsync($"Loading options for {indexInstrument.DisplayName}...");

                if (!_nearestExpiryDates.TryGetValue(indexInstrument.Symbol, out var nearestExpiry) || string.IsNullOrEmpty(nearestExpiry))
                {
                    Debug.WriteLine($"[DashboardOptions] No pre-loaded expiry found for {indexInstrument.Symbol}.");
                    return;
                }

                int step = GetStrikePriceStep(indexInstrument.UnderlyingSymbol);
                decimal atmStrike = Math.Round(livePrice / step) * step;

                var strikesToLoad = new List<decimal>();
                for (int i = -5; i <= 5; i++)
                {
                    strikesToLoad.Add(atmStrike + (i * step));
                }

                var newOptionInstruments = new List<DashboardInstrument>();
                var newSubscriptions = new Dictionary<string, int>();
                int optionSegmentId = (indexInstrument.ExchId == "BSE") ? GetSegmentIdFromName("BSE_FNO") : GetSegmentIdFromName("NSE_FNO");
                string scripMasterUnderlying = GetUnderlyingSymbolForScripMaster(indexInstrument.DisplayName);

                DateTime expiryDate = DateTime.ParseExact(nearestExpiry, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                foreach (var strike in strikesToLoad)
                {
                    var ceInfo = _scripMasterService.FindOptionScripInfo(scripMasterUnderlying, expiryDate, strike, "CE");
                    if (ceInfo != null)
                    {
                        var inst = new DashboardInstrument
                        {
                            Symbol = ceInfo.SemInstrumentName,
                            DisplayName = ceInfo.SemInstrumentName,
                            SecurityId = ceInfo.SecurityId,
                            FeedType = FeedTypeQuote,
                            SegmentId = optionSegmentId,
                            UnderlyingSymbol = scripMasterUnderlying,
                            InstrumentType = ceInfo.InstrumentType,
                            StrikePrice = ceInfo.StrikePrice,
                            OptionType = ceInfo.OptionType,
                            ExpiryDate = ceInfo.ExpiryDate
                        };
                        newOptionInstruments.Add(inst);
                        newSubscriptions[inst.SecurityId] = inst.SegmentId;
                    }

                    var peInfo = _scripMasterService.FindOptionScripInfo(scripMasterUnderlying, expiryDate, strike, "PE");
                    if (peInfo != null)
                    {
                        var inst = new DashboardInstrument
                        {
                            Symbol = peInfo.SemInstrumentName,
                            DisplayName = peInfo.SemInstrumentName,
                            SecurityId = peInfo.SecurityId,
                            FeedType = FeedTypeQuote,
                            SegmentId = optionSegmentId,
                            UnderlyingSymbol = scripMasterUnderlying,
                            InstrumentType = peInfo.InstrumentType,
                            StrikePrice = peInfo.StrikePrice,
                            OptionType = peInfo.OptionType,
                            ExpiryDate = ceInfo.ExpiryDate
                        };
                        newOptionInstruments.Add(inst);
                        newSubscriptions[inst.SecurityId] = inst.SegmentId;
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var inst in newOptionInstruments)
                    {
                        if (!Dashboard.MonitoredInstruments.Any(i => i.SecurityId == inst.SecurityId))
                        {
                            Dashboard.MonitoredInstruments.Add(inst);
                        }
                    }
                });

                if (newSubscriptions.Any())
                {
                    await _webSocketClient.SubscribeToInstrumentsAsync(newSubscriptions, 17);
                }
                await UpdateStatusAsync($"Options for {indexInstrument.DisplayName} loaded.");
            }
            catch (Exception ex)
            {
                await UpdateStatusAsync($"Error loading dashboard options for {indexInstrument.DisplayName}: {ex.Message}");
                _dashboardOptionsLoadedFor.Remove(indexInstrument.SecurityId);
            }
        }

        private int GetStrikePriceStep(string underlyingSymbol)
        {
            string upperSymbol = underlyingSymbol.ToUpperInvariant();
            if (upperSymbol.Contains("SENSEX") || (upperSymbol.Contains("NIFTY") && upperSymbol.Contains("BANK")))
            {
                return 100;
            }
            return 50;
        }

        private async Task LoadInitialOptionChainsAsync()
        {
            if (!await _ivCacheSemaphore.WaitAsync(0)) return;

            try
            {
                await UpdateStatusAsync("Refreshing IV cache...");
                _optionChainCache.Clear();

                var indicesToLoad = Indices.Where(i => i.Name == "Nifty 50" || i.Name == "Nifty Bank" || i.Name == "Sensex").ToList();

                foreach (var indexInfo in indicesToLoad)
                {
                    try
                    {
                        if (!_nearestExpiryDates.TryGetValue(indexInfo.Symbol, out var expiryDateStr) || string.IsNullOrEmpty(expiryDateStr))
                        {
                            continue;
                        }

                        var optionChainResponse = await _apiClient.GetOptionChainAsync(indexInfo.ScripId, MapSegmentForOptionChainApi(indexInfo.Segment), expiryDateStr);

                        if (optionChainResponse?.Data?.OptionChain != null)
                        {
                            string scripMasterUnderlying = GetUnderlyingSymbolForScripMaster(indexInfo.Name);
                            DateTime expiryDate = DateTime.ParseExact(expiryDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                            foreach (var strikeKvp in optionChainResponse.Data.OptionChain)
                            {
                                if (!decimal.TryParse(strikeKvp.Key, NumberStyles.Any, CultureInfo.InvariantCulture, out var strikePrice))
                                {
                                    continue;
                                }

                                var strikeData = strikeKvp.Value;

                                if (strikeData.CallOption != null)
                                {
                                    var scripInfo = _scripMasterService.FindOptionScripInfo(scripMasterUnderlying, expiryDate, strikePrice, "CE");
                                    if (scripInfo != null && !string.IsNullOrEmpty(scripInfo.SecurityId))
                                    {
                                        _optionChainCache[scripInfo.SecurityId] = strikeData.CallOption;
                                    }
                                }

                                if (strikeData.PutOption != null)
                                {
                                    var scripInfo = _scripMasterService.FindOptionScripInfo(scripMasterUnderlying, expiryDate, strikePrice, "PE");
                                    if (scripInfo != null && !string.IsNullOrEmpty(scripInfo.SecurityId))
                                    {
                                        _optionChainCache[scripInfo.SecurityId] = strikeData.PutOption;
                                    }
                                }
                            }
                        }
                        await Task.Delay(3100);
                    }
                    catch (DhanApiException ex)
                    {
                        Debug.WriteLine($"[IV_CACHE] Failed to load option chain for {indexInfo.Name}: {ex.Message}");
                    }
                }

                await Application.Current.Dispatcher.InvokeAsync(() => {
                    foreach (var instrument in Dashboard.MonitoredInstruments)
                    {
                        if (_optionChainCache.TryGetValue(instrument.SecurityId, out var cachedData))
                        {
                            instrument.ImpliedVolatility = cachedData.ImpliedVolatility;
                        }

                        decimal underlyingLtp = 0;
                        if (instrument.InstrumentType.StartsWith("OPT") || instrument.InstrumentType.StartsWith("FUT"))
                        {
                            var underlyingInstrument = FindUnderlyingInstrument(instrument);
                            if (underlyingInstrument != null)
                            {
                                underlyingLtp = underlyingInstrument.LTP;
                            }
                        }
                        else
                        {
                            underlyingLtp = instrument.LTP;
                        }

                        if (underlyingLtp > 0)
                        {
                            _analysisService.OnInstrumentDataReceived(instrument, underlyingLtp);
                        }
                    }
                });

                await UpdateStatusAsync("IV cache refresh complete.");
            }
            finally
            {
                _ivCacheSemaphore.Release();
            }
        }


        private async Task RefreshOptionChainDataAsync()
        {
            if (SelectedIndex == null || string.IsNullOrWhiteSpace(SelectedExpiry)) return;

            await _optionChainLoadSemaphore.WaitAsync();
            try
            {
                string apiSecurityId = SelectedIndex.ScripId;
                string apiSegment = MapSegmentForOptionChainApi(SelectedIndex.Segment);

                var optionChainResponse = await _apiClient.GetOptionChainAsync(apiSecurityId, apiSegment, SelectedExpiry);
                if (optionChainResponse?.Data?.OptionChain != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var spotIndexInstrument = Dashboard.MonitoredInstruments.FirstOrDefault(i => i.SecurityId == SelectedIndex.ScripId);
                        if (spotIndexInstrument != null)
                        {
                            UnderlyingPrice = spotIndexInstrument.LTP;
                            UnderlyingPreviousClose = spotIndexInstrument.Close;
                        }

                        var newChainData = optionChainResponse.Data.OptionChain;
                        var atmStrike = newChainData.Select(kvp => decimal.TryParse(kvp.Key, out var p) ? (decimal?)p : null)
                                                    .Where(p => p.HasValue)
                                                    .OrderBy(p => Math.Abs(p.Value - UnderlyingPrice))
                                                    .FirstOrDefault();

                        foreach (var rowToUpdate in OptionChainRows)
                        {
                            if (newChainData.TryGetValue(rowToUpdate.StrikePrice.ToString("F2"), out var strikeData) ||
                               newChainData.TryGetValue(rowToUpdate.StrikePrice.ToString("F0"), out strikeData) ||
                               newChainData.TryGetValue(rowToUpdate.StrikePrice.ToString(CultureInfo.InvariantCulture), out strikeData))
                            {
                                if (rowToUpdate.CallOption != null && strikeData.CallOption != null)
                                {
                                    rowToUpdate.CallOption.OI = strikeData.CallOption.OpenInterest;
                                    rowToUpdate.CallOption.OiChange = strikeData.CallOption.OiChange;
                                    rowToUpdate.CallOption.Volume = strikeData.CallOption.Volume;
                                    rowToUpdate.CallOption.IV = strikeData.CallOption.ImpliedVolatility;
                                    rowToUpdate.CallOption.Delta = strikeData.CallOption.Greeks?.Delta ?? 0;
                                    // --- ADDED: Update new greeks on refresh ---
                                    rowToUpdate.CallOption.Gamma = strikeData.CallOption.Greeks?.Gamma ?? 0;
                                    rowToUpdate.CallOption.Theta = strikeData.CallOption.Greeks?.Theta ?? 0;
                                    rowToUpdate.CallOption.Vega = strikeData.CallOption.Greeks?.Vega ?? 0;
                                    rowToUpdate.CallOption.PreviousClose = strikeData.CallOption.PreviousClose;
                                }

                                if (rowToUpdate.PutOption != null && strikeData.PutOption != null)
                                {
                                    rowToUpdate.PutOption.OI = strikeData.PutOption.OpenInterest;
                                    rowToUpdate.PutOption.OiChange = strikeData.PutOption.OiChange;
                                    rowToUpdate.PutOption.Volume = strikeData.PutOption.Volume;
                                    rowToUpdate.PutOption.IV = strikeData.PutOption.ImpliedVolatility;
                                    rowToUpdate.PutOption.Delta = strikeData.PutOption.Greeks?.Delta ?? 0;
                                    // --- ADDED: Update new greeks on refresh ---
                                    rowToUpdate.PutOption.Gamma = strikeData.PutOption.Greeks?.Gamma ?? 0;
                                    rowToUpdate.PutOption.Theta = strikeData.PutOption.Greeks?.Theta ?? 0;
                                    rowToUpdate.PutOption.Vega = strikeData.PutOption.Greeks?.Vega ?? 0;
                                    rowToUpdate.PutOption.PreviousClose = strikeData.PutOption.PreviousClose;
                                }

                                if (atmStrike.HasValue)
                                {
                                    rowToUpdate.IsAtm = rowToUpdate.StrikePrice == atmStrike.Value;
                                    rowToUpdate.CallState = rowToUpdate.StrikePrice < UnderlyingPrice ? OptionState.ITM : OptionState.OTM;
                                    rowToUpdate.PutState = rowToUpdate.StrikePrice > UnderlyingPrice ? OptionState.ITM : OptionState.OTM;
                                }
                            }
                        }

                        CalculateOptionChainAggregates();
                    });
                }
            }
            catch (DhanApiException ex)
            {
                Debug.WriteLine($"[OptionChainRefresh] API error during periodic refresh: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OptionChainRefresh] An unexpected error occurred during periodic refresh: {ex.Message}");
            }
            finally
            {
                _optionChainLoadSemaphore.Release();
            }
        }

        private string GetUnderlyingSymbolForScripMaster(string displayName)
        {
            return displayName switch
            {
                "Nifty 50" => "NIFTY",
                "Nifty Bank" => "BANKNIFTY",
                "Sensex" => "SENSEX",
                _ => displayName
            };
        }

        private (OptionDetails, string) MapToOptionDetails(DhanApi.Models.OptionData? apiData, decimal strikePrice, string optionType, string underlyingSymbol, string expiryDateStr)
        {
            if (apiData == null || string.IsNullOrEmpty(underlyingSymbol) || string.IsNullOrEmpty(expiryDateStr))
            {
                return (new OptionDetails(), string.Empty);
            }

            DateTime.TryParseExact(expiryDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiryDate);

            var scripInfo = _scripMasterService.FindOptionScripInfo(underlyingSymbol, expiryDate, strikePrice, optionType);

            var details = new OptionDetails
            {
                SecurityId = scripInfo?.SecurityId ?? string.Empty,
                LTP = apiData.LastPrice,
                PreviousClose = apiData.PreviousClose,
                IV = apiData.ImpliedVolatility,
                OI = apiData.OpenInterest,
                OiChange = apiData.OiChange,
                OiChangePercent = apiData.OiChangePercent,
                Volume = apiData.Volume,
                Delta = apiData.Greeks?.Delta ?? 0,
                // --- ADDED: Map the new greeks from the API response to the core model ---
                Gamma = apiData.Greeks?.Gamma ?? 0,
                Theta = apiData.Greeks?.Theta ?? 0,
                Vega = apiData.Greeks?.Vega ?? 0
            };

            return (details, scripInfo?.SemInstrumentName ?? string.Empty);
        }

        private void CalculateOptionChainAggregates()
        {
            if (!OptionChainRows.Any()) return;

            long runningCallOi = 0; long runningPutOi = 0;
            long runningCallVolume = 0; long runningPutVolume = 0;

            foreach (var row in OptionChainRows)
            {
                if (row.CallOption != null) { runningCallOi += (long)row.CallOption.OI; runningCallVolume += row.CallOption.Volume; }
                if (row.PutOption != null) { runningPutOi += (long)row.PutOption.OI; runningPutVolume += row.PutOption.Volume; }
            }

            TotalCallOi = runningCallOi; TotalPutOi = runningPutOi;
            TotalCallVolume = runningCallVolume; TotalPutVolume = runningPutVolume;
            PcrOi = (TotalCallOi > 0) ? (decimal)TotalPutOi / TotalCallOi : 0;

            long maxCallOi = OptionChainRows.Any(r => r.CallOption != null) ? OptionChainRows.Max(r => (long)(r.CallOption?.OI ?? 0)) : 0;
            long maxPutOi = OptionChainRows.Any(r => r.PutOption != null) ? OptionChainRows.Max(r => (long)(r.PutOption?.OI ?? 0)) : 0;
            MaxOi = Math.Max(maxCallOi, maxPutOi);

            decimal maxCallOiChange = OptionChainRows.Any(r => r.CallOption != null) ? OptionChainRows.Max(r => Math.Abs(r.CallOption?.OiChange ?? 0)) : 0;
            decimal maxPutOiChange = OptionChainRows.Any(r => r.PutOption != null) ? OptionChainRows.Max(r => Math.Abs(r.PutOption?.OiChange ?? 0)) : 0;
            MaxOiChange = Math.Max(maxCallOiChange, maxPutOiChange);
        }

        public Task UpdateStatusAsync(string message)
        {
            return Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = message;
                OnPropertyChanged(nameof(StatusMessage));
            }).Task ?? Task.CompletedTask;
        }

        private void RebuildDashboardMap()
        {
            _dashboardInstrumentMap = Dashboard.MonitoredInstruments.ToDictionary(i => i.SecurityId);
        }

        private void RebuildPositionsMap()
        {
            _openPositionsMap = Portfolio.OpenPositions.ToDictionary(p => p.SecurityId);
        }

        private DashboardInstrument? FindUnderlyingInstrument(DashboardInstrument derivative)
        {
            return Dashboard.MonitoredInstruments.FirstOrDefault(i =>
            {
                if (i.InstrumentType != "INDEX" && i.InstrumentType != "EQUITY")
                    return false;

                var derivativeUnderlying = derivative.UnderlyingSymbol.ToUpperInvariant();
                var potentialUnderlyingSymbol = i.Symbol.ToUpperInvariant();

                if (potentialUnderlyingSymbol == "NIFTY 50" && derivativeUnderlying == "NIFTY") return true;
                if (potentialUnderlyingSymbol == "NIFTY BANK" && derivativeUnderlying == "BANKNIFTY") return true;
                if (potentialUnderlyingSymbol == "SENSEX" && derivativeUnderlying == "SENSEX") return true;

                return potentialUnderlyingSymbol == derivativeUnderlying;
            });
        }
        #endregion

        #region Boilerplate

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void Dispose()
        {
            _webSocketClient?.Dispose();
            _optionChainRefreshTimer?.Dispose();
            _ivRefreshTimer?.Dispose();
            _optionChainLoadSemaphore?.Dispose();
            _ivCacheSemaphore?.Dispose();
            _uiUpdateTimer?.Dispose();

            _performanceService?.Cleanup();

            Settings.SettingsSaved -= Settings_SettingsSaved;
            if (Portfolio != null) Portfolio.PropertyChanged -= Portfolio_PropertyChanged;

            _historicalIvService?.SaveDatabase();
            _analysisService?.SaveMarketProfileDatabase();
            _analysisService?.SaveIndicatorStates();
        }
        #endregion
    }
}
