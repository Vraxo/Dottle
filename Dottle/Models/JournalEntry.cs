namespace Dottle.Models;

public class JournalEntry(string fileName, DateTime date)
{
    public string FileName { get; } = fileName;
    public DateTime Date { get; } = date;
    public string DisplayName { get; } = Path.GetFileNameWithoutExtension(fileName);
}