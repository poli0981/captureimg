using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Capture an arbitrary rectangular region of the desktop. Implementations show an
/// overlay so the user can drag-select; <c>null</c> means the user cancelled (Esc /
/// click outside / overlay closed).
/// </summary>
public interface IRegionCaptureService
{
    Task<CapturedFrame?> SelectAndCaptureAsync(CancellationToken cancellationToken = default);
}
