using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Dottle.ViewModels;

namespace Dottle.Views;

public sealed class NewJournalDialog : Window
{
    private readonly DatePicker _datePicker;
    private readonly TextBlock _persianDateTextBlock;
    private readonly StackPanel _moodSelectionPanel; // Panel to hold mood selectors
    private readonly TextBlock _errorMessageTextBlock;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;

    private NewJournalDialogViewModel? _viewModel;
    private ICommand? _confirmCommandInstance;

    public bool IsConfirmed { get; private set; } = false;
    public DateTime SelectedDate { get; private set; }
    public string SelectedMood { get; private set; } = string.Empty; // Store selected mood

    public NewJournalDialog()
    {
        Title = "Create New Journal";
        Width = 380; // Slightly wider for moods
        Height = 320; // Increased height for moods
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.Full;

        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Controls ---
        _datePicker = new DatePicker
        {
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _datePicker.SelectedDateChanged += (s, e) =>
        {
            if (_viewModel != null) _viewModel.SelectedDate = _datePicker.SelectedDate;
        };

        _persianDateTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10), // Adjusted margin
            FontWeight = FontWeight.SemiBold
        };

        // --- Mood Selection Panel ---
        _moodSelectionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        };
        // RadioButtons will be added dynamically in OnDataContextChangedHandler

        _errorMessageTextBlock = new TextBlock
        {
            Foreground = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            MinHeight = 20
        };

        _confirmButton = new Button { Content = "Create Journal", IsDefault = true, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        _confirmButton.Classes.Add("accent");
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = new Button { Content = "Cancel", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        _cancelButton.Click += (s, e) => CloseDialog(false);

        // --- Layout ---
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _cancelButton, _confirmButton }
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 5,
            Children =
            {
                new TextBlock { Text = "Select Date and Mood", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0,0,0,15), HorizontalAlignment = HorizontalAlignment.Center },
                _datePicker,
                _persianDateTextBlock,
                new TextBlock { Text = "Select Mood:", HorizontalAlignment= HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 5)},
                _moodSelectionPanel, // Add mood panel here
                _errorMessageTextBlock,
                buttonPanel
            }
        };

        Content = mainPanel;
    }

    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        // Clear previous state and unsubscribe
        _moodSelectionPanel.Children.Clear();
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _confirmCommandInstance = null;
        _viewModel = DataContext as NewJournalDialogViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _confirmCommandInstance = _viewModel.ConfirmCommand;

            // Populate Mood RadioButtons
            string? currentVmSelection = _viewModel.SelectedMoodEmoji;
            int index = 0;
            foreach (string mood in _viewModel.AvailableMoods)
            {
                var radioButton = new RadioButton
                {
                    Content = mood,
                    GroupName = "MoodGroup",
                    FontSize = 18, // Make emojis bigger
                    Tag = mood // Store the mood string in the Tag
                };
                radioButton.Checked += MoodRadioButton_Checked;
                // Set initial checked state based on ViewModel
                if (mood == currentVmSelection)
                {
                    radioButton.IsChecked = true;
                }
                _moodSelectionPanel.Children.Add(radioButton);
                index++;
            }

            // Initialize other view state from VM
            _datePicker.SelectedDate = _viewModel.SelectedDate;
            UpdatePersianDateText(_viewModel.PersianDateString);
            UpdateErrorMessage(_viewModel.ErrorMessage);
            UpdateConfirmButtonState();
        }
        else
        {
            _datePicker.SelectedDate = null;
            UpdatePersianDateText(null);
            UpdateErrorMessage(null);
            UpdateConfirmButtonState();
        }
    }

    // Update ViewModel when a mood RadioButton is checked
    private void MoodRadioButton_Checked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null && sender is RadioButton rb && rb.IsChecked == true && rb.Tag is string mood)
        {
            _viewModel.SelectedMoodEmoji = mood;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(NewJournalDialogViewModel.SelectedDate):
                if (_datePicker.SelectedDate != _viewModel.SelectedDate)
                {
                    _datePicker.SelectedDate = _viewModel.SelectedDate;
                }
                UpdateConfirmButtonState();
                break;
            case nameof(NewJournalDialogViewModel.PersianDateString):
                UpdatePersianDateText(_viewModel.PersianDateString);
                break;
            case nameof(NewJournalDialogViewModel.SelectedMoodEmoji):
                // Sync RadioButton state if VM changes mood (less common)
                string? vmMood = _viewModel.SelectedMoodEmoji;
                foreach (var rb in _moodSelectionPanel.Children.OfType<RadioButton>())
                {
                    if (rb.Tag is string rbMood)
                    {
                        rb.IsChecked = (rbMood == vmMood);
                    }
                }
                UpdateConfirmButtonState();
                break;
            case nameof(NewJournalDialogViewModel.ErrorMessage):
                UpdateErrorMessage(_viewModel.ErrorMessage);
                break;
        }
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_confirmCommandInstance?.CanExecute(null) ?? false)
        {
            _confirmCommandInstance.Execute(null);

            if (string.IsNullOrEmpty(_viewModel?.ErrorMessage))
            {
                SelectedDate = _viewModel!.ConfirmedDate;
                SelectedMood = _viewModel!.ConfirmedMoodEmoji; // Get confirmed mood
                CloseDialog(true);
            }
        }
    }

    private void UpdatePersianDateText(string? text)
    {
        _persianDateTextBlock.Text = text ?? string.Empty;
    }

    private void UpdateErrorMessage(string? message)
    {
        _errorMessageTextBlock.Text = message ?? string.Empty;
    }

    private void UpdateConfirmButtonState()
    {
        _confirmButton.IsEnabled = _confirmCommandInstance?.CanExecute(null) ?? false;
    }

    private void CloseDialog(bool success)
    {
        IsConfirmed = success;
        Close(success);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        _viewModel = null;
        _confirmCommandInstance = null;
    }
}