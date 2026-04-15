using System;

namespace CaptureImage.Core.Errors;

/// <summary>
/// Domain exception carrying a stable <see cref="CaptureError"/> code. The orchestrator
/// catches these and converts them to <c>CaptureResult.Failure</c> so view-models
/// don't see exceptions.
/// </summary>
public sealed class CaptureException : Exception
{
    public CaptureError ErrorCode { get; }

    public CaptureException(CaptureError errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public CaptureException(CaptureError errorCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
