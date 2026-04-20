using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CaptureImage.ViewModels.Settings;

namespace CaptureImage.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        var importButton = this.FindControl<Button>("ImportButton");
        var exportButton = this.FindControl<Button>("ExportButton");
        var browseOutputButton = this.FindControl<Button>("BrowseOutputButton");

        if (importButton is not null)
        {
            importButton.Click += OnImportClick;
        }
        if (exportButton is not null)
        {
            exportButton.Click += OnExportClick;
        }
        if (browseOutputButton is not null)
        {
            browseOutputButton.Click += OnBrowseOutputClick;
        }
    }

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        // Seed the picker at whatever the user has configured (or the system default if
        // the field is empty). Failing to resolve the suggested start is non-fatal — the
        // picker falls back to its own default.
        IStorageFolder? suggestedStart = null;
        if (!string.IsNullOrWhiteSpace(vm.OutputDirectory))
        {
            try
            {
                suggestedStart = await top.StorageProvider.TryGetFolderFromPathAsync(vm.OutputDirectory);
            }
            catch
            {
                // Ignore — start elsewhere.
            }
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = vm.Localization["Settings_BrowseTitle"],
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStart,
        });

        if (folders.Count == 0) return;

        var path = folders[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            // Two-way binding pushes back into AppSettings through the
            // OnOutputDirectoryChanged partial handler in the VM.
            vm.OutputDirectory = path;
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = vm.Localization["Settings_ImportTitle"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
            },
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            await vm.ImportCommand.ExecuteAsync(path);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = vm.Localization["Settings_ExportTitle"],
            DefaultExtension = "json",
            SuggestedFileName = "captureimage-settings.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
            },
        });

        if (file is null) return;

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
        {
            await vm.ExportCommand.ExecuteAsync(path);
        }
    }
}
