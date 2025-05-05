using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Services;
using Dottle.Utils;
using Dottle.Views;

namespace Dottle.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly JournalService _journalService;
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

    public Window? OwnerWindow { get; set; }

    public bool IsJournalSelected => SelectedJournal != null;

    public MainViewModel(JournalService journalService, string password)
    {
        _journalService = journalService;
        _password = password;
        LoadJournalList();
        UpdateStatusBar($"Journals loaded. Directory: {_journalService.GetJournalDirectoryPath()}");
    }

    partial void OnSelectedJournalChanged(JournalViewModel? value)
    {
        LoadSelectedJournalContent();
    }

    private void LoadJournalList()
    {
        var entries = _journalService.GetJournalEntries(); // Already filtered by current year
        var viewModels = entries.Select(e => new JournalViewModel(e)).ToList();

        var groups = viewModels
            .GroupBy(jvm => PersianCalendarHelper.GetPersianYear(jvm.Date)) // Group by Year
            .Select(yearGroup =>
            {
                var monthGroups = yearGroup
                    .GroupBy(jvm => PersianCalendarHelper.GetPersianMonth(jvm.Date)) // Group by Month within Year
                    .Select(monthGroup => new JournalMonthGroupViewModel(
                        yearGroup.Key,
                        monthGroup.Key,
                        monthGroup.ToList())) // Create Month Group VM
                    .OrderByDescending(mg => mg.Month); // Order months within the year

                return new JournalGroupViewModel(yearGroup.Key, monthGroups); // Create Year Group VM
            })
            .OrderByDescending(g => g.Year); // Order years

        JournalGroups = new ObservableCollection<JournalGroupViewModel>(groups);

        SelectedJournal = null;
        CurrentJournalContent = string.Empty;
        UpdateStatusBar("Journal list refreshed (Current Year Only).");
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

        bool? dialogResult = await dialog.ShowDialog<bool?>(OwnerWindow);

        if (dialogResult == true && dialog.IsConfirmed)
        {
            DateTime selectedDate = dialog.SelectedDate;
            string selectedMood = dialog.SelectedMood; // Get mood from dialog
            string dateString = PersianCalendarHelper.GetPersianDateString(selectedDate);
            UpdateStatusBar($"Creating journal for {dateString} with mood {selectedMood}...");

            // Pass mood to service
            string? newFileName = await Task.Run(() =>
                _journalService.CreateNewJournal(selectedDate, selectedMood, _password));

            if (newFileName != null)
            {
                LoadJournalList();
                var newEntryVm = FindJournalViewModelByFileName(newFileName);
                SelectedJournal = newEntryVm;
                UpdateStatusBar($"Created and selected journal for {dateString}.");
            }
            else
            {
                UpdateStatusBar($"Journal for {dateString} already exists or could not be created.");
                string existingFileName = $"{dateString}.txt";
                var existingEntryVm = FindJournalViewModelByFileName(existingFileName);
                if (existingEntryVm != null && SelectedJournal != existingEntryVm)
                {
                    SelectedJournal = existingEntryVm;
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
        // Search through the new nested structure: Year -> Month -> Journal
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
        return !string.IsNullOrEmpty(CurrentPasswordForChange) &&
               !string.IsNullOrEmpty(NewPassword) &&
               NewPassword == ConfirmNewPassword &&
               NewPassword != CurrentPasswordForChange &&
               !IsChangingPassword;
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePassword()
    {
        ChangePasswordErrorMessage = null;

        if (CurrentPasswordForChange != _password)
        {
            ChangePasswordErrorMessage = "The Current Password provided does not match the active password.";
            UpdateStatusBar("Password change failed: Incorrect current password.");
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
        await Task.Delay(50);

        bool success = await Task.Run(() =>
            _journalService.ChangeEncryptionPassword(_password, NewPassword!));

        IsChangingPassword = false;

        if (success)
        {
            _password = NewPassword!;
            CurrentPasswordForChange = null;
            NewPassword = null;
            ConfirmNewPassword = null;
            ChangePasswordErrorMessage = null;
            UpdateStatusBar("Password changed successfully. All journals re-encrypted.");
            if (SelectedJournal is not null) LoadSelectedJournalContent();
        }
        else
        {
            ChangePasswordErrorMessage = "Critical Error: Failed to re-encrypt one or more journals. Password has NOT been changed.";
            UpdateStatusBar("Error changing password. See error message. Password remains unchanged.");
        }
        ChangePasswordCommand.NotifyCanExecuteChanged();
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
            // Pass the JournalService, current password, and owner window to the dialog VM
            var exportViewModel = new ExportJournalsDialogViewModel(_journalService, _password, OwnerWindow);
            var exportDialog = new ExportJournalsDialog
            {
                DataContext = exportViewModel
            };

            // Show the dialog modally
            await exportDialog.ShowDialog(OwnerWindow); // We don't need a result back

            UpdateStatusBar("Export dialog closed.");
        }
        catch (Exception ex)
        {
            // Log the exception details if possible
            System.Diagnostics.Debug.WriteLine($"Error showing export dialog: {ex}");
            UpdateStatusBar($"Error: Could not open export dialog. {ex.Message}");
        }
    }
}
