// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Core.Pipeline;

/// <summary>Everything needed to write one capture to disk.</summary>
/// <param name="Frame">The 8-bit image to save in <paramref name="Format"/>.</param>
/// <param name="HdrFrame">Original HDR frame to keep as a JPEG XR sidecar, or <c>null</c>.</param>
/// <param name="Directory">Output folder (created if missing).</param>
/// <param name="Template">File-name template (see <see cref="FileNameStrategy"/>).</param>
/// <param name="Format">Output format of the main image.</param>
/// <param name="Encoder">Quality and metadata options.</param>
/// <param name="Target">Window the capture belongs to, for naming and the library.</param>
/// <param name="Mode">Capture mode, for naming and the library.</param>
/// <param name="CapturedAt">Capture time (local).</param>
public sealed record SaveRequest(
    CapturedFrame Frame,
    CapturedFrame? HdrFrame,
    string Directory,
    string Template,
    ImageFormat Format,
    EncoderSettings Encoder,
    GameTarget? Target,
    CaptureMode Mode,
    DateTimeOffset CapturedAt);

/// <summary>
/// Encodes a capture into a temporary file and renames it into place without ever
/// overwriting an existing file (a name stolen by a concurrent write is retried).
/// Encoding runs on the thread pool so callers on the UI thread never block.
/// </summary>
public sealed partial class CaptureFileWriter(
    IEnumerable<IImageEncoder> encoders,
    FileNameStrategy fileNames,
    ILogger<CaptureFileWriter> logger)
{
    private const int MaxNameAttempts = 8;

    private readonly IReadOnlyList<IImageEncoder> _encoders = [.. encoders];
    private readonly FileNameStrategy _fileNames = fileNames;
    private readonly ILogger<CaptureFileWriter> _logger = logger;

    public async Task<CaptureRecord> SaveAsync(SaveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var encoder = Resolve(request.Format, request.Frame.Format)
            ?? throw new CaptureException(CaptureError.EncodingFailure, $"No encoder for {request.Format} from {request.Frame.Format}.");

        CreateDirectory(request.Directory);

        for (var attempt = 1; attempt <= MaxNameAttempts; attempt++)
        {
            var path = _fileNames.BuildFilePath(
                request.Directory, request.Template, request.Target, request.Mode, request.CapturedAt, request.Format);

            var size = await WriteAsync(encoder, request.Frame, request.Format, request.Encoder, path, cancellationToken)
                .ConfigureAwait(false);
            if (size < 0)
            {
                LogNameTaken(path, attempt);
                continue;
            }

            var sidecar = request.HdrFrame is { IsHdr: true } hdr
                ? await TryWriteSidecarAsync(hdr, path, request.Encoder, cancellationToken).ConfigureAwait(false)
                : null;

            return new CaptureRecord(
                path,
                request.CapturedAt,
                request.Frame.Width,
                request.Frame.Height,
                request.Format,
                size,
                request.Mode,
                request.Target?.DisplayName,
                sidecar);
        }

        throw new CaptureException(CaptureError.FileWriteFailure, $"Could not find a free file name in {request.Directory}.");
    }

    private IImageEncoder? Resolve(ImageFormat format, FramePixelFormat pixelFormat) =>
        _encoders.FirstOrDefault(e => e.Supports(format, pixelFormat));

    private static void CreateDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CaptureException(CaptureError.FileWriteFailure, $"Cannot create the output folder: {ex.Message}", ex);
        }
    }

    /// <summary>Encode to <c>path.tmp</c> and move it into place; returns -1 when the name is already taken.</summary>
    private static async Task<long> WriteAsync(
        IImageEncoder encoder,
        CapturedFrame frame,
        ImageFormat format,
        EncoderSettings settings,
        string path,
        CancellationToken cancellationToken)
    {
        var tempPath = path + ".tmp";
        try
        {
            long size;
            var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                await Task.Run(() => encoder.EncodeAsync(frame, format, settings, stream, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                size = stream.Length;
            }

            try
            {
                File.Move(tempPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                TryDelete(tempPath);
                return -1;
            }

            return size;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            throw new CaptureException(CaptureError.FileWriteFailure, ex.Message, ex);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private async Task<string?> TryWriteSidecarAsync(
        CapturedFrame hdr,
        string imagePath,
        EncoderSettings settings,
        CancellationToken cancellationToken)
    {
        var encoder = Resolve(ImageFormat.Jxr, FramePixelFormat.RgbaHalf);
        if (encoder is null)
        {
            return null;
        }

        var sidecarPath = Path.ChangeExtension(imagePath, "." + ImageFormat.Jxr.Extension());
        try
        {
            var size = await WriteAsync(encoder, hdr, ImageFormat.Jxr, settings, sidecarPath, cancellationToken)
                .ConfigureAwait(false);
            return size < 0 ? null : sidecarPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The SDR image is already saved; a failed sidecar must not fail the capture
            // (WIC reports encoder failures as COM exceptions, not CaptureException).
            LogSidecarFailed(ex, sidecarPath);
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort — a stale .tmp file is harmless and gets a fresh name next time.
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "File name {Path} was taken during save (attempt {Attempt}); retrying.")]
    private partial void LogNameTaken(string path, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not write the HDR sidecar {Path}.")]
    private partial void LogSidecarFailed(Exception exception, string path);
}
