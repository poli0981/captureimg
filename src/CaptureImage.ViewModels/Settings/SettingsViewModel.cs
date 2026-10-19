// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Pipeline;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Settings;

/// <summary>
/// Settings page. Every control writes through to <see cref="ISettingsStore"/> immediately
/// (the store debounces the file write); changes made elsewhere (import, reset) reload the page.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private const string SettingsFileFilter = "Settings (*.json)|*.json";
    private const string ZipFilter = "Zip (*.zip)|*.zip";

    private readonly ISettingsStore _settings;
    private readonly CaptureCoordinator _coordinator;
    private readonly IAutostartService _autostart;
    private readonly ILogLevelSwitcher _logLevelSwitcher;
    private readonly IThemeService _themeService;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly IDiagnosticsExporter _diagnostics;
    private readonly IToastService _toasts;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly AppPaths _paths;
    private readonly TimeProvider _time;
    private readonly ILogger<SettingsViewModel> _logger;
    private AppSettings? _lastWritten;
    private bool _loading;
    private bool _disposed;

    public SettingsViewModel(
        ISettingsStore settings,
        CaptureCoordinator coordinator,
        IHotkeyService hotkeys,
        IAutostartService autostart,
        ILogLevelSwitcher logLevel,
        IThemeService theme,
        IDialogService dialogs,
        IShellService shell,
        IDiagnosticsExporter diagnostics,
        IToastService toasts,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        AppPaths paths,
        TimeProvider time,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _coordinator = coordinator;
        _autostart = autostart;
        _logLevelSwitcher = logLevel;
        _themeService = theme;
        _dialogs = dialogs;
        _shell = shell;
        _diagnostics = diagnostics;
        _toasts = toasts;
        _dispatcher = dispatcher;
        Localization = localization;
        _paths = paths;
        _time = time;
        _logger = logger;

        Formats = [.. ImageFormatExtensions.UserSelectable.Select(f => LocalizedOption.Fixed(f, FormatLabel(f)))];
        PngCompressionOptions =
        [
            LocalizedOption.Create("Fast", localization, "Settings_PngFast"),
            LocalizedOption.Create("Balanced", localization, "Settings_PngBalanced"),
            LocalizedOption.Create("Smallest", localization, "Settings_PngSmallest"),
        ];
        CountdownOptions = [.. SettingValues.CountdownChoices.Select(seconds => new LocalizedOption<int>(
            seconds,
            () => seconds == 0 ? localization["Settings_CountdownOff"] : localization.Format("Settings_CountdownSeconds", seconds)))];
        ClipboardOptions =
        [
            LocalizedOption.Create(nameof(Core.Models.ClipboardMode.None), localization, "Settings_ClipboardNone"),
            LocalizedOption.Create(nameof(Core.Models.ClipboardMode.Copy), localization, "Settings_ClipboardCopy"),
            LocalizedOption.Create(nameof(Core.Models.ClipboardMode.CopyAndSave), localization, "Settings_ClipboardCopyAndSave"),
        ];
        HdrOptions =
        [
            LocalizedOption.Create("Auto", localization, "Settings_HdrAuto"),
            LocalizedOption.Create("Off", localization, "Settings_HdrOff"),
        ];
        ThemeOptions = [.. SettingValues.Themes.Select(t => LocalizedOption.Create(t, localization, $"Settings_Theme{t}"))];
        LogLevelOptions = [.. SettingValues.LogLevels.Select(l => LocalizedOption.Create(l, localization, $"Settings_LogLevel{l}"))];
        Languages = [.. localization.SupportedCultures.Select(c => LocalizedOption.Fixed(c.Name, NativeName(c)))];
        Hotkeys = [.. Enum.GetValues<CaptureMode>().Select(mode => new HotkeyBindingViewModel(mode, settings, coordinator, hotkeys, localization))];

        Load();
        _settings.Changed += OnSettingsChanged;
        _coordinator.StateChanged += OnCoordinatorStateChanged;
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    public ILocalizationService Localization { get; }

    public IReadOnlyList<LocalizedOption<ImageFormat>> Formats { get; }

    public IReadOnlyList<LocalizedOption<string>> PngCompressionOptions { get; }

    public IReadOnlyList<LocalizedOption<int>> CountdownOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> ClipboardOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> HdrOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> ThemeOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> LogLevelOptions { get; }

    public IReadOnlyList<LocalizedOption<string>> Languages { get; }

    public IReadOnlyList<HotkeyBindingViewModel> Hotkeys { get; }

    public string SettingsFilePath => _settings.SettingsFilePath;

    public string DataDirectory => _paths.DataDirectory;

    public string DefaultOutputDirectory => AppPaths.DefaultOutputDirectory;

    // ---- Capture --------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJpeg), nameof(IsWebp), nameof(IsPng), nameof(FileNamePreview))]
    private ImageFormat _defaultFormat;

    [ObservableProperty]
    private int _jpegQuality;

    [ObservableProperty]
    private int _webpQuality;

    [ObservableProperty]
    private string _pngCompression = "Balanced";

    /// <summary>Configured folder; empty means <see cref="DefaultOutputDirectory"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveOutputDirectory), nameof(HasCustomOutputDirectory))]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileNamePreview))]
    private string _fileNameTemplate = FileNameStrategy.DefaultTemplate;

    [ObservableProperty]
    private bool _previewBeforeSave;

    [ObservableProperty]
    private int _countdownSeconds;

    [ObservableProperty]
    private string _clipboardMode = "None";

    [ObservableProperty]
    private bool _includeCursor;

    [ObservableProperty]
    private bool _includeWindowFrame;

    [ObservableProperty]
    private string _hdrMode = "Auto";

    [ObservableProperty]
    private bool _saveHdrSidecar;

    public bool IsJpeg => DefaultFormat == ImageFormat.Jpeg;

    public bool IsWebp => DefaultFormat == ImageFormat.Webp;

    public bool IsPng => DefaultFormat == ImageFormat.Png;

    public string EffectiveOutputDirectory => string.IsNullOrWhiteSpace(OutputDirectory) ? DefaultOutputDirectory : OutputDirectory;

    public bool HasCustomOutputDirectory => !string.IsNullOrWhiteSpace(OutputDirectory);

    /// <summary>What the next file would be called, for a sample game.</summary>
    public string FileNamePreview
    {
        get
        {
            var sample = new GameTarget(0, 0, "game", "Example Game", string.Empty, null, null);
            var path = new FileNameStrategy(_ => false).BuildFilePath(
                string.Empty, FileNameTemplate, sample, CaptureMode.Window, _time.GetLocalNow(), DefaultFormat);
            return Path.GetFileName(path);
        }
    }

    // ---- Behaviour, startup, appearance ---------------------------------------------

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _soundEnabled;

    [ObservableProperty]
    private bool _openFolderAfterSave;

    [ObservableProperty]
    private bool _autoPinAfterCapture;

    [ObservableProperty]
    private bool _showNotifications;

    [ObservableProperty]
    private bool _armOnStartup;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private string _language = "en-US";

    [ObservableProperty]
    private string _logLevel = "Information";

    // ---- Commands ---------------------------------------------------------------------

    [RelayCommand]
    private async Task BrowseOutputFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync(Localization["Settings_OutputFolder"], EffectiveOutputDirectory).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            OutputDirectory = folder;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        try
        {
            Directory.CreateDirectory(EffectiveOutputDirectory);
            _shell.Open(EffectiveOutputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            LogFolderFailed(ex);
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_OutputFolderUnavailable"]);
        }
    }

    [RelayCommand]
    private void ResetOutputFolder() => OutputDirectory = string.Empty;

    [RelayCommand]
    private void ResetFileNameTemplate() => FileNameTemplate = FileNameStrategy.DefaultTemplate;

    [RelayCommand]
    private void OpenDataFolder() => _shell.Open(DataDirectory);

    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        var path = await _dialogs.SaveFileAsync(Localization["Settings_ExportDiagnostics"], _diagnostics.SuggestFileName(), ZipFilter).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            await _settings.FlushAsync().ConfigureAwait(true);
            var written = await _diagnostics.ExportAsync(path).ConfigureAwait(true);
            _toasts.ShowSuccess(Localization["Settings_DiagnosticsExported"], Path.GetFileName(written));
            _shell.RevealInExplorer(written);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LogExportFailed(ex);
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_ExportFailed"]);
        }
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        var path = await _dialogs.SaveFileAsync(Localization["Settings_ExportSettings"], "CaptureImage-settings.json", SettingsFileFilter).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            await _settings.ExportAsync(path).ConfigureAwait(true);
            _toasts.ShowSuccess(Localization["Settings_SettingsExported"], Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogExportFailed(ex);
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_ExportFailed"]);
        }
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        var path = await _dialogs.PickFileAsync(Localization["Settings_ImportSettings"], SettingsFileFilter).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            await _settings.ImportAsync(path).ConfigureAwait(true);
            ApplySideEffects();
            _toasts.ShowSuccess(Localization["Settings_SettingsImported"], Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LogImportFailed(ex);
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_ImportFailed"]);
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            Localization["Settings_ResetTitle"],
            Localization["Settings_ResetMessage"],
            Localization["Settings_Reset"],
            Localization["Common_Cancel"]).ConfigureAwait(true);
        if (!confirmed)
        {
            return;
        }

        try
        {
            await _settings.ResetAsync().ConfigureAwait(true);
            ApplySideEffects();
            _toasts.ShowInfo(Localization["Settings_ResetDone"], string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogImportFailed(ex);
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_ImportFailed"]);
        }
    }

    // ---- Write-through --------------------------------------------------------------

    partial void OnDefaultFormatChanged(ImageFormat value) => Save(s => s with { Capture = s.Capture with { DefaultFormat = value } });

    partial void OnJpegQualityChanged(int value) => Save(s => s with { Capture = s.Capture with { JpegQuality = SettingValues.ClampQuality(value) } });

    partial void OnWebpQualityChanged(int value) => Save(s => s with { Capture = s.Capture with { WebpQuality = SettingValues.ClampQuality(value) } });

    partial void OnPngCompressionChanged(string value) => Save(s => s with { Capture = s.Capture with { PngCompression = value } });

    partial void OnOutputDirectoryChanged(string value) => Save(s => s with { Capture = s.Capture with { OutputDirectory = value.Trim() } });

    partial void OnFileNameTemplateChanged(string value) => Save(s => s with { Capture = s.Capture with { FileNameTemplate = value } });

    partial void OnPreviewBeforeSaveChanged(bool value) => Save(s => s with { Capture = s.Capture with { PreviewBeforeSave = value } });

    partial void OnCountdownSecondsChanged(int value) => Save(s => s with { Capture = s.Capture with { CountdownSeconds = SettingValues.NormalizeCountdown(value) } });

    partial void OnClipboardModeChanged(string value) => Save(s => s with { Capture = s.Capture with { ClipboardMode = value } });

    partial void OnIncludeCursorChanged(bool value) => Save(s => s with { Capture = s.Capture with { IncludeCursor = value } });

    partial void OnIncludeWindowFrameChanged(bool value) => Save(s => s with { Capture = s.Capture with { IncludeWindowFrame = value } });

    partial void OnHdrModeChanged(string value) => Save(s => s with { Capture = s.Capture with { HdrMode = value } });

    partial void OnSaveHdrSidecarChanged(bool value) => Save(s => s with { Capture = s.Capture with { SaveHdrSidecar = value } });

    partial void OnMinimizeToTrayChanged(bool value) => Save(s => s with { UI = s.UI with { MinimizeToTray = value } });

    partial void OnStartMinimizedChanged(bool value) => Save(s => s with { UI = s.UI with { StartMinimized = value } });

    partial void OnSoundEnabledChanged(bool value) => Save(s => s with { UI = s.UI with { SoundEnabled = value } });

    partial void OnOpenFolderAfterSaveChanged(bool value) => Save(s => s with { UI = s.UI with { OpenFolderAfterSave = value } });

    partial void OnAutoPinAfterCaptureChanged(bool value) => Save(s => s with { UI = s.UI with { AutoPinAfterCapture = value } });

    partial void OnShowNotificationsChanged(bool value) => Save(s => s with { UI = s.UI with { ShowNotifications = value } });

    partial void OnArmOnStartupChanged(bool value) => Save(s => s with { UI = s.UI with { ArmOnStartup = value } });

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        if (!_autostart.SetEnabled(value))
        {
            _toasts.ShowError(Localization["Toast_Error"], Localization["Settings_StartWithWindowsFailed"]);
            _dispatcher.Post(() => SetWithoutSaving(() => StartWithWindows = _autostart.IsEnabled));
            return;
        }

        Save(s => s with { UI = s.UI with { StartWithWindows = value } });
    }

    partial void OnThemeChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _themeService.Apply(value);
        Save(s => s with { Theme = value });
    }

    partial void OnLanguageChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value))
        {
            return;
        }

        Localization.SetCulture(CultureInfo.GetCultureInfo(value));
        Save(s => s with { Culture = value });
    }

    partial void OnLogLevelChanged(string value)
    {
        if (_loading)
        {
            return;
        }

        _logLevelSwitcher.SetLevel(value);
        Save(s => s with { LogLevel = value });
    }

    private void Save(Func<AppSettings, AppSettings> mutator)
    {
        if (_loading)
        {
            return;
        }

        _settings.Update(current =>
        {
            var next = mutator(current);
            _lastWritten = next;
            return next;
        });
    }

    private void SetWithoutSaving(Action apply)
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            apply();
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    /// <summary>Copy the stored settings into the page.</summary>
    private void Load() => SetWithoutSaving(() =>
    {
        var s = _settings.Current;
        DefaultFormat = s.Capture.DefaultFormat == ImageFormat.Jxr ? ImageFormat.Png : s.Capture.DefaultFormat;
        JpegQuality = SettingValues.ClampQuality(s.Capture.JpegQuality);
        WebpQuality = SettingValues.ClampQuality(s.Capture.WebpQuality);
        PngCompression = SettingValues.ParsePngCompression(s.Capture.PngCompression).ToString();
        OutputDirectory = s.Capture.OutputDirectory;
        FileNameTemplate = s.Capture.FileNameTemplate;
        PreviewBeforeSave = s.Capture.PreviewBeforeSave;
        CountdownSeconds = SettingValues.NormalizeCountdown(s.Capture.CountdownSeconds);
        ClipboardMode = SettingValues.ParseClipboardMode(s.Capture.ClipboardMode).ToString();
        IncludeCursor = s.Capture.IncludeCursor;
        IncludeWindowFrame = s.Capture.IncludeWindowFrame;
        HdrMode = SettingValues.IsHdrEnabled(s.Capture.HdrMode) ? "Auto" : "Off";
        SaveHdrSidecar = s.Capture.SaveHdrSidecar;
        MinimizeToTray = s.UI.MinimizeToTray;
        StartMinimized = s.UI.StartMinimized;
        SoundEnabled = s.UI.SoundEnabled;
        OpenFolderAfterSave = s.UI.OpenFolderAfterSave;
        AutoPinAfterCapture = s.UI.AutoPinAfterCapture;
        ShowNotifications = s.UI.ShowNotifications;
        ArmOnStartup = s.UI.ArmOnStartup;
        StartWithWindows = _autostart.IsEnabled;
        Theme = SettingValues.NormalizeTheme(s.Theme);
        Language = Localization.CurrentCulture.Name;
        LogLevel = SettingValues.NormalizeLogLevel(s.LogLevel);
        foreach (var hotkey in Hotkeys)
        {
            hotkey.Reload();
        }
        _lastWritten = s;
    });

    /// <summary>After import / reset: apply what lives outside the settings file.</summary>
    private void ApplySideEffects()
    {
        var s = _settings.Current;
        _themeService.Apply(SettingValues.NormalizeTheme(s.Theme));
        _logLevelSwitcher.SetLevel(SettingValues.NormalizeLogLevel(s.LogLevel));
        if (!string.IsNullOrEmpty(s.Culture) && Localization.SupportedCultures.Any(c => c.Name == s.Culture))
        {
            Localization.SetCulture(CultureInfo.GetCultureInfo(s.Culture));
        }
        if (_autostart.IsEnabled != s.UI.StartWithWindows)
        {
            _autostart.SetEnabled(s.UI.StartWithWindows);
        }
        Load();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (!_disposed && !ReferenceEquals(_settings.Current, _lastWritten))
        {
            Load();
        }
    });

    private void OnCoordinatorStateChanged(object? sender, EventArgs e) => _dispatcher.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        var registrations = _coordinator.Registrations;
        foreach (var hotkey in Hotkeys)
        {
            hotkey.ApplyRegistration(registrations.FirstOrDefault(r => r.Mode == hotkey.Mode));
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != "Item[]")
        {
            return;
        }

        LocalizedOption.RefreshAll(PngCompressionOptions);
        LocalizedOption.RefreshAll(CountdownOptions);
        LocalizedOption.RefreshAll(ClipboardOptions);
        LocalizedOption.RefreshAll(HdrOptions);
        LocalizedOption.RefreshAll(ThemeOptions);
        LocalizedOption.RefreshAll(LogLevelOptions);
        foreach (var hotkey in Hotkeys)
        {
            hotkey.RefreshText();
        }
        SetWithoutSaving(() => Language = Localization.CurrentCulture.Name);
    }

    private static string FormatLabel(ImageFormat format) => format switch
    {
        ImageFormat.Png => "PNG",
        ImageFormat.Jpeg => "JPEG",
        ImageFormat.Webp => "WebP",
        ImageFormat.Tiff => "TIFF",
        _ => format.ToString(),
    };

    /// <summary>"Tiếng Việt (Việt Nam)" rather than "tiếng Việt (Việt Nam)".</summary>
    private static string NativeName(CultureInfo culture)
    {
        var name = culture.NativeName;
        return name.Length > 0 ? char.ToUpper(name[0], culture) + name[1..] : culture.Name;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _coordinator.StateChanged -= OnCoordinatorStateChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
        foreach (var hotkey in Hotkeys)
        {
            hotkey.CancelRecordingCommand.Execute(null);
        }
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Could not open the output folder.")]
    private partial void LogFolderFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Export failed.")]
    private partial void LogExportFailed(Exception exception);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Import or reset failed.")]
    private partial void LogImportFailed(Exception exception);
}
