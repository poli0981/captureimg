namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Spawns a small always-on-top window holding a thumbnail of the most recent capture.
/// Each call creates a fresh window so users can keep multiple captures pinned and
/// compare them side-by-side. Implementation lives in the UI project.
/// </summary>
public interface IPinnedThumbnailHost
{
    /// <summary>
    /// Show a new pinned thumbnail. <paramref name="pngBytes"/> is the encoded image,
    /// <paramref name="filePath"/> is the file the capture was written to (may be null
    /// if clipboard-only mode skipped the save), and <paramref name="targetName"/> is
    /// the display name shown in the title for context.
    /// </summary>
    void Show(byte[] pngBytes, string? filePath, string targetName);
}
