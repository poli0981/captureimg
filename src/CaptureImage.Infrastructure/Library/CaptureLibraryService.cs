// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Vortice.WIC;

namespace CaptureImage.Infrastructure.Library;

/// <summary>
/// The capture library: a JSON index of saved captures (newest first, capped) plus a cache of
/// JPEG thumbnails. Files deleted outside the app drop out on the next read. The first time
/// the index is created, the output folders are scanned so captures made by v1 show up too.
/// </summary>
public sealed partial class CaptureLibraryService(
    AppPaths paths,
    ISettingsStore settings,
    IShellService shell,
    ILogger<CaptureLibraryService> logger) : ICaptureLibrary, IDisposable
{
    private const int MaxRecords = 2000;
    private const int MaxSeededFiles = 500;

    private readonly AppPaths _paths = paths;
    private readonly ISettingsStore _settings = settings;
    private readonly IShellService _shell = shell;
    private readonly ILogger<CaptureLibraryService> _logger = logger;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private readonly Lazy<IWICImagingFactory> _wic = new(() => new IWICImagingFactory(), LazyThreadSafetyMode.ExecutionAndPublication);
    private List<CaptureRecord>? _records;

    public event EventHandler? Changed;

    public async Task<IReadOnlyList<CaptureRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var removed = records.RemoveAll(r => !File.Exists(r.FilePath));
            if (removed > 0)
            {
                LogPruned(removed);
                await SaveAsync(records, cancellationToken).ConfigureAwait(false);
            }
            return [.. records];
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public async Task AddAsync(CaptureRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await LoadAsync(cancellationToken).ConfigureAwait(false);
            records.RemoveAll(r => string.Equals(r.FilePath, record.FilePath, StringComparison.OrdinalIgnoreCase));
            records.Insert(0, record);
            if (records.Count > MaxRecords)
            {
                records.RemoveRange(MaxRecords, records.Count - MaxRecords);
            }
            await SaveAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _indexLock.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> DeleteAsync(CaptureRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var deleted = !File.Exists(record.FilePath) || _shell.MoveToRecycleBin(record.FilePath);
        if (deleted && record.HdrFilePath is { } sidecar && File.Exists(sidecar))
        {
            _shell.MoveToRecycleBin(sidecar);
        }

        if (!deleted)
        {
            return false;
        }

        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = await LoadAsync(cancellationToken).ConfigureAwait(false);
            records.RemoveAll(r => string.Equals(r.FilePath, record.FilePath, StringComparison.OrdinalIgnoreCase));
            await SaveAsync(records, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _indexLock.Release();
        }

        LogDeleted(record.FileName);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public Task<string?> GetThumbnailAsync(CaptureRecord record, int maxSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Task.Run(() => GetOrCreateThumbnail(record.FilePath, Math.Clamp(maxSize, 32, 1024)), cancellationToken);
    }

    public void Dispose()
    {
        _indexLock.Dispose();
        if (_wic.IsValueCreated)
        {
            _wic.Value.Dispose();
        }
    }

    private async Task<List<CaptureRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_records is not null)
        {
            return _records;
        }

        var path = _paths.LibraryIndexFile;
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                _records = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.ListCaptureRecord) ?? [];
                return _records;
            }
            catch (JsonException ex)
            {
                LogIndexUnreadable(ex);
            }
        }

        _records = await Task.Run(SeedFromOutputFolders, cancellationToken).ConfigureAwait(false);
        await SaveAsync(_records, cancellationToken).ConfigureAwait(false);
        return _records;
    }

    private async Task SaveAsync(List<CaptureRecord> records, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var temp = _paths.LibraryIndexFile + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(records, SettingsJsonContext.Default.ListCaptureRecord), cancellationToken)
            .ConfigureAwait(false);
        File.Move(temp, _paths.LibraryIndexFile, overwrite: true);
    }

    /// <summary>Index images already sitting in the output folders (top level only).</summary>
    private List<CaptureRecord> SeedFromOutputFolders()
    {
        var settings = _settings.Current;
        var folders = settings.Profiles
            .Select(p => p.OutputDirectory)
            .Append(string.IsNullOrWhiteSpace(settings.Capture.OutputDirectory) ? AppPaths.DefaultOutputDirectory : settings.Capture.OutputDirectory)
            .Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var files = new List<FileInfo>();
        foreach (var folder in folders)
        {
            try
            {
                files.AddRange(new DirectoryInfo(folder!).EnumerateFiles()
                    .Where(f => ImageFormatExtensions.FromExtension(f.Extension) is { } format && format != ImageFormat.Jxr));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogScanFailed(ex);
            }
        }

        var records = new List<CaptureRecord>();
        foreach (var file in files.OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxSeededFiles))
        {
            var (width, height) = ReadDimensions(file.FullName);
            var sidecar = Path.ChangeExtension(file.FullName, ".jxr");
            records.Add(new CaptureRecord(
                file.FullName,
                new DateTimeOffset(file.LastWriteTime),
                width,
                height,
                ImageFormatExtensions.FromExtension(file.Extension)!.Value,
                file.Length,
                CaptureMode.Window,
                null,
                File.Exists(sidecar) ? sidecar : null));
        }

        LogSeeded(records.Count);
        return records;
    }

    private (int Width, int Height) ReadDimensions(string path)
    {
        try
        {
            using var decoder = _wic.Value.CreateDecoderFromFileName(path, FileAccess.Read, DecodeOptions.CacheOnDemand);
            using var frame = decoder.GetFrame(0);
            var size = frame.Size;
            return (size.Width, size.Height);
        }
        catch (Exception ex) when (ex is SharpGen.Runtime.SharpGenException or IOException or UnauthorizedAccessException)
        {
            return (0, 0);
        }
    }

    private string? GetOrCreateThumbnail(string imagePath, int maxSize)
    {
        var file = new FileInfo(imagePath);
        if (!file.Exists)
        {
            return null;
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{file.FullName.ToUpperInvariant()}|{file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)}|{file.Length.ToString(CultureInfo.InvariantCulture)}")))[..32];
        var thumbnail = Path.Combine(_paths.ThumbnailCacheDirectory, $"{key}_{maxSize.ToString(CultureInfo.InvariantCulture)}.jpg");
        if (File.Exists(thumbnail))
        {
            return thumbnail;
        }

        try
        {
            Directory.CreateDirectory(_paths.ThumbnailCacheDirectory);
            var factory = _wic.Value;
            using var decoder = factory.CreateDecoderFromFileName(file.FullName, FileAccess.Read, DecodeOptions.CacheOnDemand);
            using var frame = decoder.GetFrame(0);
            var size = frame.Size;
            if (size.Width <= 0 || size.Height <= 0)
            {
                return null;
            }

            var scale = Math.Min(1.0, maxSize / (double)Math.Max(size.Width, size.Height));
            var width = Math.Max(1, (int)Math.Round(size.Width * scale));
            var height = Math.Max(1, (int)Math.Round(size.Height * scale));

            using var scaler = factory.CreateBitmapScaler();
            scaler.Initialize(frame, (uint)width, (uint)height, BitmapInterpolationMode.Fant);
            using var converter = factory.CreateFormatConverter();
            converter.Initialize(scaler, PixelFormat.Format24bppBGR, BitmapDitherType.None, null, 0, BitmapPaletteType.Custom);

            var temp = thumbnail + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using var encoder = factory.CreateEncoder(ContainerFormat.Jpeg, stream, BitmapEncoderCacheOption.NoCache);
                using var frameEncode = encoder.CreateNewFrame(out var options);
                using (options)
                {
                    frameEncode.Initialize(options);
                }
                frameEncode.SetSize((uint)width, (uint)height);
                var format = PixelFormat.Format24bppBGR;
                frameEncode.SetPixelFormat(ref format);
                frameEncode.WriteSource(converter);
                frameEncode.Commit();
                encoder.Commit();
            }
            File.Move(temp, thumbnail, overwrite: true);
            return thumbnail;
        }
        catch (Exception ex) when (ex is SharpGen.Runtime.SharpGenException or IOException or UnauthorizedAccessException)
        {
            LogThumbnailFailed(ex, file.Name);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Library: dropped {Count} captures whose files were deleted outside the app.")]
    private partial void LogPruned(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Library: moved {FileName} to the Recycle Bin.")]
    private partial void LogDeleted(string fileName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Library index could not be read; rebuilding it from the output folders.")]
    private partial void LogIndexUnreadable(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Library index created with {Count} existing captures.")]
    private partial void LogSeeded(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not scan an output folder for existing captures.")]
    private partial void LogScanFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not create a thumbnail for {FileName}.")]
    private partial void LogThumbnailFailed(Exception exception, string fileName);
}
