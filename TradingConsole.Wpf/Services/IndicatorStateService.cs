using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradingConsole.Core.Models;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Manages loading and saving the final state of technical indicators to a persistent file.
    /// This allows for accurate calculations from the start of a new trading session without a warm-up period.
    /// </summary>
    public class IndicatorStateService
    {
        private readonly string _filePath;
        private IndicatorStateDatabase _database;

        public IndicatorStateService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole");
            Directory.CreateDirectory(appFolderPath);
            _filePath = Path.Combine(appFolderPath, "indicator_states.json");

            _database = LoadDatabase();
        }

        /// <summary>
        /// Loads the indicator state database from the JSON file.
        /// </summary>
        private IndicatorStateDatabase LoadDatabase()
        {
            if (!File.Exists(_filePath))
            {
                return new IndicatorStateDatabase();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var db = JsonSerializer.Deserialize<IndicatorStateDatabase>(json);
                return db ?? new IndicatorStateDatabase();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IndicatorStateService] Error loading state database: {ex.Message}");
                // Return a fresh DB if the file is corrupt to prevent crashing.
                return new IndicatorStateDatabase();
            }
        }

        /// <summary>
        /// Saves the entire indicator state database to the JSON file.
        /// </summary>
        public void SaveDatabase()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_database, options);
                File.WriteAllText(_filePath, json);
                Debug.WriteLine("[IndicatorStateService] Successfully saved indicator state database.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IndicatorStateService] Error saving state database: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the saved indicator state for a specific instrument and timeframe.
        /// </summary>
        /// <param name="key">The composite key ("{SecurityId}_{TimeframeMinutes}").</param>
        /// <returns>The saved IndicatorState, or null if not found.</returns>
        public IndicatorState? GetState(string key)
        {
            _database.States.TryGetValue(key, out var state);
            return state;
        }

        /// <summary>
        /// Updates or adds an indicator state to the database.
        /// </summary>
        /// <param name="key">The composite key ("{SecurityId}_{TimeframeMinutes}").</param>
        /// <param name="state">The IndicatorState object to save.</param>
        public void UpdateState(string key, IndicatorState state)
        {
            _database.States[key] = state;
        }
    }
}
