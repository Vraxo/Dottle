using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For ScrollViewer
using Avalonia.Controls.Templates; // For FuncDataTemplate, IDataTemplate
using Avalonia.Input;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates; // Required for FuncDataTemplate
using Avalonia.Media;
// using Avalonia.Data; // No longer needed for this view
using Dottle.ViewModels;
using System; // Required for EventArgs
using System.Collections.Generic; // Required for ListBox item comparison if needed
using System.ComponentModel; // Required for PropertyChangedEventArgs
using System.Linq; // Required for ListBox item comparison if needed
using System.Windows.Input; // Required for ICommand

namespace Dottle.Views;

public sealed class MainView : UserControl
{
    private readonly ListBox _journalListBox;
    private readonly TextBox _editorTextBox;
    private readonly TextBlock _statusBarTextBlock;
    private readonly Button _saveButton;
    private readonly Button _newButton;

    // --- Password Management UI Elements ---
    private readonly TextBlock _displayPasswordTextBlock;
    private readonly Button _togglePasswordVisibilityButton;
    private readonly TextBox _currentPasswordTextBox;
    private readonly TextBox _newPasswordTextBox;
    private readonly TextBox _confirmPasswordTextBox;
    private readonly Button _changePasswordButton;
    private readonly TextBlock _changePasswordStatusTextBlock;
    private readonly StackPanel _passwordPanel; // Container for password controls
    // --- End Password Management UI Elements ---

    // Store ViewModel and Command references
    private MainViewModel? _viewModel;
    private ICommand? _createNewJournalCommand;
    private ICommand? _saveJournalCommand;
    // --- Password Management Commands ---
    private ICommand? _togglePasswordVisibilityCommand;
    private ICommand? _changePasswordCommand;
    // --- End Password Management Commands ---

    // Flag to prevent sync loops
    private bool _isSyncingSelection = false;
    private bool _isSyncingText = false;
    // --- Password Management Sync Flags ---
    private bool _isSyncingCurrentPassword = false;
    private bool _isSyncingNewPassword = false;
    private bool _isSyncingConfirmPassword = false;
    // --- End Password Management Sync Flags ---

    public MainView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Left Panel Components (NO BINDINGS) ---
        _newButton = new Button
        {
            Content = "New Journal",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Avalonia.Thickness(5),
            IsEnabled = false // Start disabled until command is checked
        };
        DockPanel.SetDock(_newButton, Dock.Top);
        _newButton.Click += NewButton_Click; // Manual command execution

        _journalListBox = new ListBox
        {
            Background = Brushes.Transparent,
            ItemTemplate = JournalDataTemplate() // Template still needed
            // ItemsSource and SelectedItem set manually
        };
        _journalListBox.SelectionChanged += JournalListBox_SelectionChanged; // Manual VM update

        var journalListScrollViewer = new ScrollViewer { Content = _journalListBox };
        var leftDockPanel = new DockPanel { Children = { _newButton, journalListScrollViewer } };
        var leftBorder = new Border
        {
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Avalonia.Thickness(0, 0, 1, 0),
            Child = leftDockPanel
        };
        Grid.SetColumn(leftBorder, 0); Grid.SetRow(leftBorder, 0);

        // --- Right Panel Components (NO BINDINGS) ---
        _saveButton = new Button
        {
            Content = "Save Journal",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(5),
            IsEnabled = false // Start disabled
        };
        _saveButton.Classes.Add("accent");
        DockPanel.SetDock(_saveButton, Dock.Top);
        _saveButton.Click += SaveButton_Click; // Manual command execution

        _editorTextBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            FontSize = 13,
            Padding = new Avalonia.Thickness(5),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false, // Start disabled
            IsReadOnly = false // Start editable
            // Text, IsEnabled, IsReadOnly set manually
        };
        _editorTextBox.TextChanged += EditorTextBox_TextChanged; // Manual VM update

        // --- Password Management Panel Setup ---
        _displayPasswordTextBlock = new TextBlock
        {
            Margin = new Thickness(5, 10, 5, 0),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            VerticalAlignment = VerticalAlignment.Center
            // Text set manually
        };

        _togglePasswordVisibilityButton = new Button
        {
            Content = "Show", // Initial text, updated later
            Margin = new Thickness(5, 10, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 60
            // IsEnabled set manually
        };
        _togglePasswordVisibilityButton.Click += TogglePasswordVisibilityButton_Click;

        var currentPasswordPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _displayPasswordTextBlock, _togglePasswordVisibilityButton }
        };

        _currentPasswordTextBox = new TextBox { Watermark = "Current Password", Margin = new Thickness(5), PasswordChar = '•' };
        _newPasswordTextBox = new TextBox { Watermark = "New Password", Margin = new Thickness(5), PasswordChar = '•' };
        _confirmPasswordTextBox = new TextBox { Watermark = "Confirm New Password", Margin = new Thickness(5), PasswordChar = '•' };

        _currentPasswordTextBox.TextChanged += CurrentPasswordTextBox_TextChanged;
        _newPasswordTextBox.TextChanged += NewPasswordTextBox_TextChanged;
        _confirmPasswordTextBox.TextChanged += ConfirmPasswordTextBox_TextChanged;

        _changePasswordButton = new Button
        {
            Content = "Change Password",
            Margin = new Thickness(5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false // Start disabled
        };
        _changePasswordButton.Click += ChangePasswordButton_Click;

        _changePasswordStatusTextBlock = new TextBlock
        {
            Margin = new Thickness(5, 0, 5, 5),
            Foreground = Brushes.OrangeRed // Default to error color, can change
            // Text set manually
        };

        _passwordPanel = new StackPanel
        {
            Margin = new Thickness(0, 10, 0, 0),
            Background = SolidColorBrush.Parse("#282828"), // Slightly different background
            IsVisible = true, // Or bind to a VM property if needed
            Children =
            {
                new TextBlock { Text = "Password Management", FontWeight = FontWeight.Bold, Margin = new Thickness(5, 5, 5, 10) },
                currentPasswordPanel,
                new Separator { Margin = new Thickness(5, 10) },
                _currentPasswordTextBox,
                _newPasswordTextBox,
                _confirmPasswordTextBox,
                _changePasswordButton,
                _changePasswordStatusTextBlock
            }
        };
        DockPanel.SetDock(_passwordPanel, Dock.Bottom); // Add below editor

        // Add password panel to the right dock panel
        var rightDockPanel = new DockPanel { Children = { _saveButton, _passwordPanel, _editorTextBox } }; // Editor fills remaining space
        Grid.SetColumn(rightDockPanel, 1); Grid.SetRow(rightDockPanel, 0);

        // --- Status Bar Components (NO BINDINGS) ---
        _statusBarTextBlock = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
            // Text set manually
        };
        var bottomBorder = new Border
        {
            Background = SolidColorBrush.Parse("#2a2a2a"),
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Avalonia.Thickness(0, 1, 0, 0),
            Padding = new Avalonia.Thickness(5, 3),
            Child = _statusBarTextBlock
        };
        Grid.SetColumn(bottomBorder, 0); Grid.SetRow(bottomBorder, 1); Grid.SetColumnSpan(bottomBorder, 2);

        // --- Main Grid Layout ---
        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { leftBorder, rightDockPanel, bottomBorder }
        };
    }

    // Handle ViewModel changes
    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        // Clear old command references
        _createNewJournalCommand = null;
        _saveJournalCommand = null;
        _togglePasswordVisibilityCommand = null;
        _changePasswordCommand = null;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            // Get new command references
            _createNewJournalCommand = _viewModel.CreateNewJournalCommand;
            _saveJournalCommand = _viewModel.SaveJournalCommand;
            _togglePasswordVisibilityCommand = _viewModel.TogglePasswordVisibilityCommand;
            _changePasswordCommand = _viewModel.ChangePasswordCommand;

            // Manually update View state based on initial ViewModel state
            UpdateJournalList(_viewModel.JournalGroups);
            UpdateEditorText(_viewModel.CurrentJournalContent);
            UpdateSelectedItem(_viewModel.SelectedJournal);
            UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
            UpdateStatusBar(_viewModel.StatusBarText);
            UpdateNewButtonState();
            UpdateSaveButtonState();
            // --- Update Password Controls ---
            UpdateDisplayPassword(_viewModel.DisplayPassword);
            UpdatePasswordVisibilityButton(_viewModel.IsPasswordVisible);
            UpdateCurrentPasswordInput(_viewModel.CurrentPasswordForChange);
            UpdateNewPasswordInput(_viewModel.NewPassword);
            UpdateConfirmPasswordInput(_viewModel.ConfirmNewPassword);
            UpdateChangePasswordStatus(_viewModel.ChangePasswordErrorMessage);
            UpdateChangePasswordButtonState(); // Depends on CanExecute
            // --- End Update Password Controls ---
        }
        else
        {
            // Clear View state if ViewModel is null
            UpdateJournalList(null);
            UpdateEditorText(null);
            UpdateEditorState(false, false);
            UpdateStatusBar(null);
            UpdateNewButtonState();
            UpdateSaveButtonState();
            // --- Clear Password Controls ---
            UpdateDisplayPassword(null);
            UpdatePasswordVisibilityButton(false);
            UpdateCurrentPasswordInput(null);
            UpdateNewPasswordInput(null);
            UpdateConfirmPasswordInput(null);
            UpdateChangePasswordStatus(null);
            UpdateChangePasswordButtonState();
            // --- End Clear Password Controls ---
        }
    }

    // React to ViewModel property changes
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        // Update relevant View control based on changed ViewModel property
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.JournalGroups):
                UpdateJournalList(_viewModel.JournalGroups);
                break;
            case nameof(MainViewModel.SelectedJournal):
                UpdateSelectedItem(_viewModel.SelectedJournal);
                // Also update controls dependent on selection state
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                UpdateSaveButtonState();
                break;
            case nameof(MainViewModel.CurrentJournalContent):
                UpdateEditorText(_viewModel.CurrentJournalContent);
                UpdateSaveButtonState(); // Content change might affect CanSave
                break;
            case nameof(MainViewModel.IsLoadingContent):
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                break;
            case nameof(MainViewModel.IsSavingContent):
                // Update save button state if saving affects CanSave
                UpdateSaveButtonState();
                break;
            case nameof(MainViewModel.StatusBarText):
                UpdateStatusBar(_viewModel.StatusBarText);
                break;

            // --- Password Management Property Changes ---
            case nameof(MainViewModel.DisplayPassword):
                UpdateDisplayPassword(_viewModel.DisplayPassword);
                break;
            case nameof(MainViewModel.IsPasswordVisible):
                UpdatePasswordVisibilityButton(_viewModel.IsPasswordVisible);
                break;
            case nameof(MainViewModel.CurrentPasswordForChange):
                UpdateCurrentPasswordInput(_viewModel.CurrentPasswordForChange);
                UpdateChangePasswordButtonState(); // Affects CanExecute
                break;
            case nameof(MainViewModel.NewPassword):
                UpdateNewPasswordInput(_viewModel.NewPassword);
                UpdateChangePasswordButtonState(); // Affects CanExecute
                break;
            case nameof(MainViewModel.ConfirmNewPassword):
                UpdateConfirmPasswordInput(_viewModel.ConfirmNewPassword);
                UpdateChangePasswordButtonState(); // Affects CanExecute
                break;
            case nameof(MainViewModel.ChangePasswordErrorMessage):
                UpdateChangePasswordStatus(_viewModel.ChangePasswordErrorMessage);
                break;
            case nameof(MainViewModel.IsChangingPassword):
                // This might affect CanExecute, so update button state
                UpdateChangePasswordButtonState();
                // Optionally disable input fields while changing
                _currentPasswordTextBox.IsEnabled = !_viewModel.IsChangingPassword;
                _newPasswordTextBox.IsEnabled = !_viewModel.IsChangingPassword;
                _confirmPasswordTextBox.IsEnabled = !_viewModel.IsChangingPassword;
                break;
                // --- End Password Management Property Changes ---
        }
    }

    // --- Event Handlers for Manual VM Updates ---

    private void JournalListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingSelection) return;

        // Update ViewModel from ListBox selection
        _viewModel.SelectedJournal = _journalListBox.SelectedItem as JournalViewModel;
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingText) return;

        // Update ViewModel from TextBox text
        _viewModel.CurrentJournalContent = _editorTextBox.Text;
    }

    // --- Event Handlers for Manual Password VM Updates ---

    private void CurrentPasswordTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingCurrentPassword) return;
        _viewModel.CurrentPasswordForChange = _currentPasswordTextBox.Text;
    }

    private void NewPasswordTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingNewPassword) return;
        _viewModel.NewPassword = _newPasswordTextBox.Text;
    }

    private void ConfirmPasswordTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingConfirmPassword) return;
        _viewModel.ConfirmNewPassword = _confirmPasswordTextBox.Text;
    }

    // --- Event Handlers for Manual Command Execution ---

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_createNewJournalCommand?.CanExecute(null) ?? false)
        {
            _createNewJournalCommand.Execute(null);
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_saveJournalCommand?.CanExecute(null) ?? false)
        {
            _saveJournalCommand.Execute(null);
        }
    }

    private void TogglePasswordVisibilityButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_togglePasswordVisibilityCommand?.CanExecute(null) ?? false)
        {
            _togglePasswordVisibilityCommand.Execute(null);
        }
    }

    private void ChangePasswordButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_changePasswordCommand?.CanExecute(null) ?? false)
        {
            _changePasswordCommand.Execute(null);
        }
    }

    // --- Helper methods for updating View state ---

    private void UpdateJournalList(IEnumerable<JournalGroupViewModel>? groups)
    {
        _journalListBox.ItemsSource = groups;
        // Manually clear selection in ListBox if VM selection is null
        // This might be needed if resetting ItemsSource doesn't clear SelectedItem reliably
        if (_viewModel?.SelectedJournal == null)
        {
            UpdateSelectedItem(null);
        }
    }

    private void UpdateSelectedItem(JournalViewModel? selectedJournalVm)
    {
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            if (!object.Equals(_journalListBox.SelectedItem, selectedJournalVm))
            {
                _journalListBox.SelectedItem = selectedJournalVm;
                if (selectedJournalVm != null)
                {
                    _journalListBox.ScrollIntoView(selectedJournalVm);
                }
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void UpdateEditorText(string? text)
    {
        if (_isSyncingText) return;

        _isSyncingText = true;
        try
        {
            if (_editorTextBox.Text != text)
            {
                _editorTextBox.Text = text ?? string.Empty;
                // If needed, reset caret or scroll position here
            }
        }
        finally
        {
            _isSyncingText = false;
        }
    }

    private void UpdateEditorState(bool isEnabled, bool isReadOnly)
    {
        _editorTextBox.IsEnabled = isEnabled;
        _editorTextBox.IsReadOnly = isReadOnly;
    }

    private void UpdateStatusBar(string? text)
    {
        _statusBarTextBlock.Text = text ?? string.Empty;
    }

    private void UpdateNewButtonState()
    {
        _newButton.IsEnabled = _createNewJournalCommand?.CanExecute(null) ?? false;
    }

    private void UpdateSaveButtonState()
    {
        _saveButton.IsEnabled = _saveJournalCommand?.CanExecute(null) ?? false;
    }

    // --- Helper methods for updating Password Management View state ---

    private void UpdateDisplayPassword(string? text)
    {
        _displayPasswordTextBlock.Text = text ?? string.Empty;
    }

    private void UpdatePasswordVisibilityButton(bool isVisible)
    {
        _togglePasswordVisibilityButton.Content = isVisible ? "Hide" : "Show";
        // Potentially disable button if there's no password? (ViewModel logic should handle this)
        _togglePasswordVisibilityButton.IsEnabled = _togglePasswordVisibilityCommand?.CanExecute(null) ?? true; // Assume enabled unless command says otherwise
    }

    private void UpdateCurrentPasswordInput(string? text)
    {
        if (_isSyncingCurrentPassword) return;
        _isSyncingCurrentPassword = true;
        try
        {
            if (_currentPasswordTextBox.Text != text)
            {
                _currentPasswordTextBox.Text = text ?? string.Empty;
            }
        }
        finally { _isSyncingCurrentPassword = false; }
    }

    private void UpdateNewPasswordInput(string? text)
    {
        if (_isSyncingNewPassword) return;
        _isSyncingNewPassword = true;
        try
        {
            if (_newPasswordTextBox.Text != text)
            {
                _newPasswordTextBox.Text = text ?? string.Empty;
            }
        }
        finally { _isSyncingNewPassword = false; }
    }

    private void UpdateConfirmPasswordInput(string? text)
    {
        if (_isSyncingConfirmPassword) return;
        _isSyncingConfirmPassword = true;
        try
        {
            if (_confirmPasswordTextBox.Text != text)
            {
                _confirmPasswordTextBox.Text = text ?? string.Empty;
            }
        }
        finally { _isSyncingConfirmPassword = false; }
    }

    private void UpdateChangePasswordStatus(string? text)
    {
        _changePasswordStatusTextBlock.Text = text ?? string.Empty;
        // Optionally change color based on success/error
        _changePasswordStatusTextBlock.Foreground = text?.Contains("success", StringComparison.OrdinalIgnoreCase) ?? false
            ? Brushes.LightGreen
            : Brushes.OrangeRed;
    }

    private void UpdateChangePasswordButtonState()
    {
        _changePasswordButton.IsEnabled = _changePasswordCommand?.CanExecute(null) ?? false;
    }

    // --- End Helper methods for Password Management ---


    // Ensure cleanup on unload
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        _viewModel = null;
        _createNewJournalCommand = null;
        _saveJournalCommand = null;
        // --- Clear Password Command Refs ---
        _togglePasswordVisibilityCommand = null;
        _changePasswordCommand = null;
        // --- End Clear Password Command Refs ---
    }

    // DataTemplate methods remain necessary for ListBox item rendering
    private static IDataTemplate JournalDataTemplate()
    {
        return new FuncDataTemplate<object>((data, ns) =>
        {
            if (data is JournalGroupViewModel groupVm)
            {
                var itemsControl = new ItemsControl
                {
                    ItemsSource = groupVm.Journals,
                    ItemTemplate = JournalItemTemplate()
                };

                return new StackPanel
                {
                    Spacing = 4,
                    Margin = new Avalonia.Thickness(5, 8, 5, 4),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = groupVm.Year.ToString(),
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Margin = new Avalonia.Thickness(0, 0, 0, 5)
                        },
                        itemsControl
                    }
                };
            }
            return new TextBlock { Text = "Invalid item type in ListBox" };
        }, supportsRecycling: true);
    }

    private static IDataTemplate JournalItemTemplate()
    {
        return new FuncDataTemplate<JournalViewModel>((journalVm, ns) =>
            new TextBlock
            {
                Text = journalVm.DisplayName,
                Padding = new Avalonia.Thickness(15, 3, 5, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch
            },
            supportsRecycling: true);
    }
}
