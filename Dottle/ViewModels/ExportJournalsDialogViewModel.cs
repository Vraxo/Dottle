using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Models;
using Dottle.Services;
using Dottle.Utils;

namespace Dottle.ViewModels;

public partial class ExportJournalItemViewModel(JournalEntry journal) : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected = true;

    public JournalEntry Journal { get; } = journal;
    public string DisplayName => Journal.DisplayName;
    public string FileName => Journal.FileName;
    public DateTime Date => Journal.Date;
}

public partial class ExportJournalsDialogViewModel : ViewModelBase
{
    private readonly JournalService _journalService;
    private readonly string _password;
    private readonly Window? _owner;

    [ObservableProperty]
    private ObservableCollection<ExportJournalItemViewModel> _journalItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private string? _selectedFolderPath;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectFolder))]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _isExporting;

    [ObservableProperty]
    private double _exportProgress;

    [ObservableProperty]
    private bool _exportAsSingleFile = true;

    [ObservableProperty]
    private ExportFormatType _selectedExportFormat = ExportFormatType.FullText;

    [ObservableProperty]
    private SortDirection _selectedSortDirection = SortDirection.Ascending;

    public bool CanSelectFolder => !IsExporting;
    public bool CanExport => !_isExporting && !string.IsNullOrEmpty(SelectedFolderPath) && JournalItems.Any(j => j.IsSelected);

    public ExportJournalsDialogViewModel(JournalService journalService, string password, Window? owner)
    {
        _journalService = journalService;
        _password = password;
        _owner = owner;

        LoadAllJournals();
        UpdateStatus("Ready to export.");
    }

    private void LoadAllJournals()
    {
        foreach (var oldItem in JournalItems)
        {
            oldItem.PropertyChanged -= JournalItem_PropertyChanged;
        }

        try
        {
            IEnumerable<JournalEntry> allEntries = _journalService.GetAllJournalEntries();
            List<ExportJournalItemViewModel> itemViewModels = allEntries
                .OrderByDescending(e => e.Date)
                .Select(entry => new ExportJournalItemViewModel(entry))
                .ToList();

            foreach (ExportJournalItemViewModel newItem in itemViewModels)
            {
                newItem.PropertyChanged += JournalItem_PropertyChanged;
            }

            JournalItems = new(itemViewModels);
            UpdateStatus($"Loaded {JournalItems.Count} journals.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading journals: {ex.Message}");
            JournalItems.Clear();
        }

        OnPropertyChanged(nameof(CanExport));
        SelectFolderCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void JournalItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExportJournalItemViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(CanExport));
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSelectFolder))]
    private async Task SelectFolder()
    {
        if (_owner == null)
        {
            UpdateStatus("Error: Cannot open folder picker, owner window not available.");
            return;
        }

        var folderPickerOptions = new FolderPickerOpenOptions
        {
            Title = "Select Export Destination Folder",
            AllowMultiple = false
        };

        var result = await _owner.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

        if (result.Count > 0)
        {
            string? path = result[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                SelectedFolderPath = path;
                UpdateStatus($"Selected folder: {SelectedFolderPath}");
            }
            else
            {
                UpdateStatus("Error: Could not get a local path for the selected folder.");
                SelectedFolderPath = null;
            }
        }
        else
        {
            UpdateStatus("Folder selection cancelled.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task Export()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
        {
            UpdateStatus("Error: Invalid or non-existent export folder selected.");
            return;
        }

        var selectedJournals = JournalItems
            .Where(j => j.IsSelected)
            .OrderBy(j => j.Date)
            .ToList();

        if (!selectedJournals.Any())
        {
            UpdateStatus("No journals selected for export.");
            return;
        }

        IsExporting = true;
        ExportProgress = 0;
        int exportedCount = 0;
        int failedCount = 0;
        int totalToExport = selectedJournals.Count;

        UpdateStatus($"Starting export of {totalToExport} journals to {SelectedFolderPath}...");

        try
        {
            switch (SelectedExportFormat)
            {
                case ExportFormatType.FullText:
                    string exportMode = ExportAsSingleFile ? "single file" : "multiple files";
                    UpdateStatus($"Starting Full Text export ({exportMode}) of {totalToExport} journals...");
                    if (ExportAsSingleFile)
                    {
                        await ExportAsSingleFileAsync(selectedJournals, totalToExport);
                    }
                    else
                    {
                        await ExportAsMultipleFilesAsync(selectedJournals, totalToExport);
                    }
                    break;

                case ExportFormatType.MoodSummary:
                    UpdateStatus($"Starting Mood Summary export of {totalToExport} journals...");
                    await ExportMoodSummaryAsync(selectedJournals, totalToExport);
                    break;

                default:
                    UpdateStatus($"Error: Unknown export format selected.");
                    IsExporting = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Unhandled error during export: {ex.Message}");
            IsExporting = false;
            ExportProgress = 0;
        }
    }

    private async Task ExportAsSingleFileAsync(List<ExportJournalItemViewModel> journals, int totalToExport)
    {
        int exportedCount = 0;
        int failedCount = 0;
        var stringBuilder = new StringBuilder();
        string singleFileName = $"Dottle_Export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string singleFilePath = Path.Combine(SelectedFolderPath!, singleFileName);

        for (int i = 0; i < totalToExport; i++)
        {
            var journalVm = journals[i];
            ExportProgress = (double)(i + 1) / totalToExport * 100;
            UpdateStatus($"Processing {i + 1}/{totalToExport}: {journalVm.DisplayName}...");

            try
            {
                string? decryptedContent = await Task.Run(() =>
                    _journalService.ReadJournalContent(journalVm.FileName, _password));

                if (decryptedContent != null)
                {
                    stringBuilder.AppendLine(decryptedContent);
                    if (i < totalToExport - 1)
                    {
                        stringBuilder.AppendLine();
                    }
                    exportedCount++;
                }
                else
                {
                    failedCount++;
                    UpdateStatus($"Warning: Failed to read/decrypt {journalVm.DisplayName}. Skipped.");
                    stringBuilder.AppendLine($"--- Skipped: {journalVm.DisplayName} (Failed to decrypt) ---");
                    if (i < totalToExport - 1)
                    {
                        stringBuilder.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                UpdateStatus($"Error processing {journalVm.DisplayName}: {ex.Message}. Skipped.");
                stringBuilder.AppendLine($"--- Skipped: {journalVm.DisplayName} (Error: {ex.Message}) ---");
                if (i < totalToExport - 1)
                {
                    stringBuilder.AppendLine();
                }
            }
            await Task.Delay(5);
        }

        try
        {
            UpdateStatus($"Writing aggregated content to {singleFileName}...");
            await File.WriteAllTextAsync(singleFilePath, stringBuilder.ToString());
            UpdateStatus($"Export complete. {exportedCount} journals written to {singleFileName}, Failures: {failedCount}.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error writing aggregated file {singleFileName}: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
            ExportProgress = 100;
        }
    }

    private async Task ExportAsMultipleFilesAsync(List<ExportJournalItemViewModel> journals, int totalToExport)
    {
        int exportedCount = 0;
        int failedCount = 0;

        for (int i = 0; i < totalToExport; i++)
        {
            var journalVm = journals[i];
            UpdateStatus($"Exporting {i + 1}/{totalToExport}: {journalVm.DisplayName}...");
            ExportProgress = (double)(i + 1) / totalToExport * 100;

            try
            {
                string? decryptedContent = await Task.Run(() =>
                    _journalService.ReadJournalContent(journalVm.FileName, _password));

                if (decryptedContent != null)
                {
                    string outputFileName = Path.ChangeExtension(journalVm.FileName, ".txt");
                    string outputPath = Path.Combine(SelectedFolderPath!, outputFileName);

                    Directory.CreateDirectory(SelectedFolderPath!);

                    await File.WriteAllTextAsync(outputPath, decryptedContent);
                    exportedCount++;
                }
                else
                {
                    failedCount++;
                    UpdateStatus($"Warning: Failed to read/decrypt {journalVm.DisplayName}. Skipped.");
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                UpdateStatus($"Error exporting {journalVm.DisplayName}: {ex.Message}. Skipped.");
            }
            await Task.Delay(10);
        }

        IsExporting = false;
        ExportProgress = 100;
        UpdateStatus($"Export complete. Exported: {exportedCount}, Failed: {failedCount}.");
    }

    private async Task ExportMoodSummaryAsync(List<ExportJournalItemViewModel> journals, int totalToExport)
    {
        int exportedCount = 0;
        int failedCount = 0;
        List<MoodEntry> moodEntries = new();
        string moodSummaryFileName = $"Dottle_MoodSummary_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string moodSummaryFilePath = Path.Combine(SelectedFolderPath!, moodSummaryFileName);

        for (int i = 0; i < totalToExport; i++)
        {
            var journalVm = journals[i];
            ExportProgress = (double)(i + 1) / totalToExport * 100;
            UpdateStatus($"Processing {i + 1}/{totalToExport}: {journalVm.DisplayName} for mood...");

            try
            {
                string? decryptedContent = await Task.Run(() =>
                    _journalService.ReadJournalContent(journalVm.FileName, _password));

                if (decryptedContent is not null)
                {
                    using var reader = new StringReader(decryptedContent);
                    string? firstLine = await reader.ReadLineAsync();

                    if (firstLine != null)
                    {
                        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[^1], out int moodScore) && moodScore >= 1 && moodScore <= 5)
                        {
                            moodEntries.Add(new MoodEntry { Date = journalVm.Date, MoodScore = moodScore });
                            exportedCount++;
                        }
                        else
                        {
                            failedCount++;
                            UpdateStatus($"Warning: Could not parse mood score from header of {journalVm.DisplayName}. Skipped.");
                        }
                    }
                    else
                    {
                        failedCount++;
                        UpdateStatus($"Warning: File {journalVm.DisplayName} is empty or could not read first line. Skipped.");
                    }
                }
                else
                {
                    failedCount++;
                    UpdateStatus($"Warning: Failed to read/decrypt {journalVm.DisplayName}. Skipped.");
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                UpdateStatus($"Error processing {journalVm.DisplayName} for mood: {ex.Message}. Skipped.");
            }
            await Task.Delay(5);
        }

        var sortedEntries = SelectedSortDirection == SortDirection.Ascending
            ? moodEntries.OrderBy(e => e.Date)
            : moodEntries.OrderByDescending(e => e.Date);

        StringBuilder stringBuilder = new();

        foreach (MoodEntry? entry in sortedEntries)
        {
            string persianDate = PersianCalendarHelper.GetPersianDateString(entry.Date);
            string emoji = GetMoodEmoji(entry.MoodScore);
            stringBuilder.AppendLine($"{persianDate} - {entry.MoodScore} - {emoji}");
        }

        try
        {
            UpdateStatus($"Writing mood summary to {moodSummaryFileName}...");
            await File.WriteAllTextAsync(moodSummaryFilePath, stringBuilder.ToString());
            UpdateStatus($"Mood Summary export complete. {exportedCount} entries written to {moodSummaryFileName}, Failures: {failedCount}.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error writing mood summary file {moodSummaryFileName}: {ex.Message}");
        }
        finally
        {
            IsExporting = false;
            ExportProgress = 100;
        }
    }

    private static string GetMoodEmoji(int moodScore)
    {
        return moodScore switch
        {
            1 => "🌩️",
            2 => "🌧️",
            3 => "🌥️",
            4 => "☀️",
            5 => "🌈",
            _ => "❓"
        };
    }

    private void UpdateStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss} - {message}";
    }

    [RelayCommand] 
    private static void Confirm() 
    { 

    }

    [RelayCommand] 
    private static void Cancel() 
    {

    }

    private class MoodEntry
    {
        public DateTime Date { get; set; }
        public int MoodScore { get; set; }
    }
}