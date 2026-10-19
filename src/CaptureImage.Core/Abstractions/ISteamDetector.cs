// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>Maps executables to installed Steam apps (for the Steam badge and profiles).</summary>
public interface ISteamDetector
{
    SteamAppInfo? TryGetAppInfo(string executablePath);
}
