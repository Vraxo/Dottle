using System.Collections.ObjectModel;
using System.Linq;
using Dottle.Utils; // Assuming PersianCalendarHelper is here

namespace Dottle.ViewModels;

public partial class JournalMonthGroupViewModel : ViewModelBase
{
    public int Year { get; }
    public int Month { get; }
    public string MonthName { get; }
    public ObservableCollection<JournalViewModel> Journals { get; }

    public JournalMonthGroupViewModel(int year, int month, IEnumerable<JournalViewModel> journals)
    {
        Year = year;
        Month = month;
        MonthName = PersianCalendarHelper.GetPersianMonthName(month); // Get month name
        Journals = new ObservableCollection<JournalViewModel>(journals.OrderByDescending(j => j.Date));
    }

    // Display name for the TreeView
    public string DisplayName => $"{MonthName} ({Journals.Count})";
}
