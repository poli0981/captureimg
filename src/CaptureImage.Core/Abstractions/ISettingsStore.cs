using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Persistence for <see cref="AppSettings"/>. The orchestrator treats this as a live
/// cache — reads return the current in-memory copy, writes debounce to disk.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Current in-memory settings. Never <c>null</c>; defaults are returned before the first load.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Raised after <see cref="Current"/> has been mutated (via <see cref="Update"/>,
    /// <see cref="LoadAsync"/>, or <see cref="ImportAsync"/>). Subscribers run on whatever
    /// thread the change happened on.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Absolute path to the settings file on disk.</summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Load settings from disk into <see cref="Current"/>. If the file does not exist or
    /// fails to parse, defaults are used and a warning is logged. Safe to call repeatedly.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply <paramref name="mutator"/> to a copy of <see cref="Current"/> and make it the
    /// new current value. Writes are debounced — call this as often as you like, the disk
    /// only sees one write per quiet window.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> mutator);

    /// <summary>
    /// Force an immediate, non-debounced flush to disk. Use at app shutdown or before
    /// import/export operations.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Serialize the current settings to <paramref name="exportPath"/>. Existing files are
    /// overwritten.
    /// </summary>
    Task ExportAsync(string exportPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load + validate settings from <paramref name="importPath"/>, adopt them as the new
    /// <see cref="Current"/>, and immediately flush. Throws on parse errors or unknown versions.
    /// </summary>
    Task ImportAsync(string importPath, CancellationToken cancellationToken = default);
}
