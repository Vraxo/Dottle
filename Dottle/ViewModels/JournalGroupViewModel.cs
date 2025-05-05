using System.Collections.ObjectModel;

namespace Dottle.ViewModels;

public partial class JournalGroupViewModel : ViewModelBase
{
    public int Year { get; }
    public ObservableCollection<JournalMonthGroupViewModel> MonthGroups { get; }

    public JournalGroupViewModel(int year, IEnumerable<JournalMonthGroupViewModel> monthGroups)
    {
        Year = year;
        // Order months descending (e.g., Esfand before Farvardin)
        MonthGroups = new ObservableCollection<JournalMonthGroupViewModel>(monthGroups.OrderByDescending(m => m.Month));
    }

    // Display name for the TreeView
    public string DisplayName => $"{Year} ({MonthGroups.Sum(m => m.Journals.Count)})";
}
