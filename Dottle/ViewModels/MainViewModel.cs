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
    private string _password; // Keep internal track of the current valid password

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
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))] // Saving should wait for loading
    private bool _isLoadingContent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveJournalCommand))]
    private bool _isSavingContent;

    [ObservableProperty]
    private string? _statusBarText;

    // Properties specifically for the change password operation, set via dialog interaction
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _currentPasswordForChange;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _newPassword;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _confirmNewPassword; // Still needed for CanExecute

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
            .Select(g => new JournalGroupViewModel(g.Key, g.ToList()))
            .OrderByDescending(g => g.Year);

        JournalGroups = new ObservableCollection<JournalGroupViewModel>(groups);

        // Reset selection and content when list reloads
        SelectedJournal = null;
        CurrentJournalContent = string.Empty;
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
        CurrentJournalContent = string.Empty; // Clear previous content
        UpdateStatusBar($"Loading {SelectedJournal.DisplayName}...");
        // Provide visual feedback by delaying slightly
        await Task.Delay(50);

        string? content = await Task.Run(() => // Run potentially blocking file IO/decryption on background thread
            _journalService.ReadJournalContent(SelectedJournal.FileName, _password));

        IsLoadingContent = false; // Set loading false *before* updating content

        if (content != null)
        {
            CurrentJournalContent = content;
            UpdateStatusBar($"Loaded {SelectedJournal.DisplayName}.");
        }
        else
        {
            // Provide more specific feedback if possible (e.g., differentiate file not found from decryption error)
            // For now, use a generic error.
            CurrentJournalContent = $"Error: Could not load or decrypt '{SelectedJournal.FileName}'. Check password or file integrity.";
            UpdateStatusBar($"Error loading {SelectedJournal.DisplayName}.");
        }
        // Explicitly trigger CanExecuteChanged for Save command after content load attempt
        SaveJournalCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveJournal()
    {
        // Can save if a journal is selected, content isn't null, and not currently loading or saving
        return SelectedJournal != null &&
               CurrentJournalContent != null &&
               !IsLoadingContent &&
               !IsSavingContent;
    }

    [RelayCommand(CanExecute = nameof(CanSaveJournal))]
    private async Task SaveJournal()
    {
        if (SelectedJournal == null || CurrentJournalContent == null) return; // Should be prevented by CanExecute

        IsSavingContent = true;
        UpdateStatusBar($"Saving {SelectedJournal.DisplayName}...");
        await Task.Delay(50); // Brief delay for visual feedback

        // Run potentially blocking file IO/encryption on background thread
        bool success = await Task.Run(() =>
            _journalService.SaveJournalContent(SelectedJournal.FileName, CurrentJournalContent, _password));

        IsSavingContent = false; // Update state before status bar

        if (success)
        {
            UpdateStatusBar($"Saved {SelectedJournal.DisplayName} successfully.");
        }
        else
        {
            UpdateStatusBar($"Error: Failed to save {SelectedJournal.DisplayName}.");
        }
        // Explicitly trigger CanExecuteChanged after save attempt
        SaveJournalCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task CreateNewJournal() // Made async for consistency, though IO is quick
    {
        DateTime today = DateTime.Now;
        string dateString = PersianCalendarHelper.GetPersianDateString(today);
        UpdateStatusBar($"Creating journal for {dateString}...");

        string? newFileName = await Task.Run(() => // Run file IO on background thread
            _journalService.CreateNewJournal(today, _password));

        if (newFileName != null)
        {
            LoadJournalList(); // Reload the list to include the new entry
            // Find and select the newly created entry
            var newEntryVm = FindJournalViewModelByFileName(newFileName);
            SelectedJournal = newEntryVm; // This will trigger content load automatically
            UpdateStatusBar($"Created and selected journal for {dateString}.");
        }
        else
        {
            UpdateStatusBar($"Journal for {dateString} already exists or could not be created.");
            // Optionally select the existing entry if creation failed because it exists
            string existingFileName = $"{dateString}.txt";
            var existingEntryVm = FindJournalViewModelByFileName(existingFileName);
            if (existingEntryVm != null && SelectedJournal != existingEntryVm)
            {
                SelectedJournal = existingEntryVm; // Select existing if not already selected
            }
        }
    }

    private JournalViewModel? FindJournalViewModelByFileName(string fileName)
    {
        return JournalGroups
            .SelectMany(group => group.Journals)
            .FirstOrDefault(j => j.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateStatusBar(string message)
    {
        // Prepend timestamp for clarity
        StatusBarText = $"{DateTime.Now:HH:mm:ss} - {message}";
    }

    private bool CanChangePassword()
    {
        // Check if all required passwords are provided and match rules
        return !string.IsNullOrEmpty(CurrentPasswordForChange) &&
               !string.IsNullOrEmpty(NewPassword) &&
               NewPassword == ConfirmNewPassword && // New passwords must match
               NewPassword != CurrentPasswordForChange && // New must be different from old
               !IsChangingPassword; // Don't allow execution if already in progress
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePassword()
    {
        ChangePasswordErrorMessage = null; // Clear previous error

        // Perform final validation checks here, even though dialog might do some
        if (CurrentPasswordForChange != _password)
        {
            ChangePasswordErrorMessage = "The Current Password provided does not match the active password.";
            UpdateStatusBar("Password change failed: Incorrect current password.");
            return;
        }

        // These checks are redundant if CanExecute works correctly, but good defense
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
        await Task.Delay(50); // UI feedback delay

        // Execute the potentially long-running operation on a background thread
        bool success = await Task.Run(() =>
            _journalService.ChangeEncryptionPassword(_password, NewPassword!));

        IsChangingPassword = false; // Update state first

        if (success)
        {
            _password = NewPassword!; // IMPORTANT: Update the internally stored password
            CurrentPasswordForChange = null; // Clear sensitive fields after use
            NewPassword = null;
            ConfirmNewPassword = null;
            ChangePasswordErrorMessage = null; // Clear any previous error message
            UpdateStatusBar("Password changed successfully. All journals re-encrypted.");
            // Optionally reload current journal if one is selected, as its content is now encrypted with new password
            if (SelectedJournal is not null) LoadSelectedJournalContent();
        }
        else
        {
            // Don't clear the input fields on failure, user might want to retry
            ChangePasswordErrorMessage = "Critical Error: Failed to re-encrypt one or more journals. Password has NOT been changed.";
            UpdateStatusBar("Error changing password. See error message. Password remains unchanged.");
        }
        // Explicitly notify CanExecute changed as IsChangingPassword has changed
        ChangePasswordCommand.NotifyCanExecuteChanged();
    }
}