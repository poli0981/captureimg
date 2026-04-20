namespace CaptureImage.Core.Models;

/// <summary>
/// Outcome of a single capture request — either a successfully saved file or a failure reason.
/// </summary>
public abstract record CaptureResult
{
    /// <summary>Successful capture: file has been flushed to disk at <see cref="FilePath"/>.</summary>
    public sealed record Success(
        string FilePath,
        int Width,
        int Height,
        long FileSizeBytes,
        TimeSpan Duration) : CaptureResult;

    /// <summary>
    /// Failure outcome. <see cref="ErrorCode"/> is stable so the UI can localize messages;
    /// <see cref="DeveloperMessage"/> carries the raw diagnostic for logs.
    /// </summary>
    public sealed record Failure(
        Errors.CaptureError ErrorCode,
        string DeveloperMessage,
        Exception? Exception = null) : CaptureResult;
}
