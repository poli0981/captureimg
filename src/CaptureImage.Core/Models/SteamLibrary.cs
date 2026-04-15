using System.Collections.Generic;

namespace CaptureImage.Core.Models;

/// <summary>
/// A single Steam library folder (Steam supports multiple on disk) together with the
/// list of apps installed in it.
/// </summary>
/// <param name="Path">Absolute path to the library root (the folder that contains <c>steamapps\</c>).</param>
/// <param name="Apps">Apps installed under this library, keyed by <see cref="SteamAppInfo.AppId"/>.</param>
public sealed record SteamLibrary(string Path, IReadOnlyDictionary<uint, SteamAppInfo> Apps);
