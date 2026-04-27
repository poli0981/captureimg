using System.Diagnostics;
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
                "Starting picture capture of {Target} as {Format}...",
                request.Target.DisplayName, request.Format);

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
            var fileName = Path.GetFileName(finalPath);
            var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
            var sizeKb = fileSize / 1024d;
            _logger.LogInformation(
                "Picture captured: {Name} ({Width}x{Height}, {SizeKb:F1} KB) at {Path} [{Format}].",
                fileName, frame.Width, frame.Height, sizeKb, directory, request.Format);

            return new CaptureResult.Success(
                FilePath: finalPath,
                Width: frame.Width,
                Height: frame.Height,
                FileSizeBytes: fileSize,
                Duration: stopwatch.Elapsed);
        }
        catch (CaptureException ex)
        {
            if (ex.ErrorCode == CaptureError.Cancelled)
            {
                _logger.LogInformation("Picture capture cancelled by user.");
            }
            else
            {
                _logger.LogWarning(ex, "Picture capture failed: {Reason}", ex.Message);
            }
            return new CaptureResult.Failure(ex.ErrorCode, ex.Message, ex);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Picture capture cancelled by user.");
            return new CaptureResult.Failure(CaptureError.Cancelled, "Capture was cancelled.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Picture capture failed: couldn't write to {Path}.", request.OutputDirectory);
            return new CaptureResult.Failure(CaptureError.FileWriteFailure, ex.Message, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Picture capture failed unexpectedly.");
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
