using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dottle.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
    // Renamed command reference field
    private ICommand? _requestNewJournalDateCommand;
    private ICommand? _saveJournalCommand;

    private bool _isSyncingText = false;
    private ListBoxItem? _currentlySelectedListBoxItem = null;

    public MainView()
    {
        this.DataContextChanged += OnDataContextChangedHandler;

        var changePasswordMenuItem = new MenuItem { Header = "Change Password..." };
        changePasswordMenuItem.Click += ChangePasswordMenuItem_Click;

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

    private void UpdateJournalList(IEnumerable<JournalGroupViewModel>? groups)
    {
        if (_currentlySelectedListBoxItem != null)
        {
            _currentlySelectedListBoxItem.IsSelected = false;
            _currentlySelectedListBoxItem = null;
        }
        _journalListBox.ItemsSource = groups;
    }

    private void UpdateVisualSelection(JournalViewModel? newSelection)
    {
        if (_currentlySelectedListBoxItem != null)
        {
            _currentlySelectedListBoxItem.IsSelected = false;
            _currentlySelectedListBoxItem = null;
        }

        if (newSelection == null || _journalListBox.ItemsSource == null) return;

        foreach (var group in _journalListBox.ItemsSource.OfType<JournalGroupViewModel>())
        {
            var groupContainer = _journalListBox.ContainerFromItem(group) as Control;
            if (groupContainer == null)
            {
                _journalListBox.ScrollIntoView(group);
                continue;
            }

            var nestedItemsControl = groupContainer.FindDescendantOfType<ItemsControl>();
            if (nestedItemsControl == null) continue;

            var newItemContainer = nestedItemsControl.ContainerFromItem(newSelection) as ListBoxItem;

            if (newItemContainer != null)
            {
                newItemContainer.IsSelected = true;
                _currentlySelectedListBoxItem = newItemContainer;
                _journalListBox.ScrollIntoView(group);
                Dispatcher.UIThread.Post(() => newItemContainer.BringIntoView(), DispatcherPriority.Background);
                break;
            }
            else
            {
                nestedItemsControl.ScrollIntoView(newSelection);
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
        return new FuncDataTemplate<object>((data, ns) =>
        {
            if (data is JournalGroupViewModel groupVm)
            {
                var itemsControl = new ItemsControl
                {
                    ItemsSource = groupVm.Journals,
                    ItemTemplate = JournalItemTemplate(),
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
                            Text = groupVm.Year.ToString(),
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Margin = new Thickness(0, 0, 0, 5),
                            IsEnabled = false,
                            Cursor = Cursor.Default
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
        var textBlockTemplate = new FuncDataTemplate<JournalViewModel>((vm, ns) =>
            new TextBlock
            {
                Text = vm.DisplayName,
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
                Padding = new Thickness(15, 3, 5, 3),
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