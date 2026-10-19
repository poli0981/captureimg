// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Models;

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>Capture targets for tests (pure data — nothing here touches the real desktop).</summary>
internal static class TestTargets
{
    public const string GameExecutable = @"C:\Games\ELDEN RING\Game\eldenring.exe";

    public static GameTarget Game(
        nint windowHandle = 0x1001,
        string title = "Elden Ring",
        string processName = "eldenring",
        string executablePath = GameExecutable,
        SteamAppInfo? steam = null,
        bool minimized = false) =>
        new(
            ProcessId: 4242,
            WindowHandle: windowHandle,
            ProcessName: processName,
            WindowTitle: title,
            ExecutablePath: executablePath,
            IconBytes: null,
            SteamInfo: steam,
            IsMinimized: minimized);
}
