using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Core.Pipeline;

/// <summary>
/// End-to-end coordinator for a single capture: engine → encoder → file write → result.
/// Lives in Core (portable) so it can be unit-tested with fake engines/encoders.
/// </summary>
public sealed class CaptureOrchestrator
{
    private readonly ICaptureEngine _engine;
    private readonly IReadOnlyList<IImageEncoder> _encoders;
    private readonly FileNameStrategy _fileNameStrategy;
    private readonly ILogger<CaptureOrchestrator> _logger;

    public CaptureOrchestrator(
        ICaptureEngine engine,
        IEnumerable<IImageEncoder> encoders,
        FileNameStrategy fileNameStrategy,
        ILogger<CaptureOrchestrator> logger)
    {
        _engine = engine;
        _encoders = new List<IImageEncoder>(encoders).AsReadOnly();
        _fileNameStrategy = fileNameStrategy;
        _logger = logger;
    }

    /// <summary>
    /// Execute the request: capture a frame, pick the right encoder, encode to a tmp file,
    /// atomically rename, return a success/failure record. Never throws for known-bad paths.
    /// </summary>
    public async Task<CaptureResult> ExecuteAsync(
        CaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(request.OutputDirectory);

            var encoder = ResolveEncoder(request.Format)
                ?? throw new CaptureException(
                    CaptureError.EncodingFailure,
                    $"No encoder registered for format {request.Format}.");

            _logger.LogInformation(
                "Capturing target {Target} → {Format} at {Dir}.",
                request.Target.DisplayName, request.Format, request.OutputDirectory);

            var frame = await _engine.CaptureAsync(request.Target, cancellationToken).ConfigureAwait(false);

            var finalPath = _fileNameStrategy.BuildFilePath(
                request.OutputDirectory,
                request.FileNameTemplate,
                request.Target,
                DateTimeOffset.Now,
                request.Format);

            var tempPath = finalPath + ".tmp";
            long fileSize;
            try
            {
                await using (var fs = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await encoder.EncodeAsync(
                        frame,
                        request.Format,
                        request.JpegQuality,
                        request.WebpQuality,
                        fs,
                        cancellationToken).ConfigureAwait(false);
                    await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    fileSize = fs.Length;
                }

                // Atomic replace — readers never see a half-written file.
                File.Move(tempPath, finalPath, overwrite: true);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Capture OK → {Path} ({Width}x{Height}, {Size} bytes, {Ms}ms).",
                finalPath, frame.Width, frame.Height, fileSize, stopwatch.ElapsedMilliseconds);

            return new CaptureResult.Success(
                FilePath: finalPath,
                Width: frame.Width,
                Height: frame.Height,
                FileSizeBytes: fileSize,
                Duration: stopwatch.Elapsed);
        }
        catch (CaptureException ex)
        {
            _logger.LogWarning(ex, "Capture failed with known error {Code}.", ex.ErrorCode);
            return new CaptureResult.Failure(ex.ErrorCode, ex.Message, ex);
        }
        catch (OperationCanceledException)
        {
            return new CaptureResult.Failure(CaptureError.Cancelled, "Capture was cancelled.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File write failed during capture.");
            return new CaptureResult.Failure(CaptureError.FileWriteFailure, ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception during capture.");
            return new CaptureResult.Failure(CaptureError.Unknown, ex.Message, ex);
        }
    }

    private IImageEncoder? ResolveEncoder(ImageFormat format)
    {
        foreach (var encoder in _encoders)
        {
            if (encoder.Supports(format)) return encoder;
        }
        return null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* swallow — cleanup best-effort */ }
    }
}
