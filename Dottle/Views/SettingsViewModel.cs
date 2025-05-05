using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Services;
using System;
using System.Threading.Tasks;
using System.Linq; // Required for LINQ methods like Any()

namespace Dottle.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly JournalService _journalService;

    [ObservableProperty]
    private string? _currentJournalPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplySettingsCommand))]
    private string? _newJournalPath;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isApplying;

    public Window? OwnerWindow { get; set; }

    public SettingsViewModel(SettingsService settingsService, JournalService journalService)
    {
        _settingsService = settingsService;
        _journalService = journalService;
        _currentJournalPath = _settingsService.CurrentSettings.JournalDirectoryPath;
        UpdateStatus("Settings loaded.");
    }

    private bool CanApplySettings()
    {
        return !string.IsNullOrEmpty(NewJournalPath) &&
               !string.Equals(NewJournalPath, CurrentJournalPath, StringComparison.OrdinalIgnoreCase) &&
               !IsApplying;
    }

    [RelayCommand]
    private async Task SelectNewFolder()
    {
        if (OwnerWindow == null)
        {
            UpdateStatus("Error: Cannot open folder picker, owner window not available.");
            return;
        }

        var folderPickerOptions = new FolderPickerOpenOptions
        {
            Title = "Select New Journal Folder",
            AllowMultiple = false
        };

        var result = await OwnerWindow.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

        if (result.Count > 0)
        {
            string? path = result[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                NewJournalPath = path;
                UpdateStatus($"New folder selected: {NewJournalPath}");
            }
            else
            {
                UpdateStatus("Error: Could not get a local path for the selected folder.");
                NewJournalPath = null; // Clear if path is invalid
            }
        }
        else
        {
            UpdateStatus("Folder selection cancelled.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplySettings))]
    private async Task ApplySettings()
    {
        if (string.IsNullOrEmpty(NewJournalPath)) return;

        IsApplying = true;
        UpdateStatus($"Applying changes: Moving journals to '{NewJournalPath}'...");
        await Task.Delay(50); // Give UI chance to update

        (bool success, string message) = await Task.Run(() => _journalService.ChangeJournalDirectory(NewJournalPath));

        IsApplying = false;

        if (success)
        {
            CurrentJournalPath = _settingsService.CurrentSettings.JournalDirectoryPath; // Update displayed path
            NewJournalPath = null; // Clear selection
            UpdateStatus($"Success: {message}");
        }
        else
        {
            UpdateStatus($"Error: {message}");
        }
    }

    private void UpdateStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss} - {message}";
    }
}