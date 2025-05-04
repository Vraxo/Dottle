using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Dottle.ViewModels;

namespace Dottle.Views;

public class ExportJournalsDialog : Window
{
    public bool IsConfirmed { get; private set; } = false;

    public ExportJournalsDialog()
    {
        Title = "Export Journals";
        Width = 500;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false; // Keep it simple for now

        // --- UI Elements ---

        // Journal ListBox
        var journalListBox = new ListBox
        {
            Height = 300, // Fixed height for the list
            Margin = new Thickness(10),
            ItemTemplate = new FuncDataTemplate<ExportJournalItemViewModel>((vm, ns) =>
                new CheckBox
                {
                    Content = vm.DisplayName,
                    [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.IsSelected), BindingMode.TwoWay),
                    // Optionally bind IsEnabled to !vm.Parent.IsExporting if needed
                }, supportsRecycling: true),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ExportJournalsDialogViewModel.JournalItems))
        };

        // Folder Selection Area
        var folderPathTextBlock = new TextBlock
        {
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(ExportJournalsDialogViewModel.SelectedFolderPath)) { FallbackValue = "No folder selected" }
        };
        var selectFolderButton = new Button
        {
            Content = "Select Folder...",
            Margin = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            [!Button.CommandProperty] = new Binding(nameof(ExportJournalsDialogViewModel.SelectFolderCommand)),
            [!Button.IsEnabledProperty] = new Binding(nameof(ExportJournalsDialogViewModel.CanSelectFolder))
        };
        var folderPanel = new DockPanel
        {
            Margin = new Thickness(10, 0),
            Children = { selectFolderButton, folderPathTextBlock }
        };
        DockPanel.SetDock(selectFolderButton, Dock.Left);

        // Progress Bar
        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10, 5),
            Minimum = 0,
            Maximum = 100,
            [!ProgressBar.ValueProperty] = new Binding(nameof(ExportJournalsDialogViewModel.ExportProgress)),
            [!ProgressBar.IsVisibleProperty] = new Binding(nameof(ExportJournalsDialogViewModel.IsExporting))
        };

        // Status TextBlock
        var statusTextBlock = new TextBlock
        {
            Margin = new Thickness(10, 0, 10, 5),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(ExportJournalsDialogViewModel.StatusMessage))
        };

        // Buttons (Export / Close)
        var exportButton = new Button
        {
            Content = "Export Selected",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(5),
            Classes = { "accent" },
            [!Button.CommandProperty] = new Binding(nameof(ExportJournalsDialogViewModel.ExportCommand)),
            [!Button.IsEnabledProperty] = new Binding(nameof(ExportJournalsDialogViewModel.CanExport))
        };
        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(5)
        };
        closeButton.Click += (s, e) => Close(false); // Always close with false result

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10),
            Spacing = 5,
            Children = { exportButton, closeButton }
        };

        // --- Layout ---
        var mainPanel = new DockPanel
        {
            Children =
            {
                buttonPanel, // Bottom
                statusTextBlock, // Above buttons
                progressBar, // Above status
                folderPanel, // Above progress
                journalListBox // Top, fills remaining space
            }
        };

        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        DockPanel.SetDock(statusTextBlock, Dock.Bottom);
        DockPanel.SetDock(progressBar, Dock.Bottom);
        DockPanel.SetDock(folderPanel, Dock.Bottom);
        // journalListBox fills the rest

        Content = mainPanel;

        // Close window when DataContext signals completion (optional, Close button is primary)
        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is ExportJournalsDialogViewModel vm)
            {
                // Could potentially close automatically after export, but manual close is safer
            }
        };
    }

    // Override Close to manage the result if needed, though not strictly necessary here
    public new void Close(object? result = null)
    {
        // We don't really need a result from this dialog, but keeping the pattern
        base.Close(result);
    }
}
