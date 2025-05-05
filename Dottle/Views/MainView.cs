using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data; // Required for Binding
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dottle.ViewModels;

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
    private ICommand? _requestNewJournalDateCommand;
    private ICommand? _saveJournalCommand;
    private ICommand? _showExportDialogCommand; // Added field
    private ICommand? _showSettingsDialogCommand; // Added field

    private bool _isSyncingText = false;
    private ListBoxItem? _currentlySelectedListBoxItem = null;

    public MainView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        var exportJournalsMenuItem = new MenuItem { Header = "Export Journals..." };
        // Binding command replaces click handler
        // exportJournalsMenuItem.Click += ExportJournalsMenuItem_Click;
        exportJournalsMenuItem.CommandParameter = null; // Explicitly null if no parameter needed
        // Bind command in OnDataContextChangedHandler or via direct binding if VM is available early

        var settingsMenuItem = new MenuItem { Header = "Settings..." };
        // Bind command in OnDataContextChangedHandler
        settingsMenuItem.CommandParameter = null;

        var fileMenu = new MenuItem
        {
            Header = "_File",
            Items = { exportJournalsMenuItem, new Separator(), settingsMenuItem } // Added Settings
        };

        var changePasswordMenuItem = new MenuItem { Header = "Change Password..." };
        changePasswordMenuItem.Click += ChangePasswordMenuItem_Click;

        var securityMenu = new MenuItem
        {
            Header = "_Security",
            Items = { changePasswordMenuItem }
        };

        _menuBar = new Menu
        {
            Items = { fileMenu, securityMenu }
        };
        DockPanel.SetDock(_menuBar, Dock.Top);

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
            ItemTemplate = JournalDataTemplate(),
            SelectionMode = SelectionMode.Single
        };
        // ItemsSource binding happens in OnDataContextChangedHandler

        var journalListScrollViewer = new ScrollViewer { Content = _journalListBox };
        var leftDockPanel = new DockPanel { Children = { _newButton, journalListScrollViewer } };
        var leftBorder = new Border
        {
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = leftDockPanel
        };
        Grid.SetColumn(leftBorder, 0); Grid.SetRow(leftBorder, 0);

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
            IsReadOnly = false
        };
        _editorTextBox.TextChanged += EditorTextBox_TextChanged;

        var rightDockPanel = new DockPanel { Children = { _saveButton, _editorTextBox } };
        Grid.SetColumn(rightDockPanel, 1); Grid.SetRow(rightDockPanel, 0);

        _statusBarTextBlock = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Text binding happens in OnDataContextChangedHandler

        var bottomBorder = new Border
        {
            Background = SolidColorBrush.Parse("#2a2a2a"),
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(5, 3),
            Child = _statusBarTextBlock
        };
        Grid.SetColumn(bottomBorder, 0); Grid.SetRow(bottomBorder, 1); Grid.SetColumnSpan(bottomBorder, 2);

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

        // --- Bindings Setup ---
        // Bind status bar text
        this[!TextBlock.TextProperty] = new Binding(nameof(MainViewModel.StatusBarText))
        {
            Source = _statusBarTextBlock // Target the actual TextBlock
            // Path defaults to DataContext property if Source not set, but explicit better here
        };
        // Need to set DataContext early or re-apply bindings in OnDataContextChanged

    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.OwnerWindow = this.VisualRoot as Window;
        }
    }


    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.OwnerWindow = null;
            _viewModel.Cleanup(); // Call cleanup method on old VM
        }

        _viewModel = DataContext as MainViewModel;
        _requestNewJournalDateCommand = null;
        _saveJournalCommand = null;
        _showExportDialogCommand = null; // Reset command ref
        _showSettingsDialogCommand = null; // Reset command ref
        _currentlySelectedListBoxItem = null;


        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _requestNewJournalDateCommand = _viewModel.RequestNewJournalDateCommand;
            _saveJournalCommand = _viewModel.SaveJournalCommand;
            _showExportDialogCommand = _viewModel.ShowExportDialogCommand; // Get command ref
            _showSettingsDialogCommand = _viewModel.ShowSettingsDialogCommand; // Get command ref

            // Bind menu items to commands
            var fileMenu = _menuBar.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString()?.Contains("File") ?? false);
            if (fileMenu != null)
            {
                var exportItem = fileMenu.Items.OfType<MenuItem>().FirstOrDefault(i => i.Header?.ToString()?.Contains("Export") ?? false);
                if (exportItem != null) exportItem.Command = _showExportDialogCommand;

                var settingsItem = fileMenu.Items.OfType<MenuItem>().FirstOrDefault(i => i.Header?.ToString()?.Contains("Settings") ?? false);
                if (settingsItem != null) settingsItem.Command = _showSettingsDialogCommand;
            }

            // Set owner window ref on VM if attached
            if (this.IsAttachedToVisualTree())
            {
                _viewModel.OwnerWindow = this.VisualRoot as Window;
            }

            // Bind ItemsSource and Text properties explicitly
            _journalListBox.ItemsSource = _viewModel.JournalGroups;
            _statusBarTextBlock.Text = _viewModel.StatusBarText; // Initial set
            // Re-bind status bar text in case DataContext changes post-initialization
            _statusBarTextBlock[!TextBlock.TextProperty] = new Binding(nameof(MainViewModel.StatusBarText));


            // Update View state based on initial VM state
            // UpdateJournalList(_viewModel.JournalGroups); // ItemsSource binding handles this
            UpdateEditorText(_viewModel.CurrentJournalContent);
            UpdateVisualSelection(_viewModel.SelectedJournal);
            UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
            // UpdateStatusBar(_viewModel.StatusBarText); // Text binding handles this
            UpdateNewButtonState();
            UpdateSaveButtonState();
        }
        else
        {
            // Clear view state if ViewModel is null
            _journalListBox.ItemsSource = null;
            _statusBarTextBlock.Text = string.Empty;
            _statusBarTextBlock[!TextBlock.TextProperty] = (IBinding)BindingOperations.DoNothing; // Clear binding


            UpdateEditorText(null);
            UpdateVisualSelection(null);
            UpdateEditorState(false, false);
            // UpdateStatusBar(null); // Handled by clearing text
            UpdateNewButtonState();
            UpdateSaveButtonState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        // Dispatch UI updates to the UI thread if necessary, though Avalonia often handles this
        // for bindings. Manual updates might need Dispatcher.UIThread.InvokeAsync.

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.JournalGroups):
                // ItemsSource binding handles update, but selection needs refresh
                UpdateVisualSelection(_viewModel.SelectedJournal);
                break;
            case nameof(MainViewModel.SelectedJournal):
                UpdateVisualSelection(_viewModel.SelectedJournal);
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                UpdateSaveButtonState();
                UpdateEditorText(_viewModel.CurrentJournalContent); // Load content for new selection
                break;
            case nameof(MainViewModel.CurrentJournalContent):
                UpdateEditorText(_viewModel.CurrentJournalContent); // Update editor if changed externally
                UpdateSaveButtonState(); // Content change might affect CanSave
                break;
            case nameof(MainViewModel.IsLoadingContent):
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                break;
            case nameof(MainViewModel.IsSavingContent):
                UpdateSaveButtonState(); // Saving state affects CanSave
                break;
            case nameof(MainViewModel.StatusBarText):
                // Text binding handles this
                // UpdateStatusBar(_viewModel.StatusBarText);
                break;
            case nameof(MainViewModel.IsChangingPassword):
                // Might disable parts of UI, handle if needed
                break;
            case nameof(MainViewModel.ChangePasswordErrorMessage):
                // Display error if needed, maybe in status bar or dialog
                if (!string.IsNullOrEmpty(_viewModel.ChangePasswordErrorMessage))
                {
                    // Consider showing a message box or using status bar
                    // UpdateStatusBar($"Password Change Error: {_viewModel.ChangePasswordErrorMessage}");
                }
                break;
        }
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingText) return;

        // Update VM property manually (simulates TwoWay binding)
        _viewModel.CurrentJournalContent = _editorTextBox.Text;
    }

    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        // Execute command via stored reference
        _requestNewJournalDateCommand?.Execute(null);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        // Execute command via stored reference
        _saveJournalCommand?.Execute(null);
    }

    private async void ChangePasswordMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.OwnerWindow == null) return;

        // Create the dialog's ViewModel first.
        // The delegate passed here is a placeholder validation.
        // The *actual* validation and execution happens in MainViewModel's ChangePasswordCommand.
        var dialogViewModel = new ChangePasswordDialogViewModel(async (oldPass, newPass) =>
        {
            // This delegate isn't strictly needed anymore if ChangePasswordCommand handles everything.
            await Task.CompletedTask;
            // Basic local check (e.g., non-empty) could be done here, but primary logic is in MainViewModel.
            return !string.IsNullOrEmpty(oldPass) && !string.IsNullOrEmpty(newPass);
        });

        var dialog = new ChangePasswordDialog
        {
            DataContext = dialogViewModel
        };

        // Show the dialog and wait for it to close.
        // The result isn't used directly here; we check IsConfirmed and VM properties.
        await dialog.ShowDialog<object>(_viewModel.OwnerWindow); // Use object or bool?, doesn't matter much

        // After the dialog closes, check if the user confirmed *and* if the VM has the necessary data.
        if (dialog.IsConfirmed && _viewModel != null && dialogViewModel != null)
        {
            // Transfer the passwords entered in the dialog to the MainViewModel's properties.
            _viewModel.CurrentPasswordForChange = dialogViewModel.OldPassword;
            _viewModel.NewPassword = dialogViewModel.NewPassword;
            _viewModel.ConfirmNewPassword = dialogViewModel.ConfirmNewPassword;

            // Check if the MainViewModel's ChangePasswordCommand can execute with the new data.
            if (_viewModel.ChangePasswordCommand.CanExecute(null))
            {
                // Execute the command asynchronously.
                _ = _viewModel.ChangePasswordCommand.ExecuteAsync(null);
            }
            else
            {
                // Provide feedback if the command cannot execute (e.g., validation failed in CanExecute).
                UpdateStatusBar("Could not initiate password change (validation failed).");
                // Optionally, clear the password fields in MainViewModel if desired after failure.
                // _viewModel.CurrentPasswordForChange = null;
                // _viewModel.NewPassword = null;
                // _viewModel.ConfirmNewPassword = null;
            }
        }
        else
        {
            // User cancelled or dialog VM missing, update status if needed.
            UpdateStatusBar("Password change cancelled or aborted.");
        }
    }

    // Commented out as Command binding is used now
    // private void ExportJournalsMenuItem_Click(object? sender, RoutedEventArgs e)
    // {
    //     _showExportDialogCommand?.Execute(null);
    // }

    // private void SettingsMenuItem_Click(object? sender, RoutedEventArgs e)
    // {
    //     _showSettingsDialogCommand?.Execute(null);
    // }


    // No changes needed below this line for settings feature
    // ... (UpdateJournalList, UpdateVisualSelection, etc.)

    private void UpdateJournalList(IEnumerable<JournalGroupViewModel>? groups)
    {
        // Replaced by ItemsSource binding in OnDataContextChangedHandler
        // if (_currentlySelectedListBoxItem != null)
        // {
        //     _currentlySelectedListBoxItem.IsSelected = false;
        //     _currentlySelectedListBoxItem = null;
        // }
        // _journalListBox.ItemsSource = groups;
    }


    private void UpdateVisualSelection(JournalViewModel? newSelection)
    {
        if (_currentlySelectedListBoxItem != null)
        {
            _currentlySelectedListBoxItem.IsSelected = false;
            _currentlySelectedListBoxItem = null;
        }

        if (newSelection == null || _journalListBox.ItemsSource == null) return;

        foreach (var yearGroup in _journalListBox.ItemsSource.OfType<JournalGroupViewModel>())
        {
            var yearGroupContainer = _journalListBox.ContainerFromItem(yearGroup) as Control;
            if (yearGroupContainer == null)
            {
                _journalListBox.ScrollIntoView(yearGroup);
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return;
            }

            var monthItemsControl = yearGroupContainer.FindDescendantOfType<ItemsControl>();
            if (monthItemsControl == null) continue;

            var targetMonthGroup = yearGroup.MonthGroups.FirstOrDefault(mg => mg.Journals.Contains(newSelection));
            if (targetMonthGroup == null) continue;

            var monthGroupContainer = monthItemsControl.ContainerFromItem(targetMonthGroup) as Control;
            if (monthGroupContainer == null)
            {
                monthItemsControl.ScrollIntoView(targetMonthGroup);
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return;
            }

            var journalItemsControl = monthGroupContainer.FindDescendantOfType<ItemsControl>();
            if (journalItemsControl == null) continue;

            var journalItemContainer = journalItemsControl.ContainerFromItem(newSelection) as ListBoxItem;
            if (journalItemContainer != null)
            {
                journalItemContainer.IsSelected = true;
                _currentlySelectedListBoxItem = journalItemContainer;

                _journalListBox.ScrollIntoView(yearGroup);
                monthItemsControl.ScrollIntoView(targetMonthGroup);
                journalItemsControl.ScrollIntoView(newSelection);

                Dispatcher.UIThread.Post(() => journalItemContainer.BringIntoView(), DispatcherPriority.Loaded);
                return;
            }
            else
            {
                journalItemsControl.ScrollIntoView(newSelection);
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return;
            }
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
        bool editorShouldBeEnabled = isEnabled && !isLoading;
        if (_editorTextBox.IsEnabled != editorShouldBeEnabled)
        {
            _editorTextBox.IsEnabled = editorShouldBeEnabled;
        }
        // Make readonly if loading, but allow editing if just disabled (no selection)
        if (_editorTextBox.IsReadOnly != isLoading)
        {
            _editorTextBox.IsReadOnly = isLoading;
        }
    }

    private void UpdateStatusBar(string? text)
    {
        // Replaced by Text binding in OnDataContextChangedHandler
        // _statusBarTextBlock.Text = text ?? string.Empty;
    }

    private void UpdateNewButtonState()
    {
        // Check command CanExecute directly
        _newButton.IsEnabled = _requestNewJournalDateCommand?.CanExecute(null) ?? false;
    }

    private void UpdateSaveButtonState()
    {
        bool canSave = _saveJournalCommand?.CanExecute(null) ?? false;
        // Ensure editor is also enabled (implies a selection is loaded and not currently loading/saving)
        _saveButton.IsEnabled = canSave && _editorTextBox.IsEnabled;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Clean up ViewModel subscriptions and references
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.OwnerWindow = null; // Clear owner on detach
            _viewModel.Cleanup(); // Call VM cleanup
        }
        _viewModel = null;
        _requestNewJournalDateCommand = null;
        _saveJournalCommand = null;
        _showExportDialogCommand = null;
        _showSettingsDialogCommand = null;
        _currentlySelectedListBoxItem = null; // Clear selection ref
    }

    private static IDataTemplate JournalDataTemplate()
    {
        return new FuncDataTemplate<JournalGroupViewModel>((yearVm, ns) =>
        {
            var monthItemsControl = new ItemsControl
            {
                ItemsSource = yearVm.MonthGroups,
                ItemTemplate = MonthGroupTemplate(),
                Focusable = false
            };

            return new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(5, 8, 5, 4),
                Children =
                {
                    new TextBlock
                    {
                        [!TextBlock.TextProperty] = new Binding(nameof(yearVm.DisplayName)),
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 5),
                        IsEnabled = false,
                        Cursor = Cursor.Default
                    },
                    monthItemsControl
                }
            };
        }, supportsRecycling: true);
    }

    private static IDataTemplate MonthGroupTemplate()
    {
        return new FuncDataTemplate<JournalMonthGroupViewModel>((monthVm, ns) =>
        {
            var journalItemsControl = new ItemsControl
            {
                ItemsSource = monthVm.Journals,
                ItemTemplate = JournalItemTemplate(),
                Focusable = false
            };

            return new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(15, 4, 5, 4),
                Children =
                {
                    new TextBlock
                    {
                         [!TextBlock.TextProperty] = new Binding(nameof(monthVm.DisplayName)),
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Thickness(0, 0, 0, 3),
                        IsEnabled = false,
                        Cursor = Cursor.Default
                    },
                    journalItemsControl
                }
            };
        }, supportsRecycling: true);
    }


    private static IDataTemplate JournalItemTemplate()
    {
        var textBlockTemplate = new FuncDataTemplate<JournalViewModel>((vm, ns) =>
            new TextBlock
            {
                [!TextBlock.TextProperty] = new Binding(nameof(vm.DisplayName)),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            }, supportsRecycling: true);

        return new FuncDataTemplate<JournalViewModel>((journalVm, ns) =>
        {
            var item = new ListBoxItem
            {
                DataContext = journalVm,
                Content = journalVm,
                ContentTemplate = textBlockTemplate,
                Padding = new Thickness(30, 3, 5, 3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            item.PointerPressed += JournalItem_PointerPressed;
            return item;
        },
        supportsRecycling: true);
    }

    private static void JournalItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle primary button clicks to avoid context menu interference etc.
        if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed &&
            sender is ListBoxItem { DataContext: JournalViewModel journalVm } item)
        {
            var mainView = item.FindAncestorOfType<MainView>();
            if (mainView?._viewModel != null)
            {
                // Check reference equality before assigning to avoid unnecessary updates
                if (!ReferenceEquals(mainView._viewModel.SelectedJournal, journalVm))
                {
                    mainView._viewModel.SelectedJournal = journalVm;
                }
                e.Handled = true; // Indicate the event was handled
            }
        }
    }
}