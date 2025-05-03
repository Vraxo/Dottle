using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Dottle.Models;
using Dottle.Utils;

namespace Dottle.Services;

public class JournalService
{
    private readonly EncryptionService _encryptionService;
    private readonly string _journalDirectory;
    private static readonly List<string> Moods = ["🌩️", "🌧️", "🌥️", "☀️", "🌈"];

    public JournalService(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
        string? assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyLocation))
        {
            assemblyLocation = Environment.CurrentDirectory;
        }
        _journalDirectory = Path.Combine(assemblyLocation, "Journals");

        Directory.CreateDirectory(_journalDirectory);
    }

    public IEnumerable<JournalEntry> GetJournalEntries()
    {
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

        // Added space between emoji and number
        string templateContent = $"📅 {persianDate}  🗓️ {dayOfWeek}  {moodEmoji} {moodNumberString}{Environment.NewLine}{Environment.NewLine}";

        bool success = SaveJournalContent(fileName, templateContent, password);

        return success ? fileName : null;
    }

    public string GetJournalDirectoryPath()
    {
        return _journalDirectory;
    }

    public bool ChangeEncryptionPassword(string oldPassword, string newPassword)
    {
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
}