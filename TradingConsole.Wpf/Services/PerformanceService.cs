// TradingConsole.Wpf/Services/PerformanceService.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Represents a single point in the P&L history.
    /// </summary>
    public class PnlDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Pnl { get; set; }
    }

    /// <summary>
    /// Service to track and persist the user's intraday P&L performance.
    /// </summary>
    public class PerformanceService
    {
        private readonly PortfolioViewModel _portfolioViewModel;
        private readonly string _logFilePath;
        private static readonly object _fileLock = new object();

        public PerformanceService(PortfolioViewModel portfolioViewModel)
        {
            _portfolioViewModel = portfolioViewModel;

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole", "PerformanceLogs");
            Directory.CreateDirectory(appFolderPath);

            _logFilePath = Path.Combine(appFolderPath, $"pnl_history_{DateTime.Now:yyyy-MM-dd}.json");

            InitializeLogFile();

            _portfolioViewModel.PropertyChanged += OnPortfolioPropertyChanged;
        }

        /// <summary>
        /// Ensures a clean log file is ready for the current trading day.
        /// </summary>
        private void InitializeLogFile()
        {
            // This method is called at startup. If the file already exists, it means the app was restarted mid-day.
            // If it doesn't exist, it will be created on the first write.
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

            // Clear the log file if it's before market open on a new day.
            if (File.Exists(_logFilePath) && istNow.TimeOfDay < new TimeSpan(8, 0, 0))
            {
                try
                {
                    File.Delete(_logFilePath);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"[PerformanceService] Could not clear old log file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event from the PortfolioViewModel to capture P&L changes.
        /// </summary>
        private void OnPortfolioPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PortfolioViewModel.NetPnl))
            {
                var newDataPoint = new PnlDataPoint
                {
                    Timestamp = DateTime.Now,
                    Pnl = _portfolioViewModel.NetPnl
                };

                // Write to the file asynchronously to avoid blocking the UI thread.
                Task.Run(() => WriteDataPointToFile(newDataPoint));
            }
        }

        /// <summary>
        /// Appends a new P&L data point to the daily JSON log file.
        /// </summary>
        private void WriteDataPointToFile(PnlDataPoint dataPoint)
        {
            lock (_fileLock)
            {
                try
                {
                    // Append the JSON object as a new line. This is efficient for writing.
                    string jsonString = JsonSerializer.Serialize(dataPoint);
                    File.AppendAllText(_logFilePath, jsonString + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerformanceService] Error writing to P&L log file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reads all P&L data points from the current day's log file.
        /// </summary>
        public List<PnlDataPoint> LoadPnlHistory()
        {
            var history = new List<PnlDataPoint>();
            lock (_fileLock)
            {
                if (!File.Exists(_logFilePath))
                {
                    return history;
                }

                try
                {
                    var lines = File.ReadAllLines(_logFilePath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var dataPoint = JsonSerializer.Deserialize<PnlDataPoint>(line);
                        if (dataPoint != null)
                        {
                            history.Add(dataPoint);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerformanceService] Error reading P&L log file: {ex.Message}");
                }
            }
            return history;
        }

        /// <summary>
        /// Unsubscribes from the event to prevent memory leaks.
        /// </summary>
        public void Cleanup()
        {
            _portfolioViewModel.PropertyChanged -= OnPortfolioPropertyChanged;
        }
    }
}
