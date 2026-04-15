namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Locates the Steam client install directory. Production resolves from registry;
/// tests inject a fixed path.
/// </summary>
public interface ISteamRootLocator
{
    /// <summary>
    /// Absolute path to the Steam install directory (the folder that contains
    /// <c>steamapps\libraryfolders.vdf</c>), or <c>null</c> if Steam is not installed.
    /// </summary>
    string? TryFindSteamRoot();
}
