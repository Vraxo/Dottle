using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Models;
using Dottle.Services;
using Dottle.Utils;
using Dottle.Views;

namespace Dottle.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly JournalService _journalService;
    private readonly SettingsService _settingsService; // Added SettingsService
    private string _password;

    [ObservableProperty]
    private ObservableCollection<JournalGroupViewModel> _journalGroups = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJournalSelected))]
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))]
    private JournalViewModel? _selectedJournal;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))]
    private string? _currentJournalContent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))]
    private bool _isLoadingContent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))]
    private bool _isSavingContent;

    [ObservableProperty]
    private string? _statusBarText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _currentPasswordForChange;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _newPassword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _confirmNewPassword;

    [ObservableProperty]
    private string? _changePasswordErrorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private bool _isChangingPassword;

    // Needs to be set by the View or App logic after the Window is created.
    public Window? OwnerWindow { get; set; }

    public bool IsJournalSelected => SelectedJournal != null;

    // Constructor updated to accept SettingsService
    public MainViewModel(JournalService journalService, SettingsService settingsService, string password)
    {
        _journalService = journalService;
        _settingsService = settingsService; // Store injected service
        _password = password;

        _settingsService.SettingsChanged += SettingsService_SettingsChanged; // Subscribe to changes

        LoadJournalList();
        UpdateStatusBar($"Journals loaded. Directory: {_journalService.GetJournalDirectoryPath()}");
    }

    partial void OnSelectedJournalChanged(JournalViewModel? value)
    {
        LoadSelectedJournalContent();
    }

    private void LoadJournalList()
    {
        var entries = _journalService.GetJournalEntries();
        var viewModels = entries.Select(e => new JournalViewModel(e)).ToList();

        var groups = viewModels
            .GroupBy(jvm => PersianCalendarHelper.GetPersianYear(jvm.Date))
            .Select(yearGroup =>
            {
                var monthGroups = yearGroup
                    .GroupBy(jvm => PersianCalendarHelper.GetPersianMonth(jvm.Date))
                    .Select(monthGroup => new JournalMonthGroupViewModel(
                        yearGroup.Key,
                        monthGroup.Key,
                        monthGroup.ToList()))
                    .OrderByDescending(mg => mg.Month);

                return new JournalGroupViewModel(yearGroup.Key, monthGroups);
            })
            .OrderByDescending(g => g.Year);

        JournalGroups = new ObservableCollection<JournalGroupViewModel>(groups);

        // Don't reset selection if list is refreshed due to settings change
        // SelectedJournal = null;
        // CurrentJournalContent = string.Empty;
        UpdateStatusBar($"Journal list refreshed. Source: {_journalService.GetJournalDirectoryPath()}");
    }

    private async void LoadSelectedJournalContent()
    {
        if (SelectedJournal == null)
        {
            CurrentJournalContent = string.Empty;
            return;
        }

        IsLoadingContent = true;
        CurrentJournalContent = string.Empty;
        UpdateStatusBar($"Loading {SelectedJournal.DisplayName}...");
        await Task.Delay(50);

        string? content = await Task.Run(() =>
            _journalService.ReadJournalContent(SelectedJournal.FileName, _password));

        IsLoadingContent = false;

        if (content != null)
        {
            CurrentJournalContent = content;
            UpdateStatusBar($"Loaded {SelectedJournal.DisplayName}.");
        }
        else
        {
            CurrentJournalContent = $"Error: Could not load or decrypt '{SelectedJournal.FileName}'. Check password or file integrity.";
            UpdateStatusBar($"Error loading {SelectedJournal.DisplayName}.");
        }
        SaveJournalCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveJournal()
    {
        return SelectedJournal != null &&
               CurrentJournalContent != null &&
               !IsLoadingContent &&
               !IsSavingContent;
    }

    [RelayCommand(CanExecute = nameof(CanSaveJournal))]
    private async Task SaveJournal()
    {
        if (SelectedJournal == null || CurrentJournalContent == null) return;

        IsSavingContent = true;
        UpdateStatusBar($"Saving {SelectedJournal.DisplayName}...");
        await Task.Delay(50);

        bool success = await Task.Run(() =>
            _journalService.SaveJournalContent(SelectedJournal.FileName, CurrentJournalContent, _password));

        IsSavingContent = false;

        if (success)
        {
            UpdateStatusBar($"Saved {SelectedJournal.DisplayName} successfully.");
        }
        else
        {
            UpdateStatusBar($"Error: Failed to save {SelectedJournal.DisplayName}.");
        }
        SaveJournalCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RequestNewJournalDate()
    {
        if (OwnerWindow == null)
        {
            UpdateStatusBar("Error: Cannot show dialog, owner window not set.");
            return;
        }

        var dialogViewModel = new NewJournalDialogViewModel();
        var dialog = new NewJournalDialog
        {
            DataContext = dialogViewModel
        };

        await dialog.ShowDialog<bool?>(OwnerWindow); // Use await without result check simplifies logic

        if (dialog.IsConfirmed) // Check custom property on dialog
        {
            DateTime selectedDate = dialog.SelectedDate;
            string selectedMood = dialog.SelectedMood;
            string dateString = PersianCalendarHelper.GetPersianDateString(selectedDate);
            UpdateStatusBar($"Creating journal for {dateString} with mood {selectedMood}...");

            string? newFileName = await Task.Run(() =>
                _journalService.CreateNewJournal(selectedDate, selectedMood, _password));

            if (newFileName != null)
            {
                LoadJournalList(); // Refresh list
                var newEntryVm = FindJournalViewModelByFileName(newFileName);
                SelectedJournal = newEntryVm; // Auto-select new entry
                UpdateStatusBar($"Created and selected journal for {dateString}.");
            }
            else
            {
                // Handle case where journal already exists or creation failed
                string existingFileName = $"{PersianCalendarHelper.GetPersianDateString(selectedDate)}.txt";
                var existingEntryVm = FindJournalViewModelByFileName(existingFileName);
                if (existingEntryVm != null)
                {
                    SelectedJournal = existingEntryVm; // Select existing if found
                    UpdateStatusBar($"Journal for {dateString} already exists. Selected existing entry.");
                }
                else
                {
                    UpdateStatusBar($"Error: Could not create or find journal for {dateString}.");
                }
            }
        }
        else
        {
            UpdateStatusBar("New journal creation cancelled.");
        }
    }


    private JournalViewModel? FindJournalViewModelByFileName(string fileName)
    {
        return JournalGroups
            .SelectMany(yearGroup => yearGroup.MonthGroups)
            .SelectMany(monthGroup => monthGroup.Journals)
            .FirstOrDefault(j => j.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateStatusBar(string message)
    {
        StatusBarText = $"{DateTime.Now:HH:mm:ss} - {message}";
    }

    private bool CanChangePassword()
    {
        // Simplified validation - more robust checks in the command execution
        return !IsChangingPassword;
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePassword()
    {
        ChangePasswordErrorMessage = null;

        if (string.IsNullOrEmpty(CurrentPasswordForChange) || CurrentPasswordForChange != _password)
        {
            ChangePasswordErrorMessage = "The Current Password provided does not match the active password or is empty.";
            UpdateStatusBar("Password change failed: Incorrect current password.");
            // Clear the fields after failure? Maybe not, let user correct.
            return;
        }

        if (string.IsNullOrEmpty(NewPassword) || NewPassword != ConfirmNewPassword)
        {
            ChangePasswordErrorMessage = "New passwords do not match or are empty.";
            UpdateStatusBar("Password change failed: New passwords mismatch or empty.");
            return;
        }
        if (NewPassword == _password)
        {
            ChangePasswordErrorMessage = "New password cannot be the same as the old password.";
            UpdateStatusBar("Password change failed: New password is same as old.");
            return;
        }

        IsChangingPassword = true;
        UpdateStatusBar("Changing password and re-encrypting all journals... Please wait.");
        await Task.Delay(50); // Allow UI to update

        bool success = await Task.Run(() =>
            _journalService.ChangeEncryptionPassword(_password, NewPassword!));

        IsChangingPassword = false;

        if (success)
        {
            _password = NewPassword!; // Update the active password

            // Clear fields after successful change
            CurrentPasswordForChange = null;
            NewPassword = null;
            ConfirmNewPassword = null;
            ChangePasswordErrorMessage = null;

            UpdateStatusBar("Password changed successfully. All journals re-encrypted.");
            // Reload content if a journal was selected, as it needs the new password
            if (SelectedJournal is not null) LoadSelectedJournalContent();
        }
        else
        {
            ChangePasswordErrorMessage = "Critical Error: Failed to re-encrypt one or more journals. Password has NOT been changed.";
            UpdateStatusBar("Error changing password. See error message. Password remains unchanged.");
            // Do not clear fields on critical failure
        }
        // Explicitly notify CanExecuteChanged for dependent properties if needed,
        // but IsChangingPassword handles the main enable/disable state via NotifyCanExecuteChangedFor.
    }

    [RelayCommand]
    private async Task ShowExportDialog()
    {
        if (OwnerWindow == null)
        {
            UpdateStatusBar("Error: Cannot show export dialog, owner window not set.");
            return;
        }

        UpdateStatusBar("Opening export dialog...");

        try
        {
            var exportViewModel = new ExportJournalsDialogViewModel(_journalService, _password, OwnerWindow);
            var exportDialog = new ExportJournalsDialog
            {
                DataContext = exportViewModel
            };

            await exportDialog.ShowDialog(OwnerWindow);

            UpdateStatusBar("Export dialog closed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing export dialog: {ex}");
            UpdateStatusBar($"Error: Could not open export dialog. {ex.Message}");
        }
    }

    // New Command to show the Settings Dialog
    [RelayCommand]
    private async Task ShowSettingsDialog()
    {
        if (OwnerWindow == null)
        {
            UpdateStatusBar("Error: Cannot show settings dialog, owner window not set.");
            return;
        }

        UpdateStatusBar("Opening settings dialog...");

        try
        {
            // Create ViewModel, passing required services
            var settingsViewModel = new SettingsViewModel(_settingsService, _journalService);
            var settingsDialog = new SettingsDialog
            {
                DataContext = settingsViewModel
                // OwnerWindow is set by the dialog's code-behind OnDataContextChanged
            };

            await settingsDialog.ShowDialog(OwnerWindow);

            UpdateStatusBar("Settings dialog closed.");
            // Journal list is refreshed automatically via SettingsChanged event
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing settings dialog: {ex}");
            UpdateStatusBar($"Error: Could not open settings dialog. {ex.Message}");
        }
    }

    // Event handler for settings changes (specifically journal path)
    private void SettingsService_SettingsChanged(object? sender, EventArgs e)
    {
        // The journal path might have changed, reload the list
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
            SelectedJournal = null; // Deselect journal as the list is changing
            CurrentJournalContent = string.Empty; // Clear editor
            LoadJournalList();
            UpdateStatusBar($"Settings changed. Journal list refreshed from: {_journalService.GetJournalDirectoryPath()}");
        });
    }

    // Ensure cleanup
    public void Cleanup()
    {
        if (_settingsService != null)
        {
            _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        }
    }
}