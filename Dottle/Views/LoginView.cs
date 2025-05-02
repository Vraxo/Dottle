using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Avalonia.Layout;
using Avalonia.Media;
using Dottle.ViewModels;
using System; // Required for EventArgs
using System.ComponentModel; // Required for PropertyChangedEventArgs
using System.Windows.Input; // Required for ICommand

namespace Dottle.Views;

public sealed class LoginView : UserControl
{
    private readonly TextBox _passwordTextBox;
    private readonly Button _loginButton;
    private readonly TextBlock _errorMessageTextBlock;
    private readonly ProgressBar _progressBar;

    // Store ViewModel and Command references
    private LoginViewModel? _viewModel;
    private ICommand? _loginCommandInstance;

    public LoginView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Step 1: Create and Configure Controls (NO BINDINGS) ---

        var titleBlock = new TextBlock
        {
            Text = "Dottle Journal",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 24,
            Margin = new Avalonia.Thickness(0, 0, 0, 20)
        };
        Grid.SetRow(titleBlock, 0);

        _passwordTextBox = new TextBox
        {
            Watermark = "Password",
            PasswordChar = '*', // Use '*' to mask input visually
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(_passwordTextBox, 1);
        _passwordTextBox.TextChanged += PasswordTextBox_TextChanged; // Manual VM update

        _loginButton = new Button
        {
            Content = "Login",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Avalonia.Thickness(0, 5, 0, 10),
            IsEnabled = false // Start disabled, enabled via UpdateLoginButtonState
        };
        _loginButton.Classes.Add("accent"); // Apply theme accent style
        Grid.SetRow(_loginButton, 2);
        _loginButton.Click += LoginButton_Click; // Manual command execution

        _errorMessageTextBlock = new TextBlock
        {
            Foreground = Brushes.Red, // Error messages in red
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap, // Wrap long messages
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
            // Text set manually via PropertyChanged
        };
        Grid.SetRow(_errorMessageTextBlock, 3);

        _progressBar = new ProgressBar
        {
            MinWidth = 150,
            IsIndeterminate = true, // Show busy state
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 5),
            IsVisible = false // Start hidden
            // Visibility set manually via PropertyChanged
        };
        Grid.SetRow(_progressBar, 4);

        // --- Step 2: Create Layout Panel and Add Children ---
        var gridLayout = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            MaxWidth = 300,
            // Add the created controls to the Grid's children
            Children =
            {
                titleBlock,
                _passwordTextBox,
                _loginButton,
                _errorMessageTextBlock,
                _progressBar
            }
        };

        // --- Step 3: Set Content ---
        Content = gridLayout;
    }

    // Handle ViewModel changes
    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel if exists
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _loginCommandInstance = null; // Clear previous command instance
        _viewModel = DataContext as LoginViewModel; // Store new ViewModel

        if (_viewModel != null)
        {
            // Subscribe to new ViewModel's changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _loginCommandInstance = _viewModel.LoginCommand; // Store command

            // Manually update View state based on initial ViewModel state
            UpdateErrorMessage(_viewModel.ErrorMessage);
            UpdateProgressVisibility(_viewModel.IsLoggingIn);
            UpdateLoginButtonState(); // Update button based on CanExecute
        }
        else
        {
            // Clear View state if ViewModel is null
            UpdateErrorMessage(null);
            UpdateProgressVisibility(false);
            UpdateLoginButtonState();
        }
    }

    // React to ViewModel property changes
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        // Update relevant View control based on changed ViewModel property
        switch (e.PropertyName)
        {
            case nameof(LoginViewModel.ErrorMessage):
                // Update error message display
                UpdateErrorMessage(_viewModel.ErrorMessage);
                break;

            case nameof(LoginViewModel.IsLoggingIn):
                // Update progress bar visibility and button state
                UpdateProgressVisibility(_viewModel.IsLoggingIn);
                UpdateLoginButtonState();
                break;

            case nameof(LoginViewModel.Password):
                // Update button state when password changes (affects CanExecute)
                UpdateLoginButtonState();
                break;
        }
    }

    // Update password in ViewModel when TextBox changes
    private void PasswordTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel != null && sender is TextBox tb)
        {
            _viewModel.Password = tb.Text; // Manual two-way update
        }
    }

    // Execute command when button is clicked
    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        // Execute the command stored in the field if it can be executed
        if (_loginCommandInstance?.CanExecute(null) ?? false)
        {
            _loginCommandInstance.Execute(null);
        }
    }

    // --- Helper methods for updating View state ---
    private void UpdateErrorMessage(string? message)
    {
        // Set the TextBlock's text, defaulting to empty if message is null
        _errorMessageTextBlock.Text = message ?? string.Empty;
    }

    private void UpdateProgressVisibility(bool isVisible)
    {
        // Set the ProgressBar's visibility
        _progressBar.IsVisible = isVisible;
    }

    private void UpdateLoginButtonState()
    {
        // Enable/disable button based on the stored command's CanExecute result
        _loginButton.IsEnabled = _loginCommandInstance?.CanExecute(null) ?? false;
    }

    // Ensure cleanup on unload
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Unsubscribe from PropertyChanged event to prevent memory leaks
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        // Clear references
        _viewModel = null;
        _loginCommandInstance = null;
    }
}