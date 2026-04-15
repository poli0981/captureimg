using System;
using System.Threading.Tasks;
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

        if (importButton is not null)
        {
            importButton.Click += OnImportClick;
        }
        if (exportButton is not null)
        {
            exportButton.Click += OnExportClick;
        }
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import settings",
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
            Title = "Export settings",
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
