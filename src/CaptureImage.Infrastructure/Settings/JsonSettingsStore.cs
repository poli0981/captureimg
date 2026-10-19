// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Text;
using System.Text.Json;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Settings;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Settings;

/// <summary>
/// <c>settings.json</c> in the data directory, with debounced writes.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Writes go to <c>settings.json.tmp</c> and replace the file in one rename
///   (v1 deleted the old file first, so a crash in between lost every setting).</item>
///   <item>Writes are serialized; the debounce timer and <see cref="FlushAsync"/> can no
///   longer write the same temp file concurrently.</item>
///   <item>Unreadable files, newer-schema files and pre-migration files are backed up next
///   to the settings instead of being silently replaced with defaults.</item>
///   <item>On first run the v1 file from the Velopack install folder is copied (never moved).</item>
/// </list>
/// </remarks>
public sealed partial class JsonSettingsStore : ISettingsStore, IDisposable
{
    private const long MaxFileBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan WriteDebounce = TimeSpan.FromMilliseconds(300);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly AppPaths _paths;
    private readonly string? _legacySettingsFile;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Timer _debounce;
    private AppSettings _current = new();
    private bool _dirty;
    private bool _disposed;

    /// <param name="paths">Data directory layout.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="legacySettingsFile">v1 settings to adopt on first run; <c>null</c> disables the import.</param>
    public JsonSettingsStore(AppPaths paths, ILogger<JsonSettingsStore> logger, string? legacySettingsFile)
    {
        _paths = paths;
        _logger = logger;
        _legacySettingsFile = legacySettingsFile;
        _debounce = new Timer(_ => _ = WriteAsync(CancellationToken.None), null, Timeout.Infinite, Timeout.Infinite);
    }

    public AppSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public event EventHandler? Changed;

    public string SettingsFilePath => _paths.SettingsFile;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        AdoptLegacyFile();

        var (settings, needsWrite) = await ReadAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _current = settings;
            _dirty = needsWrite;
        }

        LogLoaded(settings.Version, settings.Profiles.Count);
        if (needsWrite)
        {
            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Func<AppSettings, AppSettings> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var next = mutator(_current) ?? throw new InvalidOperationException("The settings mutator returned null.");
            if (ReferenceEquals(next, _current) || next == _current)
            {
                return;
            }
            _current = next;
            _dirty = true;
            _debounce.Change(WriteDebounce, Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _debounce.Change(Timeout.Infinite, Timeout.Infinite);
        bool dirty;
        lock (_gate)
        {
            dirty = _dirty;
        }
        if (dirty)
        {
            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ExportAsync(string exportPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportPath);
        await WriteFileAtomicallyAsync(exportPath, Serialize(Current), cancellationToken).ConfigureAwait(false);
        LogExported();
    }

    public async Task ImportAsync(string importPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(importPath);
        var file = new FileInfo(importPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The settings file to import does not exist.", importPath);
        }
        if (file.Length > MaxFileBytes)
        {
            throw new InvalidDataException("The file is too large to be a CaptureImage settings file.");
        }

        var json = await File.ReadAllTextAsync(importPath, cancellationToken).ConfigureAwait(false);
        AppSettings? parsed;
        try
        {
            parsed = Deserialize(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The file is not a CaptureImage settings document.", ex);
        }

        if (parsed is null)
        {
            throw new InvalidDataException("The file is not a CaptureImage settings document.");
        }
        if (parsed.Version > AppSettings.CurrentVersion)
        {
            throw new InvalidDataException($"The file was written by a newer CaptureImage (settings version {parsed.Version}).");
        }

        var migrated = SettingsMigrator.Migrate(parsed, out _);
        RejectNetworkFolders(migrated);

        lock (_gate)
        {
            _current = migrated;
            _dirty = true;
        }
        await WriteAsync(cancellationToken).ConfigureAwait(false);
        LogImported();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        Backup("reset");
        lock (_gate)
        {
            // Keep the language so the UI doesn't switch under the user's feet.
            _current = new AppSettings { Culture = _current.Culture };
            _dirty = true;
        }
        await WriteAsync(cancellationToken).ConfigureAwait(false);
        LogReset();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }

        _debounce.Dispose();
        if (_dirty)
        {
            WriteAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        _writeLock.Dispose();
    }

    private async Task<(AppSettings Settings, bool NeedsWrite)> ReadAsync(CancellationToken cancellationToken)
    {
        var path = SettingsFilePath;
        if (!File.Exists(path))
        {
            return (new AppSettings(), true);
        }

        AppSettings? parsed = null;
        try
        {
            if (new FileInfo(path).Length <= MaxFileBytes)
            {
                parsed = Deserialize(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
            }
        }
        catch (JsonException ex)
        {
            LogUnreadable(ex);
        }

        if (parsed is null)
        {
            Backup("corrupt");
            return (new AppSettings(), true);
        }

        if (parsed.Version != AppSettings.CurrentVersion)
        {
            // Keep the original: older for safety, newer so that build can still read its file.
            Backup($"v{parsed.Version.ToString(CultureInfo.InvariantCulture)}");
        }

        var migrated = SettingsMigrator.Migrate(parsed, out var changed);
        return (migrated, changed);
    }

    private void AdoptLegacyFile()
    {
        if (_legacySettingsFile is null || File.Exists(SettingsFilePath) || !File.Exists(_legacySettingsFile))
        {
            return;
        }

        try
        {
            File.Copy(_legacySettingsFile, SettingsFilePath, overwrite: false);
            LogAdoptedLegacy();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogLegacyCopyFailed(ex);
        }
    }

    private void Backup(string tag)
    {
        var path = SettingsFilePath;
        if (!File.Exists(path))
        {
            return;
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backup = Path.Combine(_paths.DataDirectory, $"settings.{tag}-{stamp}.bak.json");
        try
        {
            File.Copy(path, backup, overwrite: true);
            LogBackedUp(Path.GetFileName(backup));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogBackupFailed(ex);
        }
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AppSettings snapshot;
            lock (_gate)
            {
                snapshot = _current;
                _dirty = false;
            }

            await WriteFileAtomicallyAsync(SettingsFilePath, Serialize(snapshot), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_gate)
            {
                _dirty = true;
            }
            LogWriteFailed(ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task WriteFileAtomicallyAsync(string path, string json, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, json, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// An imported file must not point captures at another machine: writing to a UNC share
    /// would send every screenshot there and authenticate with the user's Windows credentials.
    /// </summary>
    private static void RejectNetworkFolders(AppSettings settings)
    {
        var folders = settings.Profiles.Select(p => p.OutputDirectory).Append(settings.Capture.OutputDirectory);
        foreach (var folder in folders)
        {
            if (!string.IsNullOrWhiteSpace(folder)
                && (folder.StartsWith(@"\\", StringComparison.Ordinal) || folder.StartsWith("//", StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Imported settings may not use a network folder as the output folder.");
            }
        }
    }

    private static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);

    private static AppSettings? Deserialize(string json) =>
        JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings loaded (schema v{Version}, {ProfileCount} game profiles).")]
    private partial void LogLoaded(int version, int profileCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Copied the v1 settings file into the new data folder.")]
    private partial void LogAdoptedLegacy();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not copy the v1 settings file.")]
    private partial void LogLegacyCopyFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "settings.json could not be read; a backup was kept and defaults are used.")]
    private partial void LogUnreadable(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backed up the settings file as {FileName}.")]
    private partial void LogBackedUp(string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not back up the settings file.")]
    private partial void LogBackupFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not write settings.json; the change will be retried.")]
    private partial void LogWriteFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings exported.")]
    private partial void LogExported();

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings imported.")]
    private partial void LogImported();

    [LoggerMessage(Level = LogLevel.Information, Message = "Settings reset to defaults.")]
    private partial void LogReset();
}
