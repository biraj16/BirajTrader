// In TradingConsole.Wpf/Services/SignalLoggerService.cs
using System;
using System.IO;
using System.Text;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Provides a service to log high-conviction trade signals to a daily file for review.
    /// </summary>
    public class SignalLoggerService
    {
        private readonly string _logFilePath;
        private static readonly object _lock = new object();

        public SignalLoggerService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole", "Logs");
            Directory.CreateDirectory(appFolderPath);

            // Create a new log file for each day.
            _logFilePath = Path.Combine(appFolderPath, $"trade_signals_{DateTime.Now:yyyy-MM-dd}.log");
        }

        /// <summary>
        /// Logs the details of a generated trade signal to the daily log file.
        /// </summary>
        /// <param name="result">The analysis result containing the signal information.</param>
        public void LogSignal(AnalysisResult result)
        {
            try
            {
                var logBuilder = new StringBuilder();
                var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

                logBuilder.AppendLine("------------------------------------------------------");
                logBuilder.AppendLine($"[SIGNAL GENERATED] at {istNow:yyyy-MM-dd HH:mm:ss.fff K}");
                logBuilder.AppendLine("------------------------------------------------------");
                logBuilder.AppendLine($"Instrument: {result.Symbol}");
                logBuilder.AppendLine($"Signal: {result.FinalTradeSignal}");
                logBuilder.AppendLine($"Conviction Score: {result.ConvictionScore}");
                logBuilder.AppendLine();

                logBuilder.AppendLine("Bullish Drivers:");
                if (result.BullishDrivers.Count > 0)
                {
                    foreach (var driver in result.BullishDrivers)
                    {
                        logBuilder.AppendLine($"  - {driver}");
                    }
                }
                else
                {
                    logBuilder.AppendLine("  - None");
                }

                logBuilder.AppendLine();
                logBuilder.AppendLine("Bearish Drivers:");
                if (result.BearishDrivers.Count > 0)
                {
                    foreach (var driver in result.BearishDrivers)
                    {
                        logBuilder.AppendLine($"  - {driver}");
                    }
                }
                else
                {
                    logBuilder.AppendLine("  - None");
                }
                logBuilder.AppendLine("------------------------------------------------------");
                logBuilder.AppendLine();

                // Use a lock to prevent file access conflicts if signals are generated in very quick succession.
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, logBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                // Log to debug console if file logging fails.
                System.Diagnostics.Debug.WriteLine($"[SignalLoggerService] FAILED to write to log file: {ex.Message}");
            }
        }
    }
}
