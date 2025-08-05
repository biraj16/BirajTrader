using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using TradingConsole.Core.Models;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Manages loading and saving historical Implied Volatility data.
    /// </summary>
    public class HistoricalIvService
    {
        private readonly string _filePath;
        private HistoricalIvDatabase _database;

        public HistoricalIvService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole");
            Directory.CreateDirectory(appFolderPath);
            _filePath = Path.Combine(appFolderPath, "historical_iv.json");

            _database = LoadDatabase();
        }

        private HistoricalIvDatabase LoadDatabase()
        {
            if (!File.Exists(_filePath))
            {
                return new HistoricalIvDatabase();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var db = JsonSerializer.Deserialize<HistoricalIvDatabase>(json);
                return db ?? new HistoricalIvDatabase();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HistoricalIvService] Error loading IV database: {ex.Message}");
                return new HistoricalIvDatabase(); // Return a fresh DB if file is corrupt
            }
        }

        public void SaveDatabase()
        {
            try
            {
                PruneOldRecords();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_database, options);
                File.WriteAllText(_filePath, json);
                Debug.WriteLine("[HistoricalIvService] Successfully saved IV database.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HistoricalIvService] Error saving IV database: {ex.Message}");
            }
        }

        public void RecordDailyIv(string key, decimal highIv, decimal lowIv)
        {
            if (string.IsNullOrEmpty(key) || highIv <= 0 || lowIv <= 0) return;

            if (!_database.Records.ContainsKey(key))
            {
                _database.Records[key] = new List<DailyIvRecord>();
            }

            var todayRecord = _database.Records[key].FirstOrDefault(r => r.Date.Date == DateTime.Today);

            if (todayRecord != null)
            {
                todayRecord.HighIv = Math.Max(todayRecord.HighIv, highIv);
                todayRecord.LowIv = Math.Min(todayRecord.LowIv, lowIv);
            }
            else
            {
                _database.Records[key].Add(new DailyIvRecord
                {
                    Date = DateTime.Today,
                    HighIv = highIv,
                    LowIv = lowIv
                });
            }
        }

        public (decimal high, decimal low) Get90DayIvRange(string key)
        {
            if (!_database.Records.ContainsKey(key))
            {
                return (0, 0);
            }

            var ninetyDaysAgo = DateTime.Today.AddDays(-90);
            var relevantRecords = _database.Records[key].Where(r => r.Date >= ninetyDaysAgo).ToList();

            if (!relevantRecords.Any())
            {
                return (0, 0);
            }

            var high = relevantRecords.Max(r => r.HighIv);
            var low = relevantRecords.Min(r => r.LowIv);

            return (high, low);
        }

        private void PruneOldRecords()
        {
            var ninetyDaysAgo = DateTime.Today.AddDays(-90);
            foreach (var key in _database.Records.Keys)
            {
                _database.Records[key].RemoveAll(r => r.Date < ninetyDaysAgo);
            }
        }
    }
}
