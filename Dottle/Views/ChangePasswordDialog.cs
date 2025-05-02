using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Avalonia.Layout;
using Avalonia.Media;
using Dottle.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks; // Added for async command execution check
using System.Windows.Input;

namespace Dottle.Views;

public sealed class ChangePasswordDialog : Window
{
    private readonly TextBox _oldPasswordBox;
    private readonly TextBox _newPasswordBox;
    private readonly TextBox _confirmPasswordBox;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private readonly TextBlock _errorMessageTextBlock;

    private ChangePasswordDialogViewModel? _viewModel;
    private ICommand? _confirmCommandInstance;

    public bool IsConfirmed { get; private set; } = false;

    public ChangePasswordDialog()
    {
        Title = "Change Password";
        Width = 400;
        Height = 300; // Adjusted height slightly for typical content
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.Full;

        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Controls ---
        _oldPasswordBox = new TextBox { Watermark = "Current Password", PasswordChar = '*', Margin = new Thickness(0, 0, 0, 10) };
        // Corrected Handler: Assign directly to ViewModel property
        _oldPasswordBox.TextChanged += (s, e) => { if (_viewModel != null) _viewModel.OldPassword = _oldPasswordBox.Text; };

        _newPasswordBox = new TextBox { Watermark = "New Password", PasswordChar = '*', Margin = new Thickness(0, 0, 0, 10) };
        // Corrected Handler: Assign directly to ViewModel property
        _newPasswordBox.TextChanged += (s, e) => { if (_viewModel != null) _viewModel.NewPassword = _newPasswordBox.Text; };

        _confirmPasswordBox = new TextBox { Watermark = "Confirm New Password", PasswordChar = '*', Margin = new Thickness(0, 0, 0, 15) };
        // Corrected Handler: Assign directly to ViewModel property
        _confirmPasswordBox.TextChanged += (s, e) => { if (_viewModel != null) _viewModel.ConfirmNewPassword = _confirmPasswordBox.Text; };

        _errorMessageTextBlock = new TextBlock
        {
            Foreground = Brushes.Red,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            MinHeight = 30 // Reserve space
        };

        _confirmButton = new Button { Content = "Confirm Change", IsDefault = true, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        _confirmButton.Classes.Add("accent");
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = new Button { Content = "Cancel", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        _cancelButton.Click += (s, e) => Close(false);

        // --- Layout ---
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right, // Align buttons to the right
            Children = { _cancelButton, _confirmButton }
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 5,
            Children =
            {
                new TextBlock { Text = "Change Journal Encryption Password", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0,0,0,15), HorizontalAlignment = HorizontalAlignment.Center },
                _oldPasswordBox,
                _newPasswordBox,
                _confirmPasswordBox,
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
        _viewModel = DataContext as ChangePasswordDialogViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _confirmCommandInstance = _viewModel.ConfirmChangeCommand;

            UpdateErrorMessage(_viewModel.ErrorMessage);
            UpdateConfirmButtonState();
            // Reset text boxes to reflect ViewModel state if needed (usually starts empty)
            _oldPasswordBox.Text = _viewModel.OldPassword;
            _newPasswordBox.Text = _viewModel.NewPassword;
            _confirmPasswordBox.Text = _viewModel.ConfirmNewPassword;
        }
        else
        {
            UpdateErrorMessage(null);
            UpdateConfirmButtonState();
            _oldPasswordBox.Text = null;
            _newPasswordBox.Text = null;
            _confirmPasswordBox.Text = null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(ChangePasswordDialogViewModel.ErrorMessage):
                UpdateErrorMessage(_viewModel.ErrorMessage);
                break;
            // These properties trigger CanExecute changes, handled by UpdateConfirmButtonState
            case nameof(ChangePasswordDialogViewModel.OldPassword):
            case nameof(ChangePasswordDialogViewModel.NewPassword):
            case nameof(ChangePasswordDialogViewModel.ConfirmNewPassword):
                UpdateConfirmButtonState();
                break;
        }
    }

    private async void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_confirmCommandInstance?.CanExecute(null) ?? false)
        {
            // Execute the command (which performs local validation)
            if (_confirmCommandInstance is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
            {
                await asyncCmd.ExecuteAsync(null);
            }
            else // Fallback for non-async command, though ours is async
            {
                _confirmCommandInstance.Execute(null);
            }

            // Check the ViewModel's error message *after* command execution
            if (string.IsNullOrEmpty(_viewModel?.ErrorMessage))
            {
                IsConfirmed = true;
                Close(true); // Close dialog indicating success
            }
            // If ErrorMessage is set, the dialog stays open, and the error is displayed
        }
    }

    private void UpdateErrorMessage(string? message)
    {
        _errorMessageTextBlock.Text = message ?? string.Empty;
    }

    private void UpdateConfirmButtonState()
    {
        _confirmButton.IsEnabled = _confirmCommandInstance?.CanExecute(null) ?? false;
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