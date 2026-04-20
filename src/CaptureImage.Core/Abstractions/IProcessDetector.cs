using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Enumerates candidate capture targets — processes with a visible top-level window.
/// Implementations combine window enumeration, process metadata, icon extraction,
/// and Steam attribution into a single call.
/// </summary>
public interface IProcessDetector
{
    /// <summary>
    /// Snapshot the current set of targets. Safe to call repeatedly; detectors
    /// are expected to do their own caching for expensive lookups (icons, Steam metadata).
    /// </summary>
    Task<IReadOnlyList<GameTarget>> EnumerateTargetsAsync(CancellationToken cancellationToken = default);
}
