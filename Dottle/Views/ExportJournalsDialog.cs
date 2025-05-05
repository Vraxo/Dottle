using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
// using Avalonia.Data.Converters; // No longer needed directly here
using System.Collections.Generic;
using Dottle.ViewModels;
using Dottle.Converters; // Add using for custom converters

namespace Dottle.Views;

public class ExportJournalsDialog : Window
{
    // --- Define Custom Converters as Static Fields ---
    private static readonly EnumToBooleanConverter s_enumToBooleanConverter = new();
    private static readonly EnumToVisibilityConverter s_enumToVisibilityConverter = new();
    private static readonly EnableSingleFileExportConverter s_enableSingleFileExportConverter = new();


    public bool IsConfirmed { get; private set; } = false;

    public ExportJournalsDialog()
    {
        Title = "Export Journals";
        Width = 500;
        Height = 630;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        ListBox journalListBox = new()
        {
            Height = 300,
            Margin = new(10),
            ItemTemplate = new FuncDataTemplate<ExportJournalItemViewModel>((vm, ns) =>
                new CheckBox
                {
                    Content = vm.DisplayName,
                    [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.IsSelected), BindingMode.TwoWay),
                }, supportsRecycling: true),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ExportJournalsDialogViewModel.JournalItems))
        };

        TextBlock folderPathTextBlock = new()
        {
            Margin = new(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(ExportJournalsDialogViewModel.SelectedFolderPath)) { FallbackValue = "No folder selected" }
        };

        Button selectFolderButton = new()
        {
            Content = "Select Folder...",
            Margin = new(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            [!Button.CommandProperty] = new Binding(nameof(ExportJournalsDialogViewModel.SelectFolderCommand)),
            [!Button.IsEnabledProperty] = new Binding(nameof(ExportJournalsDialogViewModel.CanSelectFolder))
        };

        DockPanel folderPanel = new()
        {
            Margin = new Thickness(10, 0),
            Children = { selectFolderButton, folderPathTextBlock }
        };
        DockPanel.SetDock(selectFolderButton, Dock.Left);

        // --- Export Format Selection ---
        TextBlock formatLabel = new() { Text = "Export Format:", Margin = new(10, 10, 10, 0) };
        RadioButton fullTextRadio = new()
        {
            Content = "Full Text",
            GroupName = "ExportFormat",
            Margin = new(10, 0, 5, 0),
            IsChecked = true, // Default - Initial state, binding will take over
            CommandParameter = ExportFormatType.FullText
        };
        // Use custom EnumToBooleanConverter
        fullTextRadio.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(ExportJournalsDialogViewModel.SelectedExportFormat), BindingMode.TwoWay)
        {
            Converter = s_enumToBooleanConverter,
            ConverterParameter = ExportFormatType.FullText // Pass the target enum value
        });

        RadioButton moodSummaryRadio = new()
        {
            Content = "Mood Summary",
            GroupName = "ExportFormat",
            Margin = new(5, 0, 10, 0),
            CommandParameter = ExportFormatType.MoodSummary,
        };
        // Use custom EnumToBooleanConverter
        moodSummaryRadio.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(ExportJournalsDialogViewModel.SelectedExportFormat), BindingMode.TwoWay)
        {
            Converter = s_enumToBooleanConverter,
            ConverterParameter = ExportFormatType.MoodSummary // Pass the target enum value
        });
        StackPanel formatPanel = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new(10, 5, 10, 5),
            Spacing = 5,
            Children = { fullTextRadio, moodSummaryRadio }
        };

        // --- Sort Direction Selection (Visible only for Mood Summary) ---
        TextBlock sortLabel = new() { Text = "Sort Order (Mood Summary):", Margin = new(10, 5, 10, 0) };
        RadioButton ascendingRadio = new()
        {
            Content = "Ascending",
            GroupName = "SortDirection",
            Margin = new(10, 0, 5, 0),
            IsChecked = true, // Default
            CommandParameter = SortDirection.Ascending
        };
        // Use custom EnumToBooleanConverter
        ascendingRadio.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(ExportJournalsDialogViewModel.SelectedSortDirection), BindingMode.TwoWay)
        {
            Converter = s_enumToBooleanConverter,
            ConverterParameter = SortDirection.Ascending // Pass the target enum value
        });

        RadioButton descendingRadio = new()
        {
            Content = "Descending",
            GroupName = "SortDirection",
            Margin = new(5, 0, 10, 0),
            CommandParameter = SortDirection.Descending,
        };
        // Use custom EnumToBooleanConverter
        descendingRadio.Bind(RadioButton.IsCheckedProperty, new Binding(nameof(ExportJournalsDialogViewModel.SelectedSortDirection), BindingMode.TwoWay)
        {
            Converter = s_enumToBooleanConverter,
            ConverterParameter = SortDirection.Descending // Pass the target enum value
        });
        StackPanel sortPanel = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new(10, 5, 10, 5),
            Spacing = 5,
            Children = { ascendingRadio, descendingRadio },
        };
        // Use custom EnumToVisibilityConverter
        sortPanel.Bind(StackPanel.IsVisibleProperty, new Binding(nameof(ExportJournalsDialogViewModel.SelectedExportFormat))
        {
            Converter = s_enumToVisibilityConverter,
            ConverterParameter = ExportFormatType.MoodSummary // Make visible when format is MoodSummary
        });

        // --- Existing CheckBox ---
        CheckBox exportSingleFileCheckBox = new()
        {
            Content = "Export selected journals into a single file (Full Text only)",
            Margin = new(10, 5, 10, 5), // Adjusted margin
            HorizontalAlignment = HorizontalAlignment.Left
            // Bind IsChecked directly without converter first
        };
        exportSingleFileCheckBox.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(ExportJournalsDialogViewModel.ExportAsSingleFile), BindingMode.TwoWay));

        // Use custom EnableSingleFileExportConverter
        exportSingleFileCheckBox.Bind(CheckBox.IsEnabledProperty, new MultiBinding
        {
            Bindings =
                {
                    new Binding(nameof(ExportJournalsDialogViewModel.CanSelectFolder)),
                    new Binding(nameof(ExportJournalsDialogViewModel.SelectedExportFormat))
                },
            Converter = s_enableSingleFileExportConverter // Use the custom multi-converter
        });

        ProgressBar progressBar = new()
        {
            Margin = new Thickness(10, 5),
            Minimum = 0,
            Maximum = 100,
            [!ProgressBar.ValueProperty] = new Binding(nameof(ExportJournalsDialogViewModel.ExportProgress)),
            [!ProgressBar.IsVisibleProperty] = new Binding(nameof(ExportJournalsDialogViewModel.IsExporting))
        };

        TextBlock statusTextBlock = new()
        {
            Margin = new Thickness(10, 0, 10, 5),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(ExportJournalsDialogViewModel.StatusMessage))
        };

        Button exportButton = new()
        {
            Content = "Export Selected",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new(5),
            Classes = { "accent" },
            [!Button.CommandProperty] = new Binding(nameof(ExportJournalsDialogViewModel.ExportCommand)),
            [!Button.IsEnabledProperty] = new Binding(nameof(ExportJournalsDialogViewModel.CanExport))
        };

        Button closeButton = new()
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new(5)
        };
        closeButton.Click += (s, e) => Close(false);

        StackPanel buttonPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new(10),
            Spacing = 5,
            Children = { exportButton, closeButton }
        };

        DockPanel mainPanel = new()
        {
            Children =
            {
                buttonPanel,
                statusTextBlock,
                progressBar,
                exportSingleFileCheckBox, // Checkbox now below sort panel
                sortPanel,                // Sort options
                formatLabel,              // Format label
                formatPanel,              // Format options
                folderPanel,
                journalListBox
            }
        };

        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        DockPanel.SetDock(statusTextBlock, Dock.Bottom);
        DockPanel.SetDock(progressBar, Dock.Bottom);
        DockPanel.SetDock(exportSingleFileCheckBox, Dock.Bottom);
        DockPanel.SetDock(sortPanel, Dock.Bottom);
        DockPanel.SetDock(formatPanel, Dock.Bottom);
        DockPanel.SetDock(formatLabel, Dock.Bottom);
        DockPanel.SetDock(folderPanel, Dock.Bottom);

        Content = mainPanel;

        // Ensure DataContext is set if not done elsewhere (usually in App.xaml or code-behind)
        // this.DataContext = new ExportJournalsDialogViewModel(...); // Assuming instantiation happens elsewhere
    }

    public new void Close(object? result = null)
    {
        base.Close(result);
    }
}
