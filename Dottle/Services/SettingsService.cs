using System;
using System.IO;
using System.Text.Json;
using Dottle.Models;

namespace Dottle.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private const string SettingsFileName = "settings.json";

    public Settings CurrentSettings { get; private set; }

    public event EventHandler? SettingsChanged;

    public SettingsService()
    {
        string baseDirectory = AppContext.BaseDirectory;
        _settingsFilePath = Path.Combine(baseDirectory, SettingsFileName);
        CurrentSettings = LoadSettings();
    }

    private Settings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    if (string.IsNullOrEmpty(settings.JournalDirectoryPath) || !Directory.Exists(settings.JournalDirectoryPath))
                    {
                        settings.JournalDirectoryPath = GetDefaultJournalDirectory();
                        EnsureDirectoryExists(settings.JournalDirectoryPath);
                        SaveSettings(settings); // Save corrected default path
                    }
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        // Default settings if file doesn't exist or loading failed
        var defaultSettings = new Settings
        {
            JournalDirectoryPath = GetDefaultJournalDirectory()
        };
        EnsureDirectoryExists(defaultSettings.JournalDirectoryPath);
        SaveSettings(defaultSettings); // Save initial default settings
        return defaultSettings;
    }

    public bool SaveSettings(Settings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            CurrentSettings = settings; // Update in-memory settings
            SettingsChanged?.Invoke(this, EventArgs.Empty); // Notify listeners
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            return false;
        }
    }

    public string GetDefaultJournalDirectory()
    {
        string? assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyLocation))
        {
            assemblyLocation = Environment.CurrentDirectory;
        }
        return Path.Combine(assemblyLocation, "Journals");
    }

    private void EnsureDirectoryExists(string? path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating directory '{path}': {ex.Message}");
                // Handle error appropriately, maybe fallback or throw
            }
        }
    }
}