using System.Collections.ObjectModel;
using System.Linq;

namespace Dottle.ViewModels;

public partial class JournalGroupViewModel : ViewModelBase
{
    public int Year { get; }
    public ObservableCollection<JournalViewModel> Journals { get; }

    public JournalGroupViewModel(int year, IEnumerable<JournalViewModel> journals)
    {
        Year = year;
        Journals = new ObservableCollection<JournalViewModel>(journals.OrderByDescending(j => j.Date));
    }
}