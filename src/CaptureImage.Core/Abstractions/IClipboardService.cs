namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Abstraction over the OS clipboard so portable code can copy a captured image
/// without taking a Windows.* / WinAppSDK dependency. The UI project supplies the
/// real implementation; tests can inject a fake.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copy the given PNG-encoded image bytes to the clipboard as a bitmap.
    /// Best-effort: returns <c>false</c> if the clipboard is locked or the data
    /// can't be set (the caller may surface a toast but should not crash).
    /// </summary>
    Task<bool> CopyImageAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
