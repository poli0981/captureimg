using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels.Logs;

/// <summary>
/// Live log viewer for the drawer pane. Hydrates from the ring buffer on first show,
/// appends new entries as they fire, and supports pause/resume/clear/export.
/// </summary>
public sealed partial class LogViewerViewModel : ViewModelBase
{
    /// <summary>UI cap — the sink buffer holds more, but rendering 2000 rows is already
    /// the budget; we don't push them all into <see cref="Entries"/>.</summary>
    private const int MaxVisibleEntries = 500;

    private readonly ILogBufferSource _source;
    private readonly IUIThreadDispatcher _dispatcher;
    private bool _hydrated;

    public ILocalizationService Localization { get; }

    public ObservableCollection<LogEntry> Entries { get; } = new();

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isVisible;

    public LogViewerViewModel(
        ILogBufferSource source,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization)
    {
        _source = source;
        _dispatcher = dispatcher;
        Localization = localization;

        _source.Emitted += OnEmitted;

        Localization.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
            {
                OnPropertyChanged(nameof(TogglePauseLabel));
                OnPropertyChanged(nameof(EventsCountText));
                OnPropertyChanged(nameof(EmptyStateText));
                // Refresh the `{Binding Localization[Log_Title]}` / `[Log_Clear]` direct
                // indexer bindings in the header. v1.1.1 hotfix — the service's own
                // Item[] notification alone doesn't reliably re-resolve the path.
                OnPropertyChanged(nameof(Localization));
            }
        };

        Entries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(EventsCountText));
            OnPropertyChanged(nameof(HasNoEntries));
        };
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

    public bool HasNoEntries => Entries.Count == 0;

    partial void OnIsPausedChanged(bool value)
    {
        _source.Paused = value;
        OnPropertyChanged(nameof(TogglePauseLabel));
    }

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
            Entries.Add(snapshot[i]);
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
    private async Task ExportAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var snapshot = _source.Snapshot();
        var sb = new StringBuilder(snapshot.Count * 120);
        foreach (var entry in snapshot)
        {
            sb.Append('[').Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(' ').Append(entry.Level.ToString().ToUpperInvariant().Substring(0, 3)).Append("] ");
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
            Entries.Add(entry);
            while (Entries.Count > MaxVisibleEntries)
            {
                Entries.RemoveAt(0);
            }
        });
    }
}
