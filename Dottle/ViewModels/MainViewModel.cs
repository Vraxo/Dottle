using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Models;
using Dottle.Services;
using Dottle.Utils;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Dottle.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly JournalService _journalService;
    private string _password;

    // Replaced JournalGroups with a flattened list for the ListBox
    [ObservableProperty]
    private ObservableCollection<object> _displayItems = new();

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
    [NotifyCanExecuteChangedFor(nameof(CreateNewJournalCommand))]
    private string? _newJournalDateString;

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

    public bool IsJournalSelected => SelectedJournal != null;

    public MainViewModel(JournalService journalService, string password)
    {
        _journalService = journalService;
        _password = password;
        _newJournalDateString = PersianCalendarHelper.GetCurrentPersianDateString();
        LoadJournalList();
        UpdateStatusBar($"Journals loaded. Directory: {_journalService.GetJournalDirectoryPath()}");
    }

    partial void OnSelectedJournalChanged(JournalViewModel? value)
    {
        if (value == null)
        {
            CurrentJournalContent = string.Empty;
            UpdateStatusBar("No journal selected.");
            SaveJournalCommand.NotifyCanExecuteChanged();
            return;
        }
        // Only load if the selected item is actually a JournalViewModel
        LoadSelectedJournalContent();
    }

    private void LoadJournalList()
    {
        var entries = _journalService.GetJournalEntries();
        var viewModels = entries.Select(e => new JournalViewModel(e)).ToList();

        var grouped = viewModels
            .GroupBy(jvm => PersianCalendarHelper.GetPersianYear(jvm.Date))
            .OrderByDescending(g => g.Key);

        var newDisplayItems = new ObservableCollection<object>();
        foreach (var group in grouped)
        {
            newDisplayItems.Add(group.Key); // Add year header (int)
            foreach (var journalVm in group.OrderByDescending(j => j.Date))
            {
                newDisplayItems.Add(journalVm); // Add journal items
            }
        }

        DisplayItems = newDisplayItems; // Replace the collection

        // Check if the previously selected item still exists in the new list
        if (SelectedJournal != null && !DisplayItems.OfType<JournalViewModel>().Any(j => j.FileName == SelectedJournal.FileName))
        {
            SelectedJournal = null; // Deselect if the item is gone
        }
        else if (SelectedJournal == null)
        {
            CurrentJournalContent = string.Empty;
        }
        // If SelectedJournal exists, it remains selected.

        UpdateStatusBar("Journal list refreshed.");
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
        SaveJournalCommand.NotifyCanExecuteChanged();
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
        SaveJournalCommand.NotifyCanExecuteChanged();
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
    private async Task CreateNewJournal()
    {
        DateTime dateToCreate;
        string dateString;

        if (string.IsNullOrWhiteSpace(NewJournalDateString) || !PersianCalendarHelper.TryParsePersianDateString(NewJournalDateString, out dateToCreate))
        {
            UpdateStatusBar($"Invalid date format in input box. Using today's date.");
            dateToCreate = DateTime.Now;
            dateString = PersianCalendarHelper.GetPersianDateString(dateToCreate);
            NewJournalDateString = dateString;
        }
        else
        {
            dateString = NewJournalDateString;
        }

        UpdateStatusBar($"Attempting to create journal for {dateString}...");

        string? newFileName = await Task.Run(() =>
            _journalService.CreateNewJournal(dateToCreate, _password));

        if (newFileName != null)
        {
            UpdateStatusBar($"Journal created: {newFileName}. Reloading list...");
            LoadJournalList(); // Reload the flattened list
            await Task.Delay(50);

            var newEntryVm = FindJournalViewModelByFileName(newFileName);
            if (newEntryVm != null)
            {
                SelectedJournal = newEntryVm;
                UpdateStatusBar($"Created and selected journal for {dateString}.");
            }
            else
            {
                UpdateStatusBar($"Created journal {newFileName}, but could not auto-select it.");
            }
        }
        else
        {
            string existingFileName = $"{dateString}.txt";
            UpdateStatusBar($"Journal for {dateString} already exists or could not be created.");
        }
    }

    private JournalViewModel? FindJournalViewModelByFileName(string fileName)
    {
        // Search the flattened list for the specific JournalViewModel
        return DisplayItems
            .OfType<JournalViewModel>()
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
        ChangePasswordCommand.NotifyCanExecuteChanged();
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
}