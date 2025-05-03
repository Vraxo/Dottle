using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Avalonia.Layout;
using Avalonia.Media; // Required for Brushes
using Dottle.ViewModels;
using System;
using System.ComponentModel; // Required for PropertyChangedEventArgs
using System.Windows.Input; // Required for ICommand

namespace Dottle.Views;

public sealed class NewJournalDialog : Window
{
    private readonly DatePicker _datePicker;
    private readonly TextBlock _persianDateTextBlock;
    private readonly TextBlock _errorMessageTextBlock;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;

    private NewJournalDialogViewModel? _viewModel;
    private ICommand? _confirmCommandInstance;

    public bool IsConfirmed { get; private set; } = false;
    public DateTime SelectedDate { get; private set; }

    public NewJournalDialog()
    {
        Title = "Create New Journal";
        Width = 350;
        Height = 250; // Adjusted height
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
        // Bind SelectedDate manually
        _datePicker.SelectedDateChanged += (s, e) =>
        {
            if (_viewModel != null) _viewModel.SelectedDate = _datePicker.SelectedDate;
        };

        _persianDateTextBlock = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15),
            FontWeight = FontWeight.SemiBold
            // Text set via PropertyChanged
        };

        _errorMessageTextBlock = new TextBlock
        {
            Foreground = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            MinHeight = 20 // Reserve space
        };

        _confirmButton = new Button { Content = "Create Journal", IsDefault = true, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        _confirmButton.Classes.Add("accent");
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = new Button { Content = "Cancel", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        _cancelButton.Click += (s, e) => CloseDialog(false); // Use helper

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
                new TextBlock { Text = "Select Date for New Journal", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0,0,0,15), HorizontalAlignment = HorizontalAlignment.Center },
                _datePicker,
                _persianDateTextBlock,
                _errorMessageTextBlock,
                buttonPanel
            }
        };

        Content = mainPanel;
    }

    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
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

            // Initialize view state from VM
            _datePicker.SelectedDate = _viewModel.SelectedDate;
            UpdatePersianDateText(_viewModel.PersianDateString);
            UpdateErrorMessage(_viewModel.ErrorMessage);
            UpdateConfirmButtonState();
        }
        else
        {
            // Clear view state if VM is null
            _datePicker.SelectedDate = null;
            UpdatePersianDateText(null);
            UpdateErrorMessage(null);
            UpdateConfirmButtonState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(NewJournalDialogViewModel.SelectedDate):
                // Update DatePicker if changed from VM side (less common)
                if (_datePicker.SelectedDate != _viewModel.SelectedDate)
                {
                    _datePicker.SelectedDate = _viewModel.SelectedDate;
                }
                UpdateConfirmButtonState(); // CanExecute depends on SelectedDate
                break;
            case nameof(NewJournalDialogViewModel.PersianDateString):
                UpdatePersianDateText(_viewModel.PersianDateString);
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

            // Check if the command execution resulted in an error message
            if (string.IsNullOrEmpty(_viewModel?.ErrorMessage))
            {
                SelectedDate = _viewModel!.ConfirmedDate; // Get confirmed date from VM
                CloseDialog(true); // Close indicating success
            }
            // If ErrorMessage is set, dialog stays open
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
        Close(success); // Pass result to ShowDialogAsync
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