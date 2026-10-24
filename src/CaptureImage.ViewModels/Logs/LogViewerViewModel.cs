// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Logs;

/// <summary>
/// Logs page: the in-memory log buffer with level filter, search and pause. New entries are
/// batched onto the UI thread, and only while the page is visible.
/// </summary>
public sealed partial class LogViewerViewModel : ViewModelBase, IDisposable
{
    private const int MaxVisible = 2000;
    private const string ZipFilter = "Zip (*.zip)|*.zip";

    private readonly ILogBufferSource _buffer;
    private readonly IClipboardService _clipboard;
    private readonly IShellService _shell;
    private readonly IDialogService _dialogs;
    private readonly IDiagnosticsExporter _diagnostics;
    private readonly IToastService _toasts;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly AppPaths _paths;
    private readonly ILogger<LogViewerViewModel> _logger;
    private readonly ConcurrentQueue<LogEntry> _pending = new();
    private int _flushScheduled;
    private bool _isPageActive;
    private bool _disposed;

    public LogViewerViewModel(
        ILogBufferSource buffer,
        IClipboardService clipboard,
        IShellService shell,
        IDialogService dialogs,
        IDiagnosticsExporter diagnostics,
        IToastService toasts,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        AppPaths paths,
        ILogger<LogViewerViewModel> logger)
    {
        _buffer = buffer;
        _clipboard = clipboard;
        _shell = shell;
        _dialogs = dialogs;
        _diagnostics = diagnostics;
        _toasts = toasts;
        _dispatcher = dispatcher;
        Localization = localization;
        _paths = paths;
        _logger = logger;

        LevelOptions =
        [
            LocalizedOption.Create(LogEntryLevel.Verbose, localization, "Logs_LevelAll"),
            LocalizedOption.Create(LogEntryLevel.Debug, localization, "Settings_LogLevelDebug"),
            LocalizedOption.Create(LogEntryLevel.Information, localization, "Settings_LogLevelInformation"),
            LocalizedOption.Create(LogEntryLevel.Warning, localization, "Settings_LogLevelWarning"),
            LocalizedOption.Create(LogEntryLevel.Error, localization, "Settings_LogLevelError"),
        ];

        _buffer.Emitted += OnEmitted;
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    public ILocalizationService Localization { get; }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public IReadOnlyList<LocalizedOption<LogEntryLevel>> LevelOptions { get; }

    [ObservableProperty]
    public partial LogEntryLevel MinimumLevel { get; set; } = LogEntryLevel.Verbose;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySelectedCommand))]
    public partial LogEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    /// <summary>Set by the view; the list only tracks the buffer while the page is visible.</summary>
    public bool IsPageActive
    {
        get => _isPageActive;
        set
        {
            if (_isPageActive == value)
            {
                return;
            }
            _isPageActive = value;
            if (value)
            {
                Reload();
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _buffer.Clear();
        _pending.Clear();
        Entries.Clear();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task CopySelectedAsync()
    {
        if (SelectedEntry is { } entry && await _clipboard.CopyTextAsync(Format(entry)).ConfigureAwait(true))
        {
            _toasts.ShowSuccess(Localization["Toast_ClipboardCopied"], string.Empty);
        }
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        var sb = new StringBuilder();
        foreach (var entry in Entries)
        {
            sb.AppendLine(Format(entry));
        }

        if (sb.Length > 0 && await _clipboard.CopyTextAsync(sb.ToString()).ConfigureAwait(true))
        {
            _toasts.ShowSuccess(Localization["Toast_ClipboardCopied"], string.Empty);
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        _shell.Open(_paths.LogsDirectory);
    }

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

    private bool HasSelection() => SelectedEntry is not null;

    partial void OnMinimumLevelChanged(LogEntryLevel value) => Reload();

    partial void OnSearchTextChanged(string value) => Reload();

    partial void OnIsPausedChanged(bool value)
    {
        _buffer.Paused = value;
        if (!value)
        {
            Reload();
        }
    }

    /// <summary>Plain-text form used for copy.</summary>
    public static string Format(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.SourceContext}: {entry.Message}");
        return entry.Exception is null ? line : line + Environment.NewLine + entry.Exception;
    }

    private bool Matches(LogEntry entry)
    {
        if (entry.Level < MinimumLevel)
        {
            return false;
        }

        var query = SearchText.Trim();
        return query.Length == 0
            || entry.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || entry.SourceContext.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (entry.Exception?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void Reload()
    {
        if (_disposed)
        {
            return;
        }

        _pending.Clear();
        Entries.Clear();
        var snapshot = _buffer.Snapshot();
        var start = 0;
        var matches = snapshot.Where(Matches).ToList();
        if (matches.Count > MaxVisible)
        {
            start = matches.Count - MaxVisible;
        }
        for (var i = start; i < matches.Count; i++)
        {
            Entries.Add(matches[i]);
        }
    }

    private void OnEmitted(object? sender, LogEntry entry)
    {
        if (_disposed || !_isPageActive)
        {
            return;
        }

        _pending.Enqueue(entry);
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            _dispatcher.Post(Flush);
        }
    }

    private void Flush()
    {
        Volatile.Write(ref _flushScheduled, 0);
        if (_disposed || !_isPageActive)
        {
            _pending.Clear();
            return;
        }

        while (_pending.TryDequeue(out var entry))
        {
            if (Matches(entry))
            {
                Entries.Add(entry);
            }
        }

        while (Entries.Count > MaxVisible)
        {
            Entries.RemoveAt(0);
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == "Item[]")
        {
            LocalizedOption.RefreshAll(LevelOptions);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _buffer.Emitted -= OnEmitted;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Diagnostics export failed.")]
    private partial void LogExportFailed(Exception exception);
}
