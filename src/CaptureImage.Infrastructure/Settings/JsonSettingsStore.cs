using System.IO.Abstractions;
using System.Text.Json;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Settings;

/// <summary>
/// <see cref="ISettingsStore"/> backed by a single <c>settings.json</c> file under
/// <c>%LocalAppData%\CaptureImage\</c>. Writes are atomic (temp-file + rename) and
/// debounced (see <see cref="WriteDebounce"/>) so bursts of <see cref="Update"/> calls
/// don't thrash the disk.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    /// <summary>Quiet window before a pending write hits disk.</summary>
    private static readonly TimeSpan WriteDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>Highest schema version this binary understands.</summary>
    private const int SupportedVersion = 2;

    private readonly IFileSystem _fs;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly object _gate = new();
    private readonly string _filePath;

    private AppSettings _current = new();
    private Timer? _debounceTimer;
    private bool _disposed;

    public JsonSettingsStore(IFileSystem fs, ILogger<JsonSettingsStore> logger)
    {
        _fs = fs;
        _logger = logger;

        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = _fs.Path.Combine(appDataRoot, "CaptureImage");
        _fs.Directory.CreateDirectory(dir);
        _filePath = _fs.Path.Combine(dir, "settings.json");
    }

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler? Changed;

    public string SettingsFilePath => _filePath;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AppSettings loaded;
        // Ensure settings.json exists on disk after a successful load. Four paths trigger
        // a write-back:
        //   (1) file missing — first run, we want the user to see defaults they can edit;
        //   (2) parse failure — bad/corrupt JSON, replace with defaults so next run is clean;
        //   (3) version newer than we support — fall back to defaults;
        //   (4) partial/missing/extra fields — `System.Text.Json` fills init defaults for
        //       absent properties on deserialize, but on-disk file stays skewed; normalize
        //       by comparing re-serialized form with the raw JSON.
        var needsWriteBack = false;
        try
        {
            if (!_fs.File.Exists(_filePath))
            {
                _logger.LogInformation(
                    "Settings file not found at {Path}; initialising with defaults.", _filePath);
                loaded = new AppSettings();
                needsWriteBack = true;
            }
            else
            {
                var json = await _fs.File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
                var parsed = Deserialize(json);
                if (parsed is null)
                {
                    _logger.LogWarning(
                        "Settings file {Path} could not be parsed; replacing with defaults.", _filePath);
                    loaded = new AppSettings();
                    needsWriteBack = true;
                }
                else if (parsed.Version > SupportedVersion)
                {
                    _logger.LogWarning(
                        "Settings file version {Found} is newer than supported {Supported}; using defaults.",
                        parsed.Version, SupportedVersion);
                    loaded = new AppSettings();
                    needsWriteBack = true;
                }
                else
                {
                    // v1 → v2 migration: earlier builds only tracked fields up to UI;
                    // System.Text.Json fills LogLevel with its init default when
                    // deserializing a v1 document, so the data is already correct. We
                    // just need to stamp Version=2 so the canonical form matches the
                    // current schema and the next load recognises it.
                    if (parsed.Version < SupportedVersion)
                    {
                        _logger.LogInformation(
                            "Migrating settings file {Path} from version {From} to {To}.",
                            _filePath, parsed.Version, SupportedVersion);
                        parsed = parsed with { Version = SupportedVersion };
                        needsWriteBack = true;
                    }

                    loaded = parsed;
                    // Partial/missing/extra fields — re-serializing fills init defaults, so
                    // if the canonical form differs from the raw JSON, write back to
                    // normalize the on-disk file.
                    var canonical = Serialize(loaded);
                    if (!string.Equals(json.Trim(), canonical.Trim(), StringComparison.Ordinal))
                    {
                        _logger.LogInformation(
                            "Settings file {Path} differs from canonical form; normalizing.", _filePath);
                        needsWriteBack = true;
                    }
                    _logger.LogInformation(
                        "Settings loaded from {Path} (version {Version}, log level {LogLevel}).",
                        _filePath, loaded.Version, loaded.LogLevel);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}; reverting to defaults.", _filePath);
            loaded = new AppSettings();
            needsWriteBack = true;
        }

        SetCurrent(loaded, persistImmediately: false);

        if (needsWriteBack)
        {
            try
            {
                await WriteAsync(loaded, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Best-effort: Load must succeed even if the disk is read-only. A subsequent
                // Update() will retry via the debounced write path.
                _logger.LogWarning(
                    ex, "Could not write initial/normalized settings file to {Path}.", _filePath);
            }
        }
    }

    public void Update(Func<AppSettings, AppSettings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        AppSettings next;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            next = mutator(_current) ?? throw new InvalidOperationException("Mutator returned null.");
            _current = next;
            ScheduleWrite_NoLock();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        AppSettings snapshot;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            snapshot = _current;
        }
        await WriteAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportAsync(string exportPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            throw new ArgumentException("Export path cannot be empty.", nameof(exportPath));
        }

        AppSettings snapshot;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            snapshot = _current;
        }

        var json = Serialize(snapshot);
        await _fs.File.WriteAllTextAsync(exportPath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Exported settings to {Path}.", exportPath);
    }

    public async Task ImportAsync(string importPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(importPath) || !_fs.File.Exists(importPath))
        {
            throw new FileNotFoundException("Import file not found.", importPath);
        }

        var json = await _fs.File.ReadAllTextAsync(importPath, cancellationToken).ConfigureAwait(false);
        var imported = Deserialize(json)
            ?? throw new InvalidDataException("Import file did not contain a valid settings document.");

        if (imported.Version > SupportedVersion)
        {
            throw new InvalidDataException(
                $"Import file schema version {imported.Version} is newer than supported version {SupportedVersion}.");
        }

        SetCurrent(imported, persistImmediately: true);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Imported settings from {Path}.", importPath);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    // -- internals ----------------------------------------------------------------

    private void SetCurrent(AppSettings next, bool persistImmediately)
    {
        lock (_gate)
        {
            _current = next;
            if (persistImmediately)
            {
                _debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleWrite_NoLock()
    {
        _debounceTimer ??= new Timer(OnDebounceElapsed, state: null, Timeout.Infinite, Timeout.Infinite);
        _debounceTimer.Change(WriteDebounce, Timeout.InfiniteTimeSpan);
    }

    private void OnDebounceElapsed(object? state)
    {
        AppSettings snapshot;
        lock (_gate)
        {
            if (_disposed) return;
            snapshot = _current;
        }

        _ = WriteAsync(snapshot, CancellationToken.None);
    }

    private async Task WriteAsync(AppSettings snapshot, CancellationToken cancellationToken)
    {
        var tempPath = _filePath + ".tmp";
        try
        {
            var json = Serialize(snapshot);
            await _fs.File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

            // Atomic replace — readers never see a half-written file.
            if (_fs.File.Exists(_filePath))
            {
                _fs.File.Delete(_filePath);
            }
            _fs.File.Move(tempPath, _filePath);

            _logger.LogDebug("Settings flushed to {Path}.", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write settings to {Path}.", _filePath);
            TryDelete(tempPath);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (_fs.File.Exists(path)) _fs.File.Delete(path);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup — if the temp file can't be removed, a subsequent write
            // will overwrite it via the rename-into-place path. Log at Debug so we can still
            // correlate if it compounds into a real failure later.
            _logger.LogDebug(ex, "Failed to delete {Path}.", path);
        }
    }

    private static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);

    private AppSettings? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
        }
        catch (JsonException ex)
        {
            // The caller turns a null return into "replace with defaults". Surface the root
            // cause here so the log explains why, not just that parsing failed.
            _logger.LogWarning(
                ex,
                "Malformed settings JSON ({LineNumber}:{BytePositionInLine}); reverting to defaults.",
                ex.LineNumber,
                ex.BytePositionInLine);
            return null;
        }
    }
}
