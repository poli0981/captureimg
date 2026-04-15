namespace CaptureImage.Core.Models;

/// <summary>
/// Outcome of <see cref="Abstractions.IUpdateService.CheckAsync"/>. Modelled as a record so
/// the view model can pattern-match without introducing an exception path for the "no
/// update available" case.
/// </summary>
/// <param name="Status">High-level outcome bucket.</param>
/// <param name="CurrentVersion">
/// Version string currently installed. Never null; "0.0.0-dev" when unavailable.
/// </param>
/// <param name="AvailableVersion">
/// Version that would be installed if the user accepted. Null when <see cref="Status"/>
/// is not <see cref="UpdateStatus.UpdateAvailable"/>.
/// </param>
/// <param name="ReleaseNotes">
/// Optional markdown release notes from the GitHub release. May be null when the release
/// omits them or when the source doesn't support notes.
/// </param>
public sealed record UpdateCheckResult(
    UpdateStatus Status,
    string CurrentVersion,
    string? AvailableVersion,
    string? ReleaseNotes);
