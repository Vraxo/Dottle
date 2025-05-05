using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Dottle.Models;
using Dottle.Utils;

namespace Dottle.Services;

public class JournalService
{
    private readonly EncryptionService _encryptionService;
    private readonly SettingsService _settingsService;
    private string _journalDirectory;
    private static readonly List<string> Moods = ["🌩️", "🌧️", "🌥️", "☀️", "🌈"];

    public JournalService(EncryptionService encryptionService, SettingsService settingsService)
    {
        _encryptionService = encryptionService;
        _settingsService = settingsService;
        _journalDirectory = GetAndEnsureJournalDirectory(); // Use method to get/ensure path

        // Subscribe to settings changes if needed elsewhere, but direct path access is fine here
    }

    private string GetAndEnsureJournalDirectory()
    {
        string? path = _settingsService.CurrentSettings.JournalDirectoryPath;

        if (string.IsNullOrEmpty(path))
        {
            path = _settingsService.GetDefaultJournalDirectory();
            // Update settings if the path was missing/invalid
            _settingsService.CurrentSettings.JournalDirectoryPath = path;
            _settingsService.SaveSettings(_settingsService.CurrentSettings);
        }

        if (!Directory.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                // Log or handle the error appropriately
                System.Diagnostics.Debug.WriteLine($"FATAL: Could not create journal directory '{path}'. Error: {ex.Message}");
                // Potentially throw or return a known invalid path
                return string.Empty; // Or handle more gracefully
            }
        }
        return path;
    }

    public IEnumerable<JournalEntry> GetJournalEntries()
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current before use
        if (!Directory.Exists(_journalDirectory)) return Enumerable.Empty<JournalEntry>();

        var journalEntries = new List<JournalEntry>();
        var files = Directory.EnumerateFiles(_journalDirectory, "*.txt");
        int currentPersianYear = PersianCalendarHelper.GetPersianYear(DateTime.Now);

        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (PersianCalendarHelper.TryParsePersianDateString(fileName, out DateTime date))
            {
                if (PersianCalendarHelper.GetPersianYear(date) == currentPersianYear)
                {
                    journalEntries.Add(new JournalEntry(Path.GetFileName(file), date));
                }
            }
        }
        return journalEntries.OrderByDescending(je => je.Date);
    }

    public IEnumerable<JournalEntry> GetAllJournalEntries()
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current before use
        if (!Directory.Exists(_journalDirectory)) return Enumerable.Empty<JournalEntry>();

        var journalEntries = new List<JournalEntry>();
        var files = Directory.EnumerateFiles(_journalDirectory, "*.txt");

        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (PersianCalendarHelper.TryParsePersianDateString(fileName, out DateTime date))
            {
                journalEntries.Add(new JournalEntry(Path.GetFileName(file), date));
            }
        }
        return journalEntries.OrderByDescending(je => je.Date);
    }


    public string? ReadJournalContent(string fileName, string password)
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current
        string filePath = Path.Combine(_journalDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            byte[] encryptedData = File.ReadAllBytes(filePath);
            return _encryptionService.Decrypt(encryptedData, password);
        }
        catch (IOException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool SaveJournalContent(string fileName, string content, string password)
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current
        string filePath = Path.Combine(_journalDirectory, fileName);
        try
        {
            byte[] encryptedData = _encryptionService.Encrypt(content, password);
            File.WriteAllBytes(filePath, encryptedData);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string? CreateNewJournal(DateTime date, string moodEmoji, string password)
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current
        string dateString = PersianCalendarHelper.GetPersianDateString(date);
        string fileName = $"{dateString}.txt";
        string filePath = Path.Combine(_journalDirectory, fileName);

        if (File.Exists(filePath))
        {
            return null;
        }

        string persianDate = dateString;
        string dayOfWeek = date.ToString("dddd", CultureInfo.InvariantCulture);

        int moodIndex = Moods.IndexOf(moodEmoji);
        string moodNumberString = (moodIndex >= 0) ? (moodIndex + 1).ToString() : "?";

        string templateContent = $"📅 {persianDate}  🗓️ {dayOfWeek}  {moodEmoji} {moodNumberString}{Environment.NewLine}{Environment.NewLine}";

        bool success = SaveJournalContent(fileName, templateContent, password);

        return success ? fileName : null;
    }

    public string GetJournalDirectoryPath()
    {
        return GetAndEnsureJournalDirectory(); // Return the current, validated path
    }

    public bool ChangeEncryptionPassword(string oldPassword, string newPassword)
    {
        _journalDirectory = GetAndEnsureJournalDirectory(); // Ensure path is current
        if (!Directory.Exists(_journalDirectory)) return false;

        var files = Directory.EnumerateFiles(_journalDirectory, "*.txt");
        bool allSucceeded = true;

        foreach (var filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);
                string? plainText = _encryptionService.Decrypt(encryptedData, oldPassword);

                if (plainText == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Error: Failed to decrypt {fileName} with the provided old password.");
                    allSucceeded = false;
                    break;
                }

                byte[] newEncryptedData = _encryptionService.Encrypt(plainText, newPassword);
                File.WriteAllBytes(filePath, newEncryptedData);
                System.Diagnostics.Debug.WriteLine($"Successfully re-encrypted {fileName}.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing file {fileName}: {ex.Message}");
                allSucceeded = false;
                break;
            }
        }

        return allSucceeded;
    }

    public (bool Success, string ErrorMessage) ChangeJournalDirectory(string newPath)
    {
        string oldPath = GetAndEnsureJournalDirectory();

        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "New path is the same as the current path.");
        }

        if (!Directory.Exists(newPath))
        {
            try
            {
                Directory.CreateDirectory(newPath);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create new directory '{newPath}': {ex.Message}");
            }
        }

        // Check if new path is inside the old path (or vice versa) to prevent issues
        if (newPath.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase) && newPath.Length > oldPath.Length)
        {
            return (false, "Cannot move journal directory into a subdirectory of itself.");
        }
        if (oldPath.StartsWith(newPath, StringComparison.OrdinalIgnoreCase) && oldPath.Length > newPath.Length)
        {
            // This case *might* be okay, but let's disallow for safety now.
            return (false, "Cannot move journal directory into its parent directory directly (potential conflicts).");
        }


        var filesToMove = Directory.EnumerateFiles(oldPath, "*.txt").ToList();
        var failedMoves = new List<string>();

        foreach (var oldFilePath in filesToMove)
        {
            string fileName = Path.GetFileName(oldFilePath);
            string newFilePath = Path.Combine(newPath, fileName);

            try
            {
                File.Move(oldFilePath, newFilePath, overwrite: false); // Don't overwrite existing files in new location
            }
            catch (IOException ioEx) when (ioEx.Message.Contains("already exists"))
            {
                // Handle file already exists - log it, maybe skip? For now, treat as failure.
                failedMoves.Add($"{fileName} (already exists in destination)");
            }
            catch (Exception ex)
            {
                failedMoves.Add($"{fileName} ({ex.Message})");
            }
        }

        if (failedMoves.Any())
        {
            // Attempt to move back files that were successfully moved before the failure
            foreach (var oldFilePath in filesToMove)
            {
                string fileName = Path.GetFileName(oldFilePath);
                string newFilePath = Path.Combine(newPath, fileName);
                // Check if it was moved successfully (exists in new, not in old) AND it wasn't one that failed
                if (File.Exists(newFilePath) && !File.Exists(oldFilePath) && !failedMoves.Any(f => f.StartsWith(fileName)))
                {
                    try
                    {
                        File.Move(newFilePath, oldFilePath, overwrite: false);
                    }
                    catch (Exception moveBackEx)
                    {
                        // Log critical error - couldn't rollback
                        System.Diagnostics.Debug.WriteLine($"CRITICAL ERROR: Failed to move file back during rollback: {fileName}. Error: {moveBackEx.Message}");
                    }
                }
            }
            return (false, $"Failed to move some files: {string.Join(", ", failedMoves)}. No changes were made.");
        }

        // All files moved successfully, update settings
        _settingsService.CurrentSettings.JournalDirectoryPath = newPath;
        bool settingsSaved = _settingsService.SaveSettings(_settingsService.CurrentSettings);

        if (settingsSaved)
        {
            _journalDirectory = newPath; // Update internal path
            return (true, "Journal directory changed successfully.");
        }
        else
        {
            // This is tricky - files moved but setting not saved. Try to rollback move?
            // For now, return error indicating settings save failure. Manual intervention might be needed.
            return (false, "Files were moved, but failed to save the new path in settings. Please check settings.json.");
        }
    }
}