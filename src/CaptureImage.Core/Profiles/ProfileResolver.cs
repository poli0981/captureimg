// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Profiles;

/// <summary>
/// Picks the profile that applies to a capture target. Precedence: exact executable path,
/// then executable file name, then Steam AppId. Disabled profiles never match.
/// </summary>
public static class ProfileResolver
{
    public static GameProfile? Resolve(IReadOnlyList<GameProfile> profiles, GameTarget? target)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (target is null || profiles.Count == 0)
        {
            return null;
        }

        var path = target.ExecutablePath;
        var fileName = string.IsNullOrEmpty(path) ? target.ProcessName + ".exe" : Path.GetFileName(path);

        GameProfile? byName = null;
        GameProfile? bySteam = null;
        foreach (var profile in profiles)
        {
            if (!profile.Enabled)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(profile.ExecutablePath)
                && !string.IsNullOrEmpty(path)
                && string.Equals(NormalizePath(profile.ExecutablePath), NormalizePath(path), StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }

            if (byName is null
                && !string.IsNullOrWhiteSpace(profile.ExecutableName)
                && string.Equals(profile.ExecutableName.Trim(), fileName, StringComparison.OrdinalIgnoreCase))
            {
                byName = profile;
            }

            if (bySteam is null
                && profile.SteamAppId is { } appId
                && target.SteamInfo?.AppId == appId)
            {
                bySteam = profile;
            }
        }

        return byName ?? bySteam;
    }

    private static string NormalizePath(string path) => path.Trim().Replace('/', '\\').TrimEnd('\\');
}
