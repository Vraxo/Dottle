using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
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

        CheckBox exportSingleFileCheckBox = new()
        {
            Content = "Export selected journals into a single file",
            Margin = new(10, 10, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            [!CheckBox.IsCheckedProperty] = new Binding(nameof(ExportJournalsDialogViewModel.ExportAsSingleFile), BindingMode.TwoWay),
            [!CheckBox.IsEnabledProperty] = new Binding(nameof(ExportJournalsDialogViewModel.CanSelectFolder))
        };

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
                exportSingleFileCheckBox,
                folderPanel,
                journalListBox
            }
        };

        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        DockPanel.SetDock(statusTextBlock, Dock.Bottom);
        DockPanel.SetDock(progressBar, Dock.Bottom);
        DockPanel.SetDock(exportSingleFileCheckBox, Dock.Bottom);
        DockPanel.SetDock(folderPanel, Dock.Bottom);

        Content = mainPanel;
    }

    public new void Close(object? result = null)
    {
        base.Close(result);
    }
}