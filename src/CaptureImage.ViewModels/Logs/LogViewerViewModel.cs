using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Logging;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
// Disambiguate — `LogLevel` here means the one in LogEntry (Serilog-derived severity),
// not MEL's `Microsoft.Extensions.Logging.LogLevel` used by the `_logger` field.
using LogLevel = CaptureImage.Core.Models.LogLevel;

namespace CaptureImage.ViewModels.Logs;

/// <summary>
/// Live log viewer for the drawer pane. Hydrates from the ring buffer on first show,
/// appends new entries as they fire, and supports pause/resume/clear/export plus a
/// view-side level filter and a button to open the rolling-file folder in Explorer.
/// </summary>
public sealed partial class LogViewerViewModel : ViewModelBase, IDisposable
{
    /// <summary>UI cap — the sink buffer holds more, but rendering 2000 rows is already
    /// the budget; we don't push them all into <see cref="Entries"/>.</summary>
    private const int MaxVisibleEntries = 500;

    private readonly ILogBufferSource _source;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<LogViewerViewModel> _logger;
    private readonly IToastService _toasts;
    private bool _hydrated;
    private bool _disposed;

    public ILocalizationService Localization { get; }

    public ObservableCollection<LogEntry> Entries { get; } = new();

    /// <summary>Options shown in the level-filter dropdown. Order matters: lowest to highest.</summary>
    public IReadOnlyList<LogLevel> FilterLevels { get; } = new[]
    {
        LogLevel.Verbose,
        LogLevel.Debug,
        LogLevel.Information,
        LogLevel.Warning,
        LogLevel.Error,
    };

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// Minimum level the viewer displays. Entries below this get hidden from
    /// <see cref="Entries"/> but still live in the sink buffer, so Export always carries
    /// the full history regardless of this filter.
    /// </summary>
    [ObservableProperty]
    private LogLevel _selectedFilterLevel = LogLevel.Verbose;

    public LogViewerViewModel(
        ILogBufferSource source,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        ILogger<LogViewerViewModel> logger,
        IToastService toasts)
    {
        _source = source;
        _dispatcher = dispatcher;
        Localization = localization;
        _logger = logger;
        _toasts = toasts;

        _source.Emitted += OnEmitted;
        Localization.PropertyChanged += OnLocalizationChanged;

        Entries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(EventsCountText));
            OnPropertyChanged(nameof(HasNoEntries));
        };
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(TogglePauseLabel));
            OnPropertyChanged(nameof(EventsCountText));
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(FilterLabel));
            OnPropertyChanged(nameof(RevealFolderLabel));
            // Refresh the `{Binding Localization[Log_Title]}` / `[Log_Clear]` direct
            // indexer bindings in the header. v1.1.1 hotfix — the service's own
            // Item[] notification alone doesn't reliably re-resolve the path.
            OnPropertyChanged(nameof(Localization));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.Emitted -= OnEmitted;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    /// <summary>
    /// Label shown on the Pause/Resume button. Flips on <see cref="IsPaused"/> so one
    /// button drives both states, and re-localizes on culture change.
    /// </summary>
    public string TogglePauseLabel =>
        IsPaused ? Localization["Log_Resume"] : Localization["Log_Pause"];

    /// <summary>Localized "{0} events" line at the right edge of the log header.</summary>
    public string EventsCountText =>
        string.Format(Localization["Log_EventsCount"], Entries.Count);

    /// <summary>Localized hint shown inside the drawer when no log events have fired yet.</summary>
    public string EmptyStateText => Localization["Log_EmptyState"];

    /// <summary>Localized label for the minimum-level filter dropdown.</summary>
    public string FilterLabel => Localization["Log_Filter"];

    /// <summary>Localized button label that opens the rolling-file folder in Explorer.</summary>
    public string RevealFolderLabel => Localization["Log_RevealFolder"];

    public bool HasNoEntries => Entries.Count == 0;

    partial void OnIsPausedChanged(bool value)
    {
        _source.Paused = value;
        OnPropertyChanged(nameof(TogglePauseLabel));
    }

    partial void OnSelectedFilterLevelChanged(LogLevel value)
    {
        // Rebuild the visible list from the sink snapshot whenever the filter moves.
        // O(2000) — trivial and only runs on user input.
        Entries.Clear();
        var snapshot = _source.Snapshot();
        var start = Math.Max(0, snapshot.Count - MaxVisibleEntries);
        for (var i = start; i < snapshot.Count; i++)
        {
            if (PassesFilter(snapshot[i]))
            {
                Entries.Add(snapshot[i]);
            }
        }
    }

    private bool PassesFilter(LogEntry entry) => entry.Level >= SelectedFilterLevel;

    /// <summary>
    /// Called the first time the drawer is shown. Fills the ObservableCollection from the
    /// sink's snapshot so the user sees historical log events too.
    /// </summary>
    public void EnsureHydrated()
    {
        if (_hydrated) return;
        _hydrated = true;
        var snapshot = _source.Snapshot();
        var start = Math.Max(0, snapshot.Count - MaxVisibleEntries);
        for (var i = start; i < snapshot.Count; i++)
        {
            if (PassesFilter(snapshot[i]))
            {
                Entries.Add(snapshot[i]);
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        _source.Clear();
        Entries.Clear();
    }

    [RelayCommand]
    private void TogglePause() => IsPaused = !IsPaused;

    [RelayCommand]
    private void RevealLogsFolder()
    {
        var folder = LogPaths.GetLogsDirectory();
        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open logs folder {Path}.", folder);
            _toasts.ShowError(Localization["Toast_Error"], ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var snapshot = _source.Snapshot();
        var sb = new StringBuilder(snapshot.Count * 120);
        foreach (var entry in snapshot)
        {
            sb.Append('[').Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(' ').Append(entry.Level.ToString().ToUpperInvariant().Substring(0, 3)).Append("] ");
            var fileLine = entry.FileLineText;
            if (!string.IsNullOrEmpty(fileLine))
            {
                sb.Append('(').Append(fileLine).Append(") ");
            }
            if (!string.IsNullOrEmpty(entry.SourceContext))
            {
                sb.Append(entry.SourceContext).Append(": ");
            }
            sb.AppendLine(entry.Message);
            if (!string.IsNullOrEmpty(entry.Exception))
            {
                sb.AppendLine(entry.Exception);
            }
        }
        await File.WriteAllTextAsync(filePath, sb.ToString()).ConfigureAwait(false);
    }

    private void OnEmitted(object? sender, LogEntry entry)
    {
        // Marshal to UI thread before mutating the ObservableCollection.
        _dispatcher.Post(() =>
        {
            if (!PassesFilter(entry)) return;
            Entries.Add(entry);
            while (Entries.Count > MaxVisibleEntries)
            {
                Entries.RemoveAt(0);
            }
        });
    }
}
