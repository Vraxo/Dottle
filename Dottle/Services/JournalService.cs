using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dottle.Models;
using Dottle.Utils;

namespace Dottle.Services;

public class JournalService
{
    private readonly EncryptionService _encryptionService;
    private readonly string _journalDirectory;

    public JournalService(EncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
        // Determine base path (next to executable)
        string? assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(assemblyLocation))
        {
            // Fallback or error handling if location cannot be determined
            assemblyLocation = Environment.CurrentDirectory;
        }
        _journalDirectory = Path.Combine(assemblyLocation, "Journals");

        // Ensure the directory exists
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
            // Handle file read errors
            return null;
        }
        catch (Exception)
        {
            // Catch potential decryption or other errors
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
            // Handle file write errors
            return false;
        }
        catch (Exception)
        {
            // Catch potential encryption or other errors
            return false;
        }
    }

    public string? CreateNewJournal(DateTime date, string password)
    {
        string dateString = PersianCalendarHelper.GetPersianDateString(date);
        string fileName = $"{dateString}.txt";
        string filePath = Path.Combine(_journalDirectory, fileName);

        if (File.Exists(filePath))
        {
            // Journal for this date already exists
            // Could return null or the existing filename
            return null;
        }

        // Create an empty encrypted file
        bool success = SaveJournalContent(fileName, string.Empty, password);

        return success ? fileName : null;
    }

    public string GetJournalDirectoryPath()
    {
        return _journalDirectory;
    }

    /// <summary>
    /// Changes the encryption password for all existing journal files.
    /// Reads each journal, decrypts with the old password, and re-encrypts with the new password.
    /// </summary>
    /// <param name="oldPassword">The current password used for decryption.</param>
    /// <param name="newPassword">The new password to use for encryption.</param>
    /// <returns>True if all journals were successfully re-encrypted, false otherwise.</returns>
    public bool ChangeEncryptionPassword(string oldPassword, string newPassword)
    {
        var files = Directory.EnumerateFiles(_journalDirectory, "*.txt");
        bool allSucceeded = true;

        foreach (var filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            try
            {
                // 1. Read encrypted data
                byte[] encryptedData = File.ReadAllBytes(filePath);

                // 2. Decrypt with old password
                string? plainText = _encryptionService.Decrypt(encryptedData, oldPassword);

                if (plainText == null)
                {
                    // Decryption failed for this file - indicates wrong old password or corruption
                    // Log this error? For now, we stop and report failure.
                    System.Diagnostics.Debug.WriteLine($"Error: Failed to decrypt {fileName} with the provided old password.");
                    allSucceeded = false;
                    break; // Stop processing further files
                }

                // 3. Encrypt with new password
                byte[] newEncryptedData = _encryptionService.Encrypt(plainText, newPassword);

                // 4. Overwrite the file with newly encrypted data
                File.WriteAllBytes(filePath, newEncryptedData);

                System.Diagnostics.Debug.WriteLine($"Successfully re-encrypted {fileName}.");

            }
            catch (Exception ex)
            {
                // Catch any IO or other exceptions during the process for this file
                System.Diagnostics.Debug.WriteLine($"Error processing file {fileName}: {ex.Message}");
                allSucceeded = false;
                break; // Stop processing further files on error
            }
        }

        return allSucceeded;
    }
}
