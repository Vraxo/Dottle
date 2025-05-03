using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dottle.Utils;
using System;
using System.Collections.Generic; // Required for List
using System.Linq; // Required for Any

namespace Dottle.ViewModels;

public partial class NewJournalDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PersianDateString))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private DateTimeOffset? _selectedDate = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selectedMoodEmoji;

    [ObservableProperty]
    private string? _errorMessage;

    // Available moods for selection
    public List<string> AvailableMoods { get; } = ["🌩️", "🌧️", "🌥️", "☀️", "🌈"];

    public string PersianDateString => SelectedDate.HasValue
        ? PersianCalendarHelper.GetPersianDateString(SelectedDate.Value.Date)
        : "No date selected";

    public DateTime ConfirmedDate { get; private set; }
    public string ConfirmedMoodEmoji { get; private set; } = string.Empty;

    public NewJournalDialogViewModel()
    {
        SelectedDate = DateTimeOffset.Now;
        // Optionally set a default mood, or leave it null to force selection
        // SelectedMoodEmoji = AvailableMoods.FirstOrDefault();
    }

    private bool CanConfirm()
    {
        // Must have both date and mood selected
        return SelectedDate.HasValue && !string.IsNullOrEmpty(SelectedMoodEmoji);
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        ErrorMessage = null;
        if (!SelectedDate.HasValue)
        {
            ErrorMessage = "Please select a date.";
            return;
        }
        if (string.IsNullOrEmpty(SelectedMoodEmoji))
        {
            ErrorMessage = "Please select a mood.";
            return;
        }

        ConfirmedDate = SelectedDate.Value.Date;
        ConfirmedMoodEmoji = SelectedMoodEmoji; // Store confirmed mood

        // Dialog closing handled by View
    }
}