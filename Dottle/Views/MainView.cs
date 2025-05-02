using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly TextBox _newJournalDateTextBox;
    private readonly Menu _menuBar;

    private MainViewModel? _viewModel;
    private ICommand? _createNewJournalCommand;
    private ICommand? _saveJournalCommand;
    private bool _isSyncingSelection = false;
    private bool _isSyncingText = false;
    private bool _isSyncingDateText = false;

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

        _newJournalDateTextBox = new TextBox
        {
            Watermark = "YYYY-MM-DD",
            Margin = new Thickness(5, 5, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        DockPanel.SetDock(_newJournalDateTextBox, Dock.Top);
        _newJournalDateTextBox.TextChanged += NewJournalDateTextBox_TextChanged;

        _newButton = new Button
        {
            Content = "New Journal",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5, 5, 5, 5),
            IsEnabled = false
        };
        DockPanel.SetDock(_newButton, Dock.Top);
        _newButton.Click += NewButton_Click;

        _journalListBox = new ListBox
        {
            Background = Brushes.Transparent,
            ItemTemplate = CreateFlattenedJournalTemplate()
        };
        _journalListBox.SelectionChanged += JournalListBox_SelectionChanged;

        var journalListScrollViewer = new ScrollViewer { Content = _journalListBox };

        var leftDockPanel = new DockPanel
        {
            Children =
            {
                _newJournalDateTextBox,
                _newButton,
                journalListScrollViewer
            }
        };

        var leftBorder = new Border
        {
            BorderBrush = SolidColorBrush.Parse("#444"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = leftDockPanel,
            MinWidth = 150
        };
        Grid.SetColumn(leftBorder, 0);
        Grid.SetRow(leftBorder, 0);

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
        Grid.SetColumn(rightDockPanel, 1);
        Grid.SetRow(rightDockPanel, 0);

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
        Grid.SetColumn(bottomBorder, 0);
        Grid.SetRow(bottomBorder, 1);
        Grid.SetColumnSpan(bottomBorder, 2);

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

            UpdateJournalList(_viewModel.DisplayItems);
            UpdateEditorText(_viewModel.CurrentJournalContent);
            UpdateSelectedItem(_viewModel.SelectedJournal);
            UpdateNewJournalDateText(_viewModel.NewJournalDateString);
            UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
            UpdateStatusBar(_viewModel.StatusBarText);
            UpdateNewButtonState();
            UpdateSaveButtonState();
        }
        else
        {
            UpdateJournalList(null);
            UpdateEditorText(null);
            UpdateNewJournalDateText(null);
            UpdateEditorState(false, false);
            UpdateStatusBar(null);
            UpdateNewButtonState();
            UpdateSaveButtonState();
            _journalListBox.SelectedItem = null;
            _editorTextBox.Text = null;
            _newJournalDateTextBox.Text = null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.DisplayItems):
                UpdateJournalList(_viewModel.DisplayItems);
                break;
            case nameof(MainViewModel.SelectedJournal):
                UpdateSelectedItem(_viewModel.SelectedJournal);
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                break;
            case nameof(MainViewModel.CurrentJournalContent):
                UpdateEditorText(_viewModel.CurrentJournalContent);
                UpdateSaveButtonState();
                break;
            case nameof(MainViewModel.NewJournalDateString):
                UpdateNewJournalDateText(_viewModel.NewJournalDateString);
                break;
            case nameof(MainViewModel.IsLoadingContent):
                UpdateEditorState(_viewModel.IsJournalSelected, _viewModel.IsLoadingContent);
                UpdateSaveButtonState();
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
                break;
        }
    }

    private void UpdateJournalList(IEnumerable<object>? items)
    {
        var currentSelection = _journalListBox.SelectedItem;
        _journalListBox.ItemsSource = items;

        if (currentSelection != null && items?.Contains(currentSelection) == true)
        {
            _isSyncingSelection = true;
            _journalListBox.SelectedItem = currentSelection;
            _isSyncingSelection = false;
        }
        else if (_viewModel?.SelectedJournal != null && items != null)
        {
            UpdateSelectedItem(_viewModel.SelectedJournal);
        }
        else
        {
            _isSyncingSelection = true;
            _journalListBox.SelectedItem = null;
            _isSyncingSelection = false;
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
                var itemToSelect = _viewModel?.DisplayItems?.FirstOrDefault(item => item == selectedJournalVm);

                _journalListBox.SelectedItem = itemToSelect;
                if (itemToSelect != null)
                {
                    _journalListBox.ScrollIntoView(itemToSelect);
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

    private void UpdateNewJournalDateText(string? text)
    {
        if (_isSyncingDateText) return;

        _isSyncingDateText = true;
        try
        {
            var currentText = _newJournalDateTextBox.Text ?? string.Empty;
            var newText = text ?? string.Empty;
            if (currentText != newText)
            {
                _newJournalDateTextBox.Text = newText;
            }
        }
        finally
        {
            _isSyncingDateText = false;
        }
    }

    private void UpdateEditorState(bool isEnabled, bool isLoading)
    {
        bool shouldBeEnabled = isEnabled && !isLoading;
        if (_editorTextBox.IsEnabled != shouldBeEnabled)
        {
            _editorTextBox.IsEnabled = shouldBeEnabled;
        }
        if (_editorTextBox.IsReadOnly != isLoading)
        {
            _editorTextBox.IsReadOnly = isLoading;
        }
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
        bool canSaveCommand = _saveJournalCommand?.CanExecute(null) ?? false;
        _saveButton.IsEnabled = canSaveCommand;
    }

    private static IDataTemplate CreateFlattenedJournalTemplate()
    {
        return new FuncDataTemplate<object>(
            (data, ns) =>
            {
                if (data is int year)
                {
                    return CreateYearHeaderControl(year);
                }
                if (data is JournalViewModel journalVm)
                {
                    return CreateJournalItemControl(journalVm);
                }
                return new TextBlock { Text = $"Unknown data type: {data?.GetType().Name ?? "null"}" };
            },
            supportsRecycling: true,
            match: data => data is JournalViewModel || data is int
        );
    }

    private static Control CreateYearHeaderControl(int year)
    {
        return new TextBlock
        {
            Text = year.ToString(),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(5, 8, 5, 5),
            Padding = new Thickness(0, 3, 0, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = false,
            IsHitTestVisible = false
        };
    }

    private static Control CreateJournalItemControl(JournalViewModel journalVm)
    {
        return new TextBlock
        {
            Text = journalVm.DisplayName,
            Padding = new Thickness(15, 3, 5, 3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Focusable = true
        };
    }

    private void JournalListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingSelection)
        {
            return;
        }

        if (_journalListBox.SelectedItem is JournalViewModel selectedJournalVm)
        {
            _viewModel.SelectedJournal = selectedJournalVm;
        }
        else if (_journalListBox.SelectedItem == null)
        {
            _viewModel.SelectedJournal = null;
        }
    }

    private void EditorTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingText)
        {
            return;
        }
        _viewModel.CurrentJournalContent = _editorTextBox.Text;
    }

    private void NewJournalDateTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_viewModel == null || _isSyncingDateText)
        {
            return;
        }
        _viewModel.NewJournalDateString = _newJournalDateTextBox.Text;
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
        if (_viewModel == null)
        {
            return;
        }

        var dialogViewModel = new ChangePasswordDialogViewModel(async (oldPass, newPass) =>
        {
            await Task.CompletedTask;
            return true;
        });

        var dialog = new ChangePasswordDialog
        {
            DataContext = dialogViewModel
        };

        if (this.GetVisualRoot() is not Window owner)
        {
            return;
        }

        var dialogResult = await dialog.ShowDialog<bool>(owner);

        if (dialogResult && dialog.IsConfirmed && dialogViewModel != null)
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
                UpdateStatusBar("Could not initiate password change. Pre-conditions not met.");
            }
        }
    }
}