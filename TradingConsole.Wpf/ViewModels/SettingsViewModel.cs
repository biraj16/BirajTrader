// In TradingConsole.Wpf/ViewModels/SettingsViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TradingConsole.Core.Models;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;
        private readonly MainViewModel _mainViewModel;

        public ReadOnlyCollection<string> MonitoredSymbols => new ReadOnlyCollection<string>(_settings.MonitoredSymbols);

        #region Freeze Quantities
        private int _niftyFreezeQuantity;
        public int NiftyFreezeQuantity { get => _niftyFreezeQuantity; set { _niftyFreezeQuantity = value; OnPropertyChanged(); } }
        private int _bankNiftyFreezeQuantity;
        public int BankNiftyFreezeQuantity { get => _bankNiftyFreezeQuantity; set { _bankNiftyFreezeQuantity = value; OnPropertyChanged(); } }
        private int _finNiftyFreezeQuantity;
        public int FinNiftyFreezeQuantity { get => _finNiftyFreezeQuantity; set { _finNiftyFreezeQuantity = value; OnPropertyChanged(); } }
        private int _sensexFreezeQuantity;
        public int SensexFreezeQuantity { get => _sensexFreezeQuantity; set { _sensexFreezeQuantity = value; OnPropertyChanged(); } }
        #endregion

        #region EMA Lengths
        private int _shortEmaLength;
        public int ShortEmaLength { get => _shortEmaLength; set { if (_shortEmaLength != value) { _shortEmaLength = value; OnPropertyChanged(); } } }
        private int _longEmaLength;
        public int LongEmaLength { get => _longEmaLength; set { if (_longEmaLength != value) { _longEmaLength = value; OnPropertyChanged(); } } }
        #endregion

        #region Volatility (ATR) Settings
        private int _atrPeriod;
        public int AtrPeriod { get => _atrPeriod; set { if (_atrPeriod != value) { _atrPeriod = value; OnPropertyChanged(); } } }
        private int _atrSmaPeriod;
        public int AtrSmaPeriod { get => _atrSmaPeriod; set { if (_atrSmaPeriod != value) { _atrSmaPeriod = value; OnPropertyChanged(); } } }
        #endregion

        #region Analysis Parameters
        private int _rsiPeriod;
        public int RsiPeriod { get => _rsiPeriod; set { if (_rsiPeriod != value) { _rsiPeriod = value; OnPropertyChanged(); } } }
        private int _rsiDivergenceLookback;
        public int RsiDivergenceLookback { get => _rsiDivergenceLookback; set { if (_rsiDivergenceLookback != value) { _rsiDivergenceLookback = value; OnPropertyChanged(); } } }
        private int _volumeHistoryLength;
        public int VolumeHistoryLength { get => _volumeHistoryLength; set { if (_volumeHistoryLength != value) { _volumeHistoryLength = value; OnPropertyChanged(); } } }
        private double _volumeBurstMultiplier;
        public double VolumeBurstMultiplier { get => _volumeBurstMultiplier; set { if (_volumeBurstMultiplier != value) { _volumeBurstMultiplier = value; OnPropertyChanged(); } } }
        private int _ivHistoryLength;
        public int IvHistoryLength { get => _ivHistoryLength; set { if (_ivHistoryLength != value) { _ivHistoryLength = value; OnPropertyChanged(); } } }
        private decimal _ivSpikeThreshold;
        public decimal IvSpikeThreshold { get => _ivSpikeThreshold; set { if (_ivSpikeThreshold != value) { _ivSpikeThreshold = value; OnPropertyChanged(); } } }
        private int _obvMovingAveragePeriod;
        public int ObvMovingAveragePeriod { get => _obvMovingAveragePeriod; set { if (_obvMovingAveragePeriod != value) { _obvMovingAveragePeriod = value; OnPropertyChanged(); } } }

        private decimal _vwapUpperBandMultiplier;
        public decimal VwapUpperBandMultiplier { get => _vwapUpperBandMultiplier; set { if (_vwapUpperBandMultiplier != value) { _vwapUpperBandMultiplier = value; OnPropertyChanged(); } } }
        private decimal _vwapLowerBandMultiplier;
        public decimal VwapLowerBandMultiplier { get => _vwapLowerBandMultiplier; set { if (_vwapLowerBandMultiplier != value) { _vwapLowerBandMultiplier = value; OnPropertyChanged(); } } }

        // --- ADDED: Property for the new Gamma setting ---
        private decimal _atmGammaThreshold;
        public decimal AtmGammaThreshold { get => _atmGammaThreshold; set { if (_atmGammaThreshold != value) { _atmGammaThreshold = value; OnPropertyChanged(); } } }
        #endregion

        #region Custom Index Levels
        private decimal _niftyNoTradeUpper;
        public decimal NiftyNoTradeUpper { get => _niftyNoTradeUpper; set { _niftyNoTradeUpper = value; OnPropertyChanged(); } }
        private decimal _niftyNoTradeLower;
        public decimal NiftyNoTradeLower { get => _niftyNoTradeLower; set { _niftyNoTradeLower = value; OnPropertyChanged(); } }
        private decimal _niftySupport;
        public decimal NiftySupport { get => _niftySupport; set { _niftySupport = value; OnPropertyChanged(); } }
        private decimal _niftyResistance;
        public decimal NiftyResistance { get => _niftyResistance; set { _niftyResistance = value; OnPropertyChanged(); } }
        private decimal _niftyThreshold;
        public decimal NiftyThreshold { get => _niftyThreshold; set { _niftyThreshold = value; OnPropertyChanged(); } }

        private decimal _bankniftyNoTradeUpper;
        public decimal BankniftyNoTradeUpper { get => _bankniftyNoTradeUpper; set { _bankniftyNoTradeUpper = value; OnPropertyChanged(); } }
        private decimal _bankniftyNoTradeLower;
        public decimal BankniftyNoTradeLower { get => _bankniftyNoTradeLower; set { _bankniftyNoTradeLower = value; OnPropertyChanged(); } }
        private decimal _bankniftySupport;
        public decimal BankniftySupport { get => _bankniftySupport; set { _bankniftySupport = value; OnPropertyChanged(); } }
        private decimal _bankniftyResistance;
        public decimal BankniftyResistance { get => _bankniftyResistance; set { _bankniftyResistance = value; OnPropertyChanged(); } }
        private decimal _bankniftyThreshold;
        public decimal BankniftyThreshold { get => _bankniftyThreshold; set { _bankniftyThreshold = value; OnPropertyChanged(); } }

        private decimal _sensexNoTradeUpper;
        public decimal SensexNoTradeUpper { get => _sensexNoTradeUpper; set { _sensexNoTradeUpper = value; OnPropertyChanged(); } }
        private decimal _sensexNoTradeLower;
        public decimal SensexNoTradeLower { get => _sensexNoTradeLower; set { _sensexNoTradeLower = value; OnPropertyChanged(); } }
        private decimal _sensexSupport;
        public decimal SensexSupport { get => _sensexSupport; set { _sensexSupport = value; OnPropertyChanged(); } }
        private decimal _sensexResistance;
        public decimal SensexResistance { get => _sensexResistance; set { _sensexResistance = value; OnPropertyChanged(); } }
        private decimal _sensexThreshold;
        public decimal SensexThreshold { get => _sensexThreshold; set { _sensexThreshold = value; OnPropertyChanged(); } }
        #endregion

        #region Kill Switch Settings
        private bool _isAutoKillSwitchEnabled;
        public bool IsAutoKillSwitchEnabled { get => _isAutoKillSwitchEnabled; set { if (_isAutoKillSwitchEnabled != value) { _isAutoKillSwitchEnabled = value; OnPropertyChanged(); } } }

        private decimal _maxDailyLossLimit;
        public decimal MaxDailyLossLimit { get => _maxDailyLossLimit; set { if (_maxDailyLossLimit != value) { _maxDailyLossLimit = value; OnPropertyChanged(); } } }

        public bool IsKillSwitchActive => _mainViewModel.IsKillSwitchActive;
        #endregion

        #region Notification Settings
        private bool _isTelegramNotificationEnabled;
        public bool IsTelegramNotificationEnabled { get => _isTelegramNotificationEnabled; set { if (_isTelegramNotificationEnabled != value) { _isTelegramNotificationEnabled = value; OnPropertyChanged(); } } }

        private string? _telegramBotToken;
        public string? TelegramBotToken { get => _telegramBotToken; set { if (_telegramBotToken != value) { _telegramBotToken = value; OnPropertyChanged(); } } }

        private string? _telegramChatId;
        public string? TelegramChatId { get => _telegramChatId; set { if (_telegramChatId != value) { _telegramChatId = value; OnPropertyChanged(); } } }
        #endregion

        #region Automation Settings
        private AutomationSettings _automationSettings = new AutomationSettings();
        public AutomationSettings AutomationSettings
        {
            get => _automationSettings;
            set { _automationSettings = value; OnPropertyChanged(); }
        }
        #endregion

        private StrategySettings _strategy = new StrategySettings();
        public StrategySettings Strategy { get => _strategy; set { _strategy = value; OnPropertyChanged(); } }


        public ObservableCollection<DateTime> MarketHolidays { get; set; }

        private DateTime? _newHoliday;
        public DateTime? NewHoliday
        {
            get => _newHoliday;
            set { _newHoliday = value; OnPropertyChanged(); }
        }

        public ICommand AddHolidayCommand { get; }
        public ICommand RemoveHolidayCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ActivateKillSwitchCommand { get; }
        public event EventHandler? SettingsSaved;

        public SettingsViewModel(SettingsService settingsService, MainViewModel mainViewModel)
        {
            _settingsService = settingsService;
            _mainViewModel = mainViewModel;
            _settings = _settingsService.LoadSettings();

            _mainViewModel.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(MainViewModel.IsKillSwitchActive))
                {
                    OnPropertyChanged(nameof(IsKillSwitchActive));
                }
            };

            MarketHolidays = new ObservableCollection<DateTime>();

            LoadSettingsIntoViewModel();

            SaveSettingsCommand = new RelayCommand(ExecuteSaveSettings);
            AddHolidayCommand = new RelayCommand(ExecuteAddHoliday, CanExecuteAddHoliday);
            RemoveHolidayCommand = new RelayCommand(ExecuteRemoveHoliday);
            ActivateKillSwitchCommand = new RelayCommand(async _ => await _mainViewModel.EngageKillSwitchAsync(), _ => !IsKillSwitchActive);
        }

        private void LoadSettingsIntoViewModel()
        {
            NiftyFreezeQuantity = _settings.FreezeQuantities.GetValueOrDefault("NIFTY", 1800);
            BankNiftyFreezeQuantity = _settings.FreezeQuantities.GetValueOrDefault("BANKNIFTY", 900);
            FinNiftyFreezeQuantity = _settings.FreezeQuantities.GetValueOrDefault("FINNIFTY", 1800);
            SensexFreezeQuantity = _settings.FreezeQuantities.GetValueOrDefault("SENSEX", 1000);

            ShortEmaLength = _settings.ShortEmaLength;
            LongEmaLength = _settings.LongEmaLength;

            AtrPeriod = _settings.AtrPeriod;
            AtrSmaPeriod = _settings.AtrSmaPeriod;

            RsiPeriod = _settings.RsiPeriod;
            RsiDivergenceLookback = _settings.RsiDivergenceLookback;
            VolumeHistoryLength = _settings.VolumeHistoryLength;
            VolumeBurstMultiplier = _settings.VolumeBurstMultiplier;
            IvHistoryLength = _settings.IvHistoryLength;
            IvSpikeThreshold = _settings.IvSpikeThreshold;
            ObvMovingAveragePeriod = _settings.ObvMovingAveragePeriod;

            VwapUpperBandMultiplier = _settings.VwapUpperBandMultiplier;
            VwapLowerBandMultiplier = _settings.VwapLowerBandMultiplier;

            // --- ADDED: Load the new Gamma setting ---
            AtmGammaThreshold = _settings.AtmGammaThreshold;


            var niftyLevels = _settings.CustomIndexLevels.GetValueOrDefault("NIFTY", new IndexLevels());
            NiftyNoTradeUpper = niftyLevels.NoTradeUpperBand;
            NiftyNoTradeLower = niftyLevels.NoTradeLowerBand;
            NiftySupport = niftyLevels.SupportLevel;
            NiftyResistance = niftyLevels.ResistanceLevel;
            NiftyThreshold = niftyLevels.Threshold;

            var bankniftyLevels = _settings.CustomIndexLevels.GetValueOrDefault("BANKNIFTY", new IndexLevels());
            BankniftyNoTradeUpper = bankniftyLevels.NoTradeUpperBand;
            BankniftyNoTradeLower = bankniftyLevels.NoTradeLowerBand;
            BankniftySupport = bankniftyLevels.SupportLevel;
            BankniftyResistance = bankniftyLevels.ResistanceLevel;
            BankniftyThreshold = bankniftyLevels.Threshold;

            var sensexLevels = _settings.CustomIndexLevels.GetValueOrDefault("SENSEX", new IndexLevels());
            SensexNoTradeUpper = sensexLevels.NoTradeUpperBand;
            SensexNoTradeLower = sensexLevels.NoTradeLowerBand;
            SensexSupport = sensexLevels.SupportLevel;
            SensexResistance = sensexLevels.ResistanceLevel;
            SensexThreshold = sensexLevels.Threshold;

            IsAutoKillSwitchEnabled = _settings.IsAutoKillSwitchEnabled;
            MaxDailyLossLimit = _settings.MaxDailyLossLimit;

            Strategy = _settings.Strategy ?? new StrategySettings();

            // --- ADDED: Load Automation Settings ---
            AutomationSettings = _settings.AutomationSettings ?? new AutomationSettings();

            MarketHolidays.Clear();
            var loadedHolidays = _settings.MarketHolidays ?? new List<DateTime>();
            var sortedHolidays = loadedHolidays.OrderBy(d => d.Date);
            foreach (var holiday in sortedHolidays)
            {
                MarketHolidays.Add(holiday);
            }
            NewHoliday = DateTime.Today;

            // --- NEW: Load Notification settings ---
            IsTelegramNotificationEnabled = _settings.IsTelegramNotificationEnabled;
            TelegramBotToken = _settings.TelegramBotToken;
            TelegramChatId = _settings.TelegramChatId;
        }

        private void ExecuteSaveSettings(object? parameter)
        {
            _settings.FreezeQuantities["NIFTY"] = NiftyFreezeQuantity;
            _settings.FreezeQuantities["BANKNIFTY"] = BankNiftyFreezeQuantity;
            _settings.FreezeQuantities["FINNIFTY"] = FinNiftyFreezeQuantity;
            _settings.FreezeQuantities["SENSEX"] = SensexFreezeQuantity;

            _settings.ShortEmaLength = ShortEmaLength;
            _settings.LongEmaLength = LongEmaLength;

            _settings.AtrPeriod = AtrPeriod;
            _settings.AtrSmaPeriod = AtrSmaPeriod;

            _settings.RsiPeriod = RsiPeriod;
            _settings.RsiDivergenceLookback = RsiDivergenceLookback;
            _settings.VolumeHistoryLength = VolumeHistoryLength;
            _settings.VolumeBurstMultiplier = VolumeBurstMultiplier;
            _settings.IvHistoryLength = IvHistoryLength;
            _settings.IvSpikeThreshold = IvSpikeThreshold;
            _settings.ObvMovingAveragePeriod = ObvMovingAveragePeriod;

            _settings.VwapUpperBandMultiplier = VwapUpperBandMultiplier;
            _settings.VwapLowerBandMultiplier = VwapLowerBandMultiplier;

            // --- ADDED: Save the new Gamma setting ---
            _settings.AtmGammaThreshold = AtmGammaThreshold;

            _settings.CustomIndexLevels["NIFTY"] = new IndexLevels
            {
                NoTradeUpperBand = NiftyNoTradeUpper,
                NoTradeLowerBand = NiftyNoTradeLower,
                SupportLevel = NiftySupport,
                ResistanceLevel = NiftyResistance,
                Threshold = NiftyThreshold
            };
            _settings.CustomIndexLevels["BANKNIFTY"] = new IndexLevels
            {
                NoTradeUpperBand = BankniftyNoTradeUpper,
                NoTradeLowerBand = BankniftyNoTradeLower,
                SupportLevel = BankniftySupport,
                ResistanceLevel = BankniftyResistance,
                Threshold = BankniftyThreshold
            };
            _settings.CustomIndexLevels["SENSEX"] = new IndexLevels
            {
                NoTradeUpperBand = SensexNoTradeUpper,
                NoTradeLowerBand = SensexNoTradeLower,
                SupportLevel = SensexSupport,
                ResistanceLevel = SensexResistance,
                Threshold = SensexThreshold
            };
            _settings.MarketHolidays = MarketHolidays.ToList();

            _settings.IsAutoKillSwitchEnabled = IsAutoKillSwitchEnabled;
            _settings.MaxDailyLossLimit = MaxDailyLossLimit;

            _settings.Strategy = this.Strategy;

            // --- ADDED: Save Automation Settings ---
            _settings.AutomationSettings = this.AutomationSettings;

            // --- NEW: Save Notification settings ---
            _settings.IsTelegramNotificationEnabled = IsTelegramNotificationEnabled;
            _settings.TelegramBotToken = TelegramBotToken;
            _settings.TelegramChatId = TelegramChatId;

            _settingsService.SaveSettings(_settings);
            MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        private bool CanExecuteAddHoliday(object? parameter)
        {
            return NewHoliday.HasValue;
        }

        private void ExecuteAddHoliday(object? parameter)
        {
            if (NewHoliday.HasValue)
            {
                var dateToAdd = NewHoliday.Value.Date;
                if (!MarketHolidays.Contains(dateToAdd))
                {
                    MarketHolidays.Add(dateToAdd);
                    var sorted = MarketHolidays.OrderBy(d => d.Date).ToList();
                    MarketHolidays.Clear();
                    foreach (var d in sorted) MarketHolidays.Add(d);
                }
            }
        }

        private void ExecuteRemoveHoliday(object? parameter)
        {
            if (parameter is DateTime holidayToRemove)
            {
                MarketHolidays.Remove(holidayToRemove);
            }
        }

        public IndexLevels? GetLevelsForIndex(string indexSymbol)
        {
            if (string.IsNullOrEmpty(indexSymbol)) return null;

            string key = indexSymbol.ToUpper();
            string settingsKey;

            if (key.Contains("BANK") && key.Contains("NIFTY"))
            {
                settingsKey = "BANKNIFTY";
            }
            else if (key.Contains("NIFTY"))
            {
                settingsKey = "NIFTY";
            }
            else if (key.Contains("SENSEX"))
            {
                settingsKey = "SENSEX";
            }
            else
            {
                return null;
            }

            return _settings.CustomIndexLevels.GetValueOrDefault(settingsKey);
        }


        public void AddMonitoredSymbol(string symbol)
        {
            if (!_settings.MonitoredSymbols.Any(s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                _settings.MonitoredSymbols.Add(symbol);
                _settingsService.SaveSettings(_settings);
                OnPropertyChanged(nameof(MonitoredSymbols));
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveMonitoredSymbol(string symbol)
        {
            var symbolToRemove = _settings.MonitoredSymbols.FirstOrDefault(s => s.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (symbolToRemove != null)
            {
                _settings.MonitoredSymbols.Remove(symbolToRemove);
                _settingsService.SaveSettings(_settings);
                OnPropertyChanged(nameof(MonitoredSymbols));
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ReplaceMonitoredSymbols(List<string> newSymbols)
        {
            _settings.MonitoredSymbols = newSymbols;
            _settingsService.SaveSettings(_settings);
            OnPropertyChanged(nameof(MonitoredSymbols));
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
