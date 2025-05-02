using System;

namespace Dottle.Models;

public class JournalEntry
{
    public string FileName { get; }
    public DateTime Date { get; }
    public string DisplayName { get; } // e.g., "1404-02-10"

    public JournalEntry(string fileName, DateTime date)
    {
        FileName = fileName;
        Date = date;
        // Assuming filename is already in YYYY-MM-DD.txt format
        DisplayName = Path.GetFileNameWithoutExtension(fileName);
    }
}