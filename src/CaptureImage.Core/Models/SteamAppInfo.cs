namespace CaptureImage.Core.Models;

/// <summary>
/// Minimal metadata for a Steam-installed application, parsed from
/// <c>steamapps\appmanifest_*.acf</c>.
/// </summary>
/// <param name="AppId">Steam application ID (integer).</param>
/// <param name="Name">Display name from the manifest.</param>
/// <param name="InstallDir">
/// Directory name under <c>steamapps\common\</c>. Note: this is just the folder name,
/// not a full path. Combine with the library's <see cref="SteamLibrary.Path"/> to resolve.
/// </param>
public sealed record SteamAppInfo(uint AppId, string Name, string InstallDir);
