// In TradingConsole.Wpf/Services/SettingsService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradingConsole.Core.Models;

namespace TradingConsole.Wpf.Services
{
    /// <summary>
    /// Manages loading and saving application settings to a persistent file.
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            // Define a standard location to store the settings file in the user's AppData folder.
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole");

            // Ensure the directory exists.
            Directory.CreateDirectory(appFolderPath);

            _settingsFilePath = Path.Combine(appFolderPath, "settings.json");
        }

        /// <summary>
        /// Loads the application settings from the JSON file.
        /// If the file doesn't exist, it returns a new instance with default values.
        /// </summary>
        /// <returns>An instance of AppSettings.</returns>
        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings(); // Return default settings
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                // Use ?? new AppSettings() to ensure a default object is returned even if deserialization yields null
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                // If the file is corrupt or invalid, log the error and return default settings.
                Debug.WriteLine($"Error loading settings from {_settingsFilePath}: {ex.Message}");
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves the provided settings object to the JSON file.
        /// </summary>
        /// <param name="settings">The AppSettings object to save.</param>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                // In a real-world app, you might want to log this error more formally.
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
