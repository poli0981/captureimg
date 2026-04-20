using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Settings tab — language picker, format + quality, output folder, import/export, and
/// open-settings-file. Persists every change through <see cref="ISettingsStore.Update"/> so
/// the debounced writer handles the disk.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsStore _settings;
    private readonly IToastService _toasts;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _suppressPush;
    private bool _disposed;

    public ILocalizationService Localization { get; }

    /// <summary>Child VM driving the Settings → Capture hotkey recorder.</summary>
    public HotkeyBindingViewModel Hotkey { get; }

    public ObservableCollection<CultureInfo> SupportedCultures { get; }

    public ObservableCollection<ImageFormat> SupportedFormats { get; } =
        new(new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Webp, ImageFormat.Tiff });

    [ObservableProperty]
    private CultureInfo _selectedCulture;

    [ObservableProperty]
    private ImageFormat _defaultFormat;

    [ObservableProperty]
    private int _jpegQuality;

    [ObservableProperty]
    private int _webpQuality;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _fileNameTemplate = string.Empty;

    [ObservableProperty]
    private bool _previewBeforeSave;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _soundEnabled;

    public string SettingsFilePath => _settings.SettingsFilePath;

    public SettingsViewModel(
        ISettingsStore settings,
        ILocalizationService localization,
        IToastService toasts,
        HotkeyBindingViewModel hotkey,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _toasts = toasts;
        _logger = logger;
        Localization = localization;
        Hotkey = hotkey;

        SupportedCultures = new ObservableCollection<CultureInfo>(localization.SupportedCultures);
        _selectedCulture = FindCulture(settings.Current.Culture) ?? SupportedCultures[0];

        Hydrate();
        _settings.Changed += OnSettingsStoreChanged;

        // Raise OnPropertyChanged(nameof(Localization)) on culture switch so every
        // `{Binding Localization[Key]}` in SettingsView re-resolves the full path and
        // picks up the new translation in-place. The service's own "Item[]" INPC fires,
        // but Avalonia's compiled indexer bindings through an intermediate property do
        // not always listen to that; bumping the property name at the VM level forces
        // re-evaluation and matches the pattern used by NavItemViewModel.
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    private void OnSettingsStoreChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        Hydrate();
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Localization));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.Changed -= OnSettingsStoreChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    private CultureInfo? FindCulture(string name)
    {
        foreach (var c in SupportedCultures)
        {
            if (c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }

    private void Hydrate()
    {
        _suppressPush = true;
        try
        {
            var current = _settings.Current;
            SelectedCulture = FindCulture(current.Culture) ?? SupportedCultures[0];
            DefaultFormat = current.Capture.DefaultFormat;
            JpegQuality = current.Capture.JpegQuality;
            WebpQuality = current.Capture.WebpQuality;
            OutputDirectory = current.Capture.OutputDirectory;
            FileNameTemplate = current.Capture.FileNameTemplate;
            PreviewBeforeSave = current.Capture.PreviewBeforeSave;
            MinimizeToTray = current.UI.MinimizeToTray;
            SoundEnabled = current.UI.SoundEnabled;
        }
        finally
        {
            _suppressPush = false;
        }
    }

    partial void OnSelectedCultureChanged(CultureInfo value)
    {
        if (_suppressPush || value is null) return;
        _settings.Update(s => s with { Culture = value.Name });
        Localization.SetCulture(value);
    }

    partial void OnDefaultFormatChanged(ImageFormat value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { DefaultFormat = value } });
    }

    partial void OnJpegQualityChanged(int value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { JpegQuality = Clamp(value) } });
    }

    partial void OnWebpQualityChanged(int value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { WebpQuality = Clamp(value) } });
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { OutputDirectory = value ?? string.Empty } });
    }

    partial void OnFileNameTemplateChanged(string value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { FileNameTemplate = value ?? string.Empty } });
    }

    partial void OnPreviewBeforeSaveChanged(bool value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { Capture = s.Capture with { PreviewBeforeSave = value } });
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { UI = s.UI with { MinimizeToTray = value } });
    }

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_suppressPush) return;
        _settings.Update(s => s with { UI = s.UI with { SoundEnabled = value } });
    }

    private static int Clamp(int v) => v < 1 ? 1 : v > 100 ? 100 : v;

    [RelayCommand]
    private void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SettingsFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open settings file.");
            _toasts.ShowError(Localization["Toast_Error"], ex.Message);
        }
    }

    /// <summary>
    /// Exposed as an async command. The View wires it to a <c>SaveFilePicker</c> and passes
    /// the resolved path in as the command parameter.
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            await _settings.ExportAsync(path).ConfigureAwait(true);
            _toasts.ShowSuccess(Localization["Toast_SettingsExported"], path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Settings export failed.");
            _toasts.ShowError(Localization["Toast_Error"], ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            await _settings.ImportAsync(path).ConfigureAwait(true);
            _toasts.ShowSuccess(Localization["Toast_SettingsImported"], path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Settings import failed.");
            _toasts.ShowError(Localization["Toast_Error"], ex.Message);
        }
    }
}
