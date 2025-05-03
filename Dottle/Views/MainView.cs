using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Dottle.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks; // Added for async dialog showing
using System.Windows.Input;

namespace Dottle.Views;

public sealed class MainView : UserControl
{
    private readonly ListBox _journalListBox;
    private readonly TextBox _editorTextBox;
    private readonly TextBlock _statusBarTextBlock;
    private readonly Button _saveButton;
    private readonly Button _newButton;
    private readonly Menu _menuBar;

    private MainViewModel? _viewModel;
    private ICommand? _createNewJournalCommand;
    private ICommand? _saveJournalCommand;
    // No need to store ChangePassword command here, it's triggered indirectly

    private bool _isSyncingSelection = false;
    private bool _isSyncingText = false;

    public MainView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Menu Bar ---
        var changePasswordMenuItem = new MenuItem { Header = "Change Password..." };
        changePasswordMenuItem.Click += ChangePasswordMenuItem_Click; // Assign click handler

        var securityMenu = new MenuItem
        {
            Header = "_Security",
            Items = { changePasswordMenuItem }
        };

        _menuBar = new Menu
        {
            Items = { securityMenu }
        };
        DockPanel.SetDock(_menuBar, Dock.Top);

        // --- Left Panel Components ---
        _newButton = new Button
        {
            Content = "New Journal",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5),
            IsEnabled = false
        };
        DockPanel.SetDock(_newButton, Dock.Top);
        _newButton.Click += NewButton_Click;

        _journalListBox = new ListBox
        {
            Background = Brushes.Transparent,
            ItemTemplate = JournalDataTemplate()
        };
        _journalListBox.SelectionChanged += JournalListBox_SelectionChanged;

        var journalListScrollViewer = new ScrollViewer { Content = _journalListBox };
        var leftDockPanel = new DockPanel { Children = { _newButton, journalListScrollViewer } };
        var leftBorder = new Border
        {
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = leftDockPanel
        };
        Grid.SetColumn(leftBorder, 0); Grid.SetRow(leftBorder, 0);

        // --- Right Panel Components ---
        _saveButton = new Button
        {
            Content = "Save Journal",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(5),
            IsEnabled = false
        };
        _saveButton.Classes.Add("accent");
        DockPanel.SetDock(_saveButton, Dock.Top);
        _saveButton.Click += SaveButton_Click;

        _editorTextBox = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            FontSize = 13,
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false,
            IsReadOnly = false // Start as editable, disable based on state
        };
        _editorTextBox.TextChanged += EditorTextBox_TextChanged;

        var rightDockPanel = new DockPanel { Children = { _saveButton, _editorTextBox } };
        Grid.SetColumn(rightDockPanel, 1); Grid.SetRow(rightDockPanel, 0);

        // --- Status Bar Components ---
        _statusBarTextBlock = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        var bottomBorder = new Border
        {
            Background = SolidColorBrush.Parse("#2a2a2a"),
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(5, 3),
            Child = _statusBarTextBlock
        };
        Grid.SetColumn(bottomBorder, 0); Grid.SetRow(bottomBorder, 1); Grid.SetColumnSpan(bottomBorder, 2);

        // --- Top Level Container ---
        var mainDockPanel = new DockPanel();
        mainDockPanel.Children.Add(_menuBar);

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children = { leftBorder, rightDockPanel, bottomBorder }
        };
        mainDockPanel.Children.Add(contentGrid);

        Content = mainDockPanel;
    }

    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        _createNewJournalCommand = null;
        _saveJournalCommand = null;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _createNewJournalCommand = _viewModel.CreateNewJournalCommand;
            _saveJournalCommand = _viewModel.SaveJournalCommand;

            UpdateJournalList(_viewModel.JournalGroups);
            UpdateEditorText(_viewModel.CurrentJournalContent);
            UpdateSelectedItem(_viewModel.SelectedJournal);
            UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
            UpdateStatusBar(_viewModel.StatusBarText);
            UpdateNewButtonState();
            UpdateSaveButtonState();
        }
        else
        {
            UpdateJournalList(null);
            UpdateEditorText(null);
            UpdateEditorState(false, false);
            UpdateStatusBar(null);
            UpdateNewButtonState();
            UpdateSaveButtonState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.JournalGroups):
                UpdateJournalList(_viewModel.JournalGroups);
                break;
            case nameof(MainViewModel.SelectedJournal):
                UpdateSelectedItem(_viewModel.SelectedJournal);
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                UpdateSaveButtonState();
                UpdateEditorText(_viewModel.CurrentJournalContent);
                break;
            case nameof(MainViewModel.CurrentJournalContent):
                UpdateEditorText(_viewModel.CurrentJournalContent);
                UpdateSaveButtonState();
                break;
            case nameof(MainViewModel.IsLoadingContent):
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                break;
            case nameof(MainViewModel.IsSavingContent):
                UpdateSaveButtonState();
                break;
            case nameof(MainViewModel.StatusBarText):
                UpdateStatusBar(_viewModel.StatusBarText);
                break;
            case nameof(MainViewModel.IsChangingPassword): // Update relevant UI if needed
                // Maybe disable the 'Change Password' menu item while processing?
                break;
            case nameof(MainViewModel.ChangePasswordErrorMessage): // Display errors from main VM
                if (!string.IsNullOrEmpty(_viewModel.ChangePasswordErrorMessage))
                {
                    // Optionally show this in a more prominent way, e.g., temporary status bar message
                    // Or rely on the status bar text already being updated by the command
                }
                break;
        }
    }

    private void JournalListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingSelection) return;

        _viewModel.SelectedJournal = _journalListBox.SelectedItem as JournalViewModel;
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingText) return;

        _viewModel.CurrentJournalContent = _editorTextBox.Text;
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        _createNewJournalCommand?.Execute(null);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        _saveJournalCommand?.Execute(null);
    }

    private async void ChangePasswordMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var dialogViewModel = new ChangePasswordDialogViewModel(async (oldPass, newPass) =>
        {
            // This delegate doesn't run the command directly,
            // it's just here to satisfy the constructor.
            // The real work happens after the dialog closes.
            await Task.CompletedTask;
            return true; // Placeholder
        });

        var dialog = new ChangePasswordDialog
        {
            DataContext = dialogViewModel
        };

        if (this.VisualRoot is not Window owner) return;

        var dialogResult = await dialog.ShowDialog<bool>(owner);

        if (dialogResult && dialog.IsConfirmed && _viewModel != null && dialogViewModel != null) // Check IsConfirmed and VM null states
        {
            // Transfer confirmed passwords to MainViewModel properties
            _viewModel.CurrentPasswordForChange = dialogViewModel.OldPassword;
            _viewModel.NewPassword = dialogViewModel.NewPassword;
            _viewModel.ConfirmNewPassword = dialogViewModel.ConfirmNewPassword; // Needed for CanExecute

            // Now execute the command on MainViewModel
            if (_viewModel.ChangePasswordCommand.CanExecute(null))
            {
                // The command is async, but we don't necessarily need to await it here
                // as it updates the UI (status bar, IsChangingPassword) via bindings.
                _ = _viewModel.ChangePasswordCommand.ExecuteAsync(null);
            }
            else
            {
                // This case shouldn't normally happen if dialog validation is correct,
                // but handle defensively. Maybe update status bar?
                UpdateStatusBar("Could not initiate password change.");
            }
        }
    }


    private void UpdateJournalList(IEnumerable<JournalGroupViewModel>? groups)
    {
        _journalListBox.ItemsSource = groups;
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
            var currentText = _editorTextBox.Text ?? string.Empty;
            var newText = text ?? string.Empty;
            if (currentText != newText)
            {
                _editorTextBox.Text = newText;
            }
        }
        finally
        {
            _isSyncingText = false;
        }
    }

    private void UpdateEditorState(bool isEnabled, bool isLoading)
    {
        _editorTextBox.IsEnabled = isEnabled && !isLoading;
        _editorTextBox.IsReadOnly = isLoading; // Make read-only while loading
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
        bool canSave = _saveJournalCommand?.CanExecute(null) ?? false;
        _saveButton.IsEnabled = canSave && _editorTextBox.IsEnabled; // Also consider if editor is enabled
    }

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
    }

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
                    Margin = new Thickness(5, 8, 5, 4),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = groupVm.Year.ToString(),
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Margin = new Thickness(0, 0, 0, 5)
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
                Padding = new Thickness(15, 3, 5, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch
            },
            supportsRecycling: true);
    }
}