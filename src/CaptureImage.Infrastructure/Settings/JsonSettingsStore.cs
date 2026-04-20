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
    private const int SupportedVersion = 1;

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
        try
        {
            if (!_fs.File.Exists(_filePath))
            {
                _logger.LogInformation("Settings file not found; using defaults at {Path}.", _filePath);
                loaded = new AppSettings();
            }
            else
            {
                var json = await _fs.File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
                loaded = Deserialize(json) ?? new AppSettings();
                if (loaded.Version > SupportedVersion)
                {
                    _logger.LogWarning(
                        "Settings file version {Found} is newer than supported {Supported}; using defaults.",
                        loaded.Version, SupportedVersion);
                    loaded = new AppSettings();
                }
                _logger.LogInformation("Settings loaded from {Path} (version {Version}).",
                    _filePath, loaded.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}; reverting to defaults.", _filePath);
            loaded = new AppSettings();
        }

        SetCurrent(loaded, persistImmediately: false);
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
        try { if (_fs.File.Exists(path)) _fs.File.Delete(path); }
        catch { /* best-effort */ }
    }

    private static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);

    private static AppSettings? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
