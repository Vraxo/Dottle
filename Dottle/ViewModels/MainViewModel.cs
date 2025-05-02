using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Dottle.Services;
using Dottle.Models;
using Dottle.Utils;
using System.Collections.Generic; // Required for List
using System.Security; // For SecureString potentially, though sticking to string for now
using System.Windows.Input; // Required for ICommand if not using RelayCommand directly everywhere

namespace Dottle.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly JournalService _journalService;
    private string _password; // Store the password securely (or derived key). Made mutable for password change.

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
    private bool _isLoadingContent;

    [ObservableProperty]
    private bool _isSavingContent;

    [ObservableProperty]
    private string? _statusBarText;

    // --- Password Management Properties ---
    [ObservableProperty]
    private bool _isPasswordVisible = false;

    [ObservableProperty]
    private string? _currentPasswordForChange; // Input field for current password verification

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _newPassword; // Input field for new password

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangePasswordCommand))]
    private string? _confirmNewPassword; // Input field for confirming new password

    [ObservableProperty]
    private string? _changePasswordErrorMessage;

    [ObservableProperty]
    private bool _isChangingPassword;
    // --- End Password Management Properties ---

    public bool IsJournalSelected => SelectedJournal != null;

    // Computed property to display the password (masked or plain)
    public string DisplayPassword => IsPasswordVisible ? _password : new string('•', _password?.Length ?? 0);

    public MainViewModel(JournalService journalService, string password)
    {
        _journalService = journalService;
        _password = password; // Be cautious with storing raw passwords
        LoadJournalList();
        UpdateStatusBar($"Journals loaded from: {_journalService.GetJournalDirectoryPath()}");
    }

    // Notify UI that DisplayPassword needs refresh when IsPasswordVisible changes
    partial void OnIsPasswordVisibleChanged(bool value) => OnPropertyChanged(nameof(DisplayPassword));

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
        SelectedJournal = null; // Ensure no journal is selected initially or after reload
        CurrentJournalContent = null; // Clear content
    }

    private async void LoadSelectedJournalContent()
    {
        if (SelectedJournal == null)
        {
            CurrentJournalContent = string.Empty;
            return;
        }

        IsLoadingContent = true;
        CurrentJournalContent = string.Empty; // Clear previous content immediately
        UpdateStatusBar($"Loading {SelectedJournal.DisplayName}...");

        // Simulate loading delay and perform actual load
        await Task.Delay(50); // Small delay for UI update responsiveness
        string? content = _journalService.ReadJournalContent(SelectedJournal.FileName, _password);

        if (content != null)
        {
            CurrentJournalContent = content;
            UpdateStatusBar($"Loaded {SelectedJournal.DisplayName}.");
        }
        else
        {
            // Handle decryption failure or file not found
            CurrentJournalContent = $"Error: Could not load or decrypt {SelectedJournal.FileName}.";
            UpdateStatusBar($"Error loading {SelectedJournal.DisplayName}.");
        }
        IsLoadingContent = false;
    }

    private bool CanSaveJournal()
    {
        return SelectedJournal != null && CurrentJournalContent != null && !IsSavingContent;
    }

    [RelayCommand(CanExecute = nameof(CanSaveJournal))]
    private async Task SaveJournal()
    {
        if (SelectedJournal == null || CurrentJournalContent == null) return;

        IsSavingContent = true;
        UpdateStatusBar($"Saving {SelectedJournal.DisplayName}...");
        await Task.Delay(50); // Small delay for UI responsiveness

        bool success = _journalService.SaveJournalContent(SelectedJournal.FileName, CurrentJournalContent, _password);

        if (success)
        {
            UpdateStatusBar($"Saved {SelectedJournal.DisplayName}.");
        }
        else
        {
            UpdateStatusBar($"Error saving {SelectedJournal.DisplayName}.");
            // Optionally show a more prominent error message
        }
        IsSavingContent = false;
    }

    [RelayCommand]
    private void CreateNewJournal()
    {
        DateTime today = DateTime.Now;
        string? newFileName = _journalService.CreateNewJournal(today, _password);

        if (newFileName != null)
        {
            LoadJournalList(); // Refresh the list
            // Find and select the newly created journal
            var newEntryVm = FindJournalViewModelByFileName(newFileName);
            SelectedJournal = newEntryVm;
            UpdateStatusBar($"Created new journal for {PersianCalendarHelper.GetPersianDateString(today)}.");
        }
        else
        {
            // Handle error - maybe journal for today already exists
            UpdateStatusBar($"Journal for {PersianCalendarHelper.GetPersianDateString(today)} already exists or could not be created.");
            // Optionally try to select the existing journal for today
            string existingFileName = $"{PersianCalendarHelper.GetPersianDateString(today)}.txt";
            SelectedJournal = FindJournalViewModelByFileName(existingFileName);
        }
    }

    private JournalViewModel? FindJournalViewModelByFileName(string fileName)
    {
        foreach (var group in JournalGroups)
        {
            var found = group.Journals.FirstOrDefault(j => j.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private void UpdateStatusBar(string message)
    {
        StatusBarText = $"{DateTime.Now:HH:mm:ss} - {message}";
    }

    // --- Password Management Commands ---

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private bool CanChangePassword()
    {
        return !string.IsNullOrEmpty(CurrentPasswordForChange) &&
               !string.IsNullOrEmpty(NewPassword) &&
               !string.IsNullOrEmpty(ConfirmNewPassword) &&
               NewPassword == ConfirmNewPassword &&
               !IsChangingPassword;
    }

    [RelayCommand(CanExecute = nameof(CanChangePassword))]
    private async Task ChangePassword()
    {
        ChangePasswordErrorMessage = null; // Clear previous errors

        // 1. Verify Current Password
        if (CurrentPasswordForChange != _password)
        {
            ChangePasswordErrorMessage = "Current password does not match.";
            return;
        }

        // 2. Basic validation (redundant due to CanExecute, but good practice)
        if (string.IsNullOrEmpty(NewPassword) || NewPassword != ConfirmNewPassword)
        {
            ChangePasswordErrorMessage = "New passwords do not match or are empty.";
            return;
        }

        // 3. Prevent changing to the same password (optional)
        if (NewPassword == _password)
        {
            ChangePasswordErrorMessage = "New password cannot be the same as the old password.";
            return;
        }

        IsChangingPassword = true;
        UpdateStatusBar("Changing password and re-encrypting journals...");
        await Task.Delay(50); // UI responsiveness

        // 4. Call Service to re-encrypt
        bool success = await Task.Run(() => // Run potentially long operation in background
            _journalService.ChangeEncryptionPassword(_password, NewPassword));

        if (success)
        {
            // 5. Update stored password in ViewModel
            _password = NewPassword;

            // 6. Clear fields and provide feedback
            CurrentPasswordForChange = null;
            NewPassword = null;
            ConfirmNewPassword = null;
            OnPropertyChanged(nameof(CurrentPasswordForChange)); // Notify UI to clear fields
            OnPropertyChanged(nameof(NewPassword));
            OnPropertyChanged(nameof(ConfirmNewPassword));
            OnPropertyChanged(nameof(DisplayPassword)); // Update displayed password (masked)
            UpdateStatusBar("Password changed successfully. All journals re-encrypted.");
            ChangePasswordErrorMessage = "Password changed successfully!"; // Success message
        }
        else
        {
            // Service layer failed
            ChangePasswordErrorMessage = "Failed to re-encrypt journals. Password not changed.";
            UpdateStatusBar("Error changing password.");
            // Consider more specific error handling if the service provided details
        }

        IsChangingPassword = false;
    }

    // --- End Password Management Commands ---
}
