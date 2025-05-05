using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
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
    // Renamed command reference field
    private ICommand? _requestNewJournalDateCommand;
    private ICommand? _saveJournalCommand;

    private bool _isSyncingText = false;
    private ListBoxItem? _currentlySelectedListBoxItem = null;

    public MainView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        var exportJournalsMenuItem = new MenuItem { Header = "Export Journals..." };
        exportJournalsMenuItem.Click += ExportJournalsMenuItem_Click; // Placeholder handler

        var fileMenu = new MenuItem
        {
            Header = "_File",
            Items = { exportJournalsMenuItem }
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
            Items = { fileMenu, securityMenu } // Add fileMenu here
        };
        DockPanel.SetDock(_menuBar, Dock.Top);

        _newButton = new Button
        {
            Content = "New Journal",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5),
            IsEnabled = false // Initial state, enabled by VM command CanExecute
        };
        DockPanel.SetDock(_newButton, Dock.Top);
        _newButton.Click += NewButton_Click; // Click handler executes the VM command

        _journalListBox = new ListBox
        {
            Background = Brushes.Transparent,
            ItemTemplate = JournalDataTemplate(),
            SelectionMode = SelectionMode.Single
        };

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
    }

    // Get owner window reference when attached
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_viewModel != null)
        {
            // Pass owner window ref to VM for dialogs
            _viewModel.OwnerWindow = this.VisualRoot as Window;
        }
    }


    private void OnDataContextChangedHandler(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.OwnerWindow = null; // Clear owner reference
        }

        _viewModel = DataContext as MainViewModel;
        // Reset command references
        _requestNewJournalDateCommand = null;
        _saveJournalCommand = null;
        _currentlySelectedListBoxItem = null;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            // Fetch correct command reference
            _requestNewJournalDateCommand = _viewModel.RequestNewJournalDateCommand;
            _saveJournalCommand = _viewModel.SaveJournalCommand;

            // Set owner window ref on VM if we are already attached
            if (this.IsAttachedToVisualTree())
            {
                _viewModel.OwnerWindow = this.VisualRoot as Window;
            }

            UpdateJournalList(_viewModel.JournalGroups);
            UpdateEditorText(_viewModel.CurrentJournalContent);
            UpdateVisualSelection(_viewModel.SelectedJournal);
            UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
            UpdateStatusBar(_viewModel.StatusBarText);
            UpdateNewButtonState(); // Update state based on new command
            UpdateSaveButtonState();
        }
        else
        {
            UpdateJournalList(null);
            UpdateEditorText(null);
            UpdateVisualSelection(null);
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
                UpdateVisualSelection(_viewModel.SelectedJournal);
                break;
            case nameof(MainViewModel.SelectedJournal):
                UpdateVisualSelection(_viewModel.SelectedJournal);
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
            case nameof(MainViewModel.IsChangingPassword):
                break;
            case nameof(MainViewModel.ChangePasswordErrorMessage):
                if (!string.IsNullOrEmpty(_viewModel.ChangePasswordErrorMessage))
                {
                }
                break;
        }
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingText) return;

        _viewModel.CurrentJournalContent = _editorTextBox.Text;
    }

    // Executes the VM command to show the date dialog
    private void NewButton_Click(object? sender, RoutedEventArgs e)
    {
        _requestNewJournalDateCommand?.Execute(null);
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        _saveJournalCommand?.Execute(null);
    }

    private async void ChangePasswordMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null || _viewModel.OwnerWindow == null) return;

        var dialogViewModel = new ChangePasswordDialogViewModel(async (oldPass, newPass) =>
        {
            await Task.CompletedTask;
            return true;
        });

        var dialog = new ChangePasswordDialog
        {
            DataContext = dialogViewModel
        };

        var dialogResult = await dialog.ShowDialog<bool>(_viewModel.OwnerWindow);

        if (dialogResult && dialog.IsConfirmed && _viewModel != null && dialogViewModel != null)
        {
            _viewModel.CurrentPasswordForChange = dialogViewModel.OldPassword;
            _viewModel.NewPassword = dialogViewModel.NewPassword;
            _viewModel.ConfirmNewPassword = dialogViewModel.ConfirmNewPassword;

            if (_viewModel.ChangePasswordCommand.CanExecute(null))
            {
                _ = _viewModel.ChangePasswordCommand.ExecuteAsync(null);
            }
            else
            {
                UpdateStatusBar("Could not initiate password change.");
            }
        }
    }

    // Executes the VM command to show the export dialog
    private void ExportJournalsMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.ShowExportDialogCommand.CanExecute(null) ?? false)
        {
            _ = _viewModel.ShowExportDialogCommand.ExecuteAsync(null); // Execute async command
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Cannot execute ShowExportDialogCommand.");
            // Optionally update status bar or show a message
            // _viewModel?.UpdateStatusBar("Cannot open export dialog right now.");
        }
    }

    private void UpdateJournalList(IEnumerable<JournalGroupViewModel>? groups)
    {
        if (_currentlySelectedListBoxItem != null)
        {
            _currentlySelectedListBoxItem.IsSelected = false;
            _currentlySelectedListBoxItem = null;
        }
        _journalListBox.ItemsSource = groups; // Still binding the top-level groups
    }

    // Updated to handle nested Month Groups
    private void UpdateVisualSelection(JournalViewModel? newSelection)
    {
        // Clear previous selection highlight if any
        if (_currentlySelectedListBoxItem != null)
        {
            _currentlySelectedListBoxItem.IsSelected = false;
            _currentlySelectedListBoxItem = null;
        }

        if (newSelection == null || _journalListBox.ItemsSource == null) return;

        // Find the target ListBoxItem through the nested structure
        foreach (var yearGroup in _journalListBox.ItemsSource.OfType<JournalGroupViewModel>())
        {
            var yearGroupContainer = _journalListBox.ContainerFromItem(yearGroup) as Control;
            if (yearGroupContainer == null)
            {
                // If container not realized, try scrolling the year group into view first
                _journalListBox.ScrollIntoView(yearGroup);
                // We might need to defer the rest of the search until UI updates
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return; // Exit for now, will retry after scroll
            }

            // Find the ItemsControl for MonthGroups within the YearGroup container
            var monthItemsControl = yearGroupContainer.FindDescendantOfType<ItemsControl>();
            if (monthItemsControl == null) continue; // Should not happen with the template

            // Find the specific MonthGroupViewModel that contains the newSelection
            var targetMonthGroup = yearGroup.MonthGroups.FirstOrDefault(mg => mg.Journals.Contains(newSelection));
            if (targetMonthGroup == null) continue; // Journal not in this year group

            var monthGroupContainer = monthItemsControl.ContainerFromItem(targetMonthGroup) as Control;
            if (monthGroupContainer == null)
            {
                // If month container not realized, scroll month group into view
                monthItemsControl.ScrollIntoView(targetMonthGroup);
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return; // Exit for now, will retry after scroll
            }

            // Find the ItemsControl for Journals within the MonthGroup container
            var journalItemsControl = monthGroupContainer.FindDescendantOfType<ItemsControl>();
            if (journalItemsControl == null) continue; // Should not happen

            // Find the final ListBoxItem for the JournalViewModel
            var journalItemContainer = journalItemsControl.ContainerFromItem(newSelection) as ListBoxItem;
            if (journalItemContainer != null)
            {
                // Found it! Select and scroll.
                journalItemContainer.IsSelected = true;
                _currentlySelectedListBoxItem = journalItemContainer; // Track the selected item

                // Scroll the main listbox to the year, then the inner controls
                _journalListBox.ScrollIntoView(yearGroup);
                monthItemsControl.ScrollIntoView(targetMonthGroup);
                journalItemsControl.ScrollIntoView(newSelection);

                // Ensure the final item is fully visible
                Dispatcher.UIThread.Post(() => journalItemContainer.BringIntoView(), DispatcherPriority.Loaded);
                return; // Found and selected, exit the loop
            }
            else
            {
                // If journal item container not realized yet, scroll it into view
                journalItemsControl.ScrollIntoView(newSelection);
                Dispatcher.UIThread.Post(() => UpdateVisualSelection(newSelection), DispatcherPriority.Background);
                return; // Exit for now, will retry after scroll
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
        _editorTextBox.IsEnabled = isEnabled && !isLoading;
        _editorTextBox.IsReadOnly = isLoading;
    }

    private void UpdateStatusBar(string? text)
    {
        _statusBarTextBlock.Text = text ?? string.Empty;
    }

    // Update button state based on the correct command
    private void UpdateNewButtonState()
    {
        _newButton.IsEnabled = _requestNewJournalDateCommand?.CanExecute(null) ?? false;
    }

    private void UpdateSaveButtonState()
    {
        bool canSave = _saveJournalCommand?.CanExecute(null) ?? false;
        _saveButton.IsEnabled = canSave && _editorTextBox.IsEnabled;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.OwnerWindow = null; // Clear owner on detach
        }
        _viewModel = null;
        _requestNewJournalDateCommand = null;
        _saveJournalCommand = null;
        _currentlySelectedListBoxItem = null;
    }

    private static IDataTemplate JournalDataTemplate()
    {
        // Template for the top-level item (Year Group)
        return new FuncDataTemplate<JournalGroupViewModel>((yearVm, ns) =>
        {
            // ItemsControl for the months within this year
            var monthItemsControl = new ItemsControl
            {
                ItemsSource = yearVm.MonthGroups, // Bind to the collection of Month Groups
                ItemTemplate = MonthGroupTemplate(), // Use the new template for months
                Focusable = false // Don't let this steal focus
            };

            return new StackPanel // Container for the year header and its months
            {
                Spacing = 4,
                Margin = new Thickness(5, 8, 5, 4), // Outer margin for the year group
                Children =
                {
                    // Year Header Text
                    new TextBlock
                    {
                        Text = yearVm.DisplayName, // Use DisplayName from JournalGroupViewModel
                        FontSize = 16,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 5), // Margin below year header
                        IsEnabled = false, // Not interactive
                        Cursor = Cursor.Default
                    },
                    // The list of months for this year
                    monthItemsControl
                }
            };
        }, supportsRecycling: true); // Enable recycling for performance
    }

    // New Template for the Month Group level
    private static IDataTemplate MonthGroupTemplate()
    {
        return new FuncDataTemplate<JournalMonthGroupViewModel>((monthVm, ns) =>
        {
            // ItemsControl for the journals within this month
            var journalItemsControl = new ItemsControl
            {
                ItemsSource = monthVm.Journals, // Bind to the collection of Journals
                ItemTemplate = JournalItemTemplate(), // Use the existing template for journal entries
                Focusable = false // Don't let this steal focus
            };

            return new StackPanel // Container for the month header and its journals
            {
                Spacing = 2,
                Margin = new Thickness(15, 4, 5, 4), // Indent month groups slightly
                Children =
                {
                    // Month Header Text
                    new TextBlock
                    {
                        Text = monthVm.DisplayName, // Use DisplayName from JournalMonthGroupViewModel
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Margin = new Thickness(0, 0, 0, 3), // Margin below month header
                        IsEnabled = false, // Not interactive
                        Cursor = Cursor.Default
                    },
                    // The list of journals for this month
                    journalItemsControl
                }
            };
        }, supportsRecycling: true); // Enable recycling
    }


    // Template for the final Journal Entry item (leaf node)
    private static IDataTemplate JournalItemTemplate()
    {
        // Defines how the JournalViewModel itself is displayed within the ListBoxItem
        var textBlockTemplate = new FuncDataTemplate<JournalViewModel>((vm, ns) =>
            new TextBlock
            {
                [!TextBlock.TextProperty] = new Avalonia.Data.Binding(nameof(vm.DisplayName)), // Bind to DisplayName
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            }, supportsRecycling: true);

        // Creates the actual ListBoxItem container for the JournalViewModel
        return new FuncDataTemplate<JournalViewModel>((journalVm, ns) =>
        {
            var item = new ListBoxItem
            {
                DataContext = journalVm, // Set DataContext for binding and event handlers
                Content = journalVm, // Content to be displayed using ContentTemplate
                ContentTemplate = textBlockTemplate, // Use the TextBlock template above
                Padding = new Thickness(30, 3, 5, 3), // Indent journal items further under the month
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand) // Indicate it's clickable
            };
            // Attach event handler to detect clicks
            item.PointerPressed += JournalItem_PointerPressed;
            return item;
        },
        supportsRecycling: true); // Enable recycling
    }

    private static void JournalItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: JournalViewModel journalVm } item)
        {
            var mainView = item.FindAncestorOfType<MainView>();
            if (mainView?._viewModel != null)
            {
                if (!ReferenceEquals(mainView._viewModel.SelectedJournal, journalVm))
                {
                    mainView._viewModel.SelectedJournal = journalVm;
                }
                e.Handled = true;
            }
        }
    }
}
