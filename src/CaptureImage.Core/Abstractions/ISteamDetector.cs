using System.Collections.Generic;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Maps executable paths to Steam application metadata. The user decided (see the plan file)
/// that Steam games get a <b>warning badge, not an ignore</b> — this service returns the
/// <see cref="SteamAppInfo"/> so the UI can render the badge; it does not filter anything out.
/// </summary>
public interface ISteamDetector
{
    /// <summary>
    /// All Steam library roots discovered on this machine (empty if Steam is not installed).
    /// </summary>
    IReadOnlyList<SteamLibrary> Libraries { get; }

    /// <summary>
    /// Return the <see cref="SteamAppInfo"/> for the executable at <paramref name="executablePath"/>,
    /// or <c>null</c> if the executable does not live under a Steam library.
    /// </summary>
    SteamAppInfo? TryGetAppInfo(string executablePath);
}
