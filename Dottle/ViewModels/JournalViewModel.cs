using Dottle.Models;

namespace Dottle.ViewModels;

public class JournalViewModel : ViewModelBase
{
    private readonly JournalEntry _journalEntry;

    public string FileName => _journalEntry.FileName;
    public string DisplayName => _journalEntry.DisplayName;
    public DateTime Date => _journalEntry.Date;

    public JournalViewModel(JournalEntry journalEntry)
    {
        _journalEntry = journalEntry;
    }
}