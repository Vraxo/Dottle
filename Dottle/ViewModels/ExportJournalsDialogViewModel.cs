using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Models;
using Dottle.Services;

namespace Dottle.ViewModels;

public partial class ExportJournalItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isSelected;

    public JournalEntry Journal { get; }
    public string DisplayName => Journal.DisplayName;
    public string FileName => Journal.FileName;
    public DateTime Date => Journal.Date;

    public ExportJournalItemViewModel(JournalEntry journal)
    {
        Journal = journal;
        _isSelected = true;
    }
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
    private bool _exportAsSingleFile;

    public bool CanSelectFolder => !_isExporting;
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
            var allEntries = _journalService.GetAllJournalEntries();
            var itemViewModels = allEntries
                .OrderByDescending(e => e.Date)
                .Select(entry => new ExportJournalItemViewModel(entry))
                .ToList();

            foreach (var newItem in itemViewModels)
            {
                newItem.PropertyChanged += JournalItem_PropertyChanged;
            }

            JournalItems = new ObservableCollection<ExportJournalItemViewModel>(itemViewModels);
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

        // Order selected journals by date ascending for single file export consistency
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
        string exportMode = ExportAsSingleFile ? "single file" : "multiple files";

        UpdateStatus($"Starting export of {totalToExport} journals ({exportMode}) to {SelectedFolderPath}...");

        if (ExportAsSingleFile)
        {
            await ExportAsSingleFileAsync(selectedJournals, totalToExport, exportedCount, failedCount);
        }
        else
        {
            await ExportAsMultipleFilesAsync(selectedJournals, totalToExport, exportedCount, failedCount);
        }
    }

    private async Task ExportAsSingleFileAsync(List<ExportJournalItemViewModel> journals, int totalToExport, int exportedCount, int failedCount)
    {
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
                    // Append content directly
                    stringBuilder.AppendLine(decryptedContent);
                    // Add a single blank line as a separator IF it's not the last entry
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
                    // Add a single blank line as a separator IF it's not the last entry
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
                // Add a single blank line as a separator IF it's not the last entry
                if (i < totalToExport - 1)
                {
                    stringBuilder.AppendLine();
                }
            }
            await Task.Delay(5); // Small delay to allow UI updates
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
            // In this case, all content might be lost
        }
        finally
        {
            IsExporting = false;
            ExportProgress = 100;
        }
    }

    private async Task ExportAsMultipleFilesAsync(List<ExportJournalItemViewModel> journals, int totalToExport, int exportedCount, int failedCount)
    {
        for (int i = 0; i < totalToExport; i++)
        {
            var journalVm = journals[i]; // Still ordered by date due to initial sorting
            UpdateStatus($"Exporting {i + 1}/{totalToExport}: {journalVm.DisplayName}...");
            ExportProgress = (double)(i + 1) / totalToExport * 100;

            try
            {
                string? decryptedContent = await Task.Run(() =>
                    _journalService.ReadJournalContent(journalVm.FileName, _password));

                if (decryptedContent != null)
                {
                    // Keep original filename but ensure .txt extension
                    string outputFileName = Path.ChangeExtension(journalVm.FileName, ".txt");
                    string outputPath = Path.Combine(SelectedFolderPath!, outputFileName);

                    // Ensure directory exists (though SelectedFolderPath should)
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
            await Task.Delay(10); // Keep small delay
        }

        IsExporting = false;
        ExportProgress = 100;
        UpdateStatus($"Export complete. Exported: {exportedCount}, Failed: {failedCount}.");
    }


    private void UpdateStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss} - {message}";
    }

    [RelayCommand] private void Confirm() { /* Logic handled by View closing dialog */ }
    [RelayCommand] private void Cancel() { /* Logic handled by View closing dialog */ }
}
