using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Dottle.ViewModels;
using System;
using System.ComponentModel;
using System.Windows.Input; // Required for ICommand
using Avalonia.Interactivity; // Required for RoutedEventArgs

namespace Dottle.Views;

public sealed class SettingsDialog : Window
{
    private readonly TextBlock _currentPathTextBlockValue;
    private readonly TextBlock _newPathTextBlockValue;
    private readonly Button _selectFolderButton;
    private readonly Button _applyButton;
    private readonly Button _closeButton;
    private readonly TextBlock _statusTextBlock;
    private readonly ProgressBar _progressBar;

    private SettingsViewModel? _viewModel;
    private ICommand? _selectFolderCommandInstance;
    private ICommand? _applyCommandInstance;

    public SettingsDialog()
    {
        Title = "Settings";
        Width = 550;
        Height = 350;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.Full;

        this.DataContextChanged += OnDataContextChangedHandler;

        // --- Controls ---
        var currentPathLabel = new TextBlock { Text = "Current Journal Folder:", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 5, 0) };
        _currentPathTextBlockValue = new TextBlock { TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };

        var currentPathPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { currentPathLabel, _currentPathTextBlockValue }
        };

        _selectFolderButton = new Button { Content = "Select New Folder...", Margin = new Thickness(0, 0, 10, 0) };
        _selectFolderButton.Click += SelectFolderButton_Click; // Manual command execution

        _newPathTextBlockValue = new TextBlock { Text = "No folder selected", FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

        var newPathPanel = new DockPanel
        {
            Margin = new Thickness(0, 5, 0, 15),
            Children = { _selectFolderButton, _newPathTextBlockValue }
        };
        DockPanel.SetDock(_selectFolderButton, Dock.Left);


        _statusTextBlock = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 5),
            MinHeight = 30 // Reserve space
        };

        _progressBar = new ProgressBar
        {
            MinWidth = 150,
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 5, 0, 10),
            IsVisible = false // Start hidden
        };

        _applyButton = new Button { Content = "Apply Changes", IsDefault = true, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        _applyButton.Classes.Add("accent");
        _applyButton.Click += ApplyButton_Click; // Manual command execution

        _closeButton = new Button { Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        _closeButton.Click += (s, e) => Close();

        // --- Layout ---
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0),
            Children = { _closeButton, _applyButton }
        };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 5,
            Children =
            {
                new TextBlock { Text = "Journal Settings", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0,0,0,15), HorizontalAlignment = HorizontalAlignment.Center },
                currentPathPanel,
                newPathPanel,
                _statusTextBlock,
                _progressBar,
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

        _selectFolderCommandInstance = null;
        _applyCommandInstance = null;
        _viewModel = DataContext as SettingsViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _selectFolderCommandInstance = _viewModel.SelectNewFolderCommand;
            _applyCommandInstance = _viewModel.ApplySettingsCommand;

            // Pass owner window to VM
            _viewModel.OwnerWindow = this;

            // Initialize view state from VM
            UpdateCurrentPath(_viewModel.CurrentJournalPath);
            UpdateNewPath(_viewModel.NewJournalPath);
            UpdateStatus(_viewModel.StatusMessage);
            UpdateProgressVisibility(_viewModel.IsApplying);
            UpdateApplyButtonState();
        }
        else
        {
            // Clear view state
            UpdateCurrentPath(null);
            UpdateNewPath(null);
            UpdateStatus(null);
            UpdateProgressVisibility(false);
            UpdateApplyButtonState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.CurrentJournalPath):
                UpdateCurrentPath(_viewModel.CurrentJournalPath);
                UpdateApplyButtonState(); // Path change might affect CanExecute
                break;
            case nameof(SettingsViewModel.NewJournalPath):
                UpdateNewPath(_viewModel.NewJournalPath);
                UpdateApplyButtonState();
                break;
            case nameof(SettingsViewModel.StatusMessage):
                UpdateStatus(_viewModel.StatusMessage);
                break;
            case nameof(SettingsViewModel.IsApplying):
                UpdateProgressVisibility(_viewModel.IsApplying);
                UpdateApplyButtonState();
                break;
        }
    }

    private void SelectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        _selectFolderCommandInstance?.Execute(null);
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        _applyCommandInstance?.Execute(null);
    }

    // --- Helper methods for updating View state ---
    private void UpdateCurrentPath(string? path)
    {
        _currentPathTextBlockValue.Text = path ?? "Not set";
        ToolTip.SetTip(_currentPathTextBlockValue, path); // Use ToolTip.SetTip
    }

    private void UpdateNewPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            _newPathTextBlockValue.Text = "No folder selected";
            _newPathTextBlockValue.FontStyle = FontStyle.Italic;
            ToolTip.SetTip(_newPathTextBlockValue, null); // Use ToolTip.SetTip
        }
        else
        {
            _newPathTextBlockValue.Text = path;
            _newPathTextBlockValue.FontStyle = FontStyle.Normal;
            ToolTip.SetTip(_newPathTextBlockValue, path); // Use ToolTip.SetTip
        }
    }

    private void UpdateStatus(string? message)
    {
        _statusTextBlock.Text = message ?? string.Empty;
    }

    private void UpdateProgressVisibility(bool isVisible)
    {
        _progressBar.IsVisible = isVisible;
    }

    private void UpdateApplyButtonState()
    {
        _applyButton.IsEnabled = _applyCommandInstance?.CanExecute(null) ?? false;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_viewModel != null)
        {
            _viewModel.OwnerWindow = null; // Clear owner ref
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        _viewModel = null;
        _selectFolderCommandInstance = null;
        _applyCommandInstance = null;
    }
}