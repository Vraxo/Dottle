using Avalonia.Controls; // Required for Window reference if passing it
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Utils;
using System;

namespace Dottle.ViewModels;

public partial class NewJournalDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PersianDateString))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private DateTimeOffset? _selectedDate = DateTimeOffset.Now; // Default to today

    [ObservableProperty]
    private string? _errorMessage;

    public string PersianDateString => SelectedDate.HasValue
        ? PersianCalendarHelper.GetPersianDateString(SelectedDate.Value.Date)
        : "No date selected";

    // Result property set upon confirmation
    public DateTime ConfirmedDate { get; private set; }

    public NewJournalDialogViewModel()
    {
        // Initialize default date
        SelectedDate = DateTimeOffset.Now;
    }

    private bool CanConfirm()
    {
        return SelectedDate.HasValue;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        ErrorMessage = null;
        if (!SelectedDate.HasValue)
        {
            ErrorMessage = "Please select a date.";
            return; // Keep dialog open
        }

        ConfirmedDate = SelectedDate.Value.Date; // Store the confirmed date (ignore time)
        // Dialog closing will be handled by the View code-behind based on command execution success
    }
}