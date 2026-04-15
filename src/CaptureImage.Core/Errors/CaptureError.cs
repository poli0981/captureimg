namespace CaptureImage.Core.Errors;

/// <summary>
/// Stable error codes for capture failures. The UI layer maps these to localized strings.
/// Never renumber — persisted settings (e.g. "ignore last error") may reference these values.
/// </summary>
public enum CaptureError
{
    /// <summary>Generic/unknown — the engine threw something we didn't classify.</summary>
    Unknown = 0,

    /// <summary>Target window handle was zero or no longer valid by the time we tried to capture.</summary>
    TargetGone = 1,

    /// <summary>Target process died between selection and capture.</summary>
    ProcessExited = 2,

    /// <summary>WGC refused to capture this surface (protected content, HDCP, DRM).</summary>
    ProtectedContent = 3,

    /// <summary>
    /// The Windows.Graphics.Capture session never delivered a frame within the timeout.
    /// Most likely the window was minimized or occluded at capture time.
    /// </summary>
    NoFrameArrived = 4,

    /// <summary>D3D11 device creation or resource allocation failed.</summary>
    GraphicsDeviceFailure = 5,

    /// <summary>The encoder refused the pixel data (unsupported format, buffer too small, etc.).</summary>
    EncodingFailure = 6,

    /// <summary>I/O failure while writing the final file to disk.</summary>
    FileWriteFailure = 7,

    /// <summary>The caller cancelled the capture via a CancellationToken.</summary>
    Cancelled = 8,

    /// <summary>Fallback path (PrintWindow) returned a fully black bitmap.</summary>
    FallbackProducedBlackFrame = 9,
}
