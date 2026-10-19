// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Profiles;

/// <summary>
/// Per-game overrides, matched against the captured window's executable. Every override is
/// optional — <c>null</c> (or an empty string) means "use the global setting".
/// </summary>
public sealed record GameProfile
{
    /// <summary>Stable identifier (GUID string) so renames don't break references.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    /// <summary>Full path of the game executable; matched case-insensitively (most specific).</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>File name of the executable (e.g. <c>game.exe</c>); used when the path differs.</summary>
    public string? ExecutableName { get; init; }

    /// <summary>Steam AppId; matches any window whose executable belongs to that app.</summary>
    public uint? SteamAppId { get; init; }

    /// <summary>Absolute output folder for this game's captures.</summary>
    public string? OutputDirectory { get; init; }

    public ImageFormat? Format { get; init; }

    public string? FileNameTemplate { get; init; }

    /// <summary><c>Auto</c> / <c>Off</c>; <c>null</c> follows the global HDR setting.</summary>
    public string? HdrMode { get; init; }

    /// <summary>Arm the hotkeys automatically when this game becomes the foreground window.</summary>
    public bool AutoArm { get; init; }

    /// <summary>When auto-arming, also lock the Window-mode target to the game's window.</summary>
    public bool LockTarget { get; init; } = true;
}
