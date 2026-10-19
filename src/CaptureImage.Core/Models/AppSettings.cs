// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Profiles;

namespace CaptureImage.Core.Models;

/// <summary>
/// Persisted user settings (<c>settings.json</c>). Serialized with System.Text.Json source
/// generation, camelCase, enums as strings.
/// </summary>
/// <remarks>
/// <para><b>Versioning:</b> bump <see cref="CurrentVersion"/> when the schema changes and add
/// a step to <see cref="Settings.SettingsMigrator"/>. Never renumber or repurpose fields.</para>
/// <para><b>Forward compatibility:</b> unknown fields are kept in <c>ExtensionData</c> and
/// written back unchanged, so a newer build's settings survive a round trip.</para>
/// <para><b>Lenient values:</b> mode-like options are strings parsed with fallbacks
/// (<see cref="SettingValues"/>) so a hand-edited typo can't make the whole file unreadable.</para>
/// </remarks>
public sealed record AppSettings
{
    public const int CurrentVersion = 3;

    public int Version { get; init; } = CurrentVersion;

    /// <summary>BCP-47 UI culture; empty means "follow Windows" (resolved on first run).</summary>
    public string Culture { get; init; } = string.Empty;

    /// <summary><c>System</c>, <c>Light</c>, <c>Dark</c> or <c>HighContrast</c>.</summary>
    public string Theme { get; init; } = "System";

    /// <summary><c>Debug</c>, <c>Information</c>, <c>Warning</c> or <c>Error</c>.</summary>
    public string LogLevel { get; init; } = "Information";

    public HotkeySettings Hotkeys { get; init; } = new();

    public CaptureSettings Capture { get; init; } = new();

    public UiSettings UI { get; init; } = new();

    public IReadOnlyList<GameProfile> Profiles { get; init; } = [];

    /// <summary>v1/v2 single capture hotkey — read by the migration, never written.</summary>
    public HotkeyBinding? CaptureHotkey { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>One global hotkey per capture mode; <see cref="HotkeyBinding.Unbound"/> disables a mode's key.</summary>
public sealed record HotkeySettings
{
    public HotkeyBinding Window { get; init; } = HotkeyBinding.DefaultFor(CaptureMode.Window);

    public HotkeyBinding Region { get; init; } = HotkeyBinding.DefaultFor(CaptureMode.Region);

    public HotkeyBinding Monitor { get; init; } = HotkeyBinding.DefaultFor(CaptureMode.Monitor);

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }

    public HotkeyBinding Get(CaptureMode mode) => mode switch
    {
        CaptureMode.Window => Window,
        CaptureMode.Region => Region,
        CaptureMode.Monitor => Monitor,
        _ => HotkeyBinding.Unbound,
    };

    public HotkeySettings With(CaptureMode mode, HotkeyBinding binding) => mode switch
    {
        CaptureMode.Window => this with { Window = binding },
        CaptureMode.Region => this with { Region = binding },
        CaptureMode.Monitor => this with { Monitor = binding },
        _ => this,
    };
}

/// <summary>Capture and output options.</summary>
public sealed record CaptureSettings
{
    public ImageFormat DefaultFormat { get; init; } = ImageFormat.Png;

    /// <summary>JPEG quality 1-100.</summary>
    public int JpegQuality { get; init; } = 90;

    /// <summary>WebP quality 1-100.</summary>
    public int WebpQuality { get; init; } = 85;

    /// <summary><c>Fast</c>, <c>Balanced</c> or <c>Smallest</c>.</summary>
    public string PngCompression { get; init; } = "Balanced";

    /// <summary>Absolute output folder; empty means <c>Pictures\CaptureImage</c>.</summary>
    public string OutputDirectory { get; init; } = string.Empty;

    public string FileNameTemplate { get; init; } = FileNameStrategy.DefaultTemplate;

    /// <summary>Ask Save / Discard for every capture.</summary>
    public bool PreviewBeforeSave { get; init; }

    /// <summary>Delay before a capture fires: 0, 3, 5 or 10 seconds.</summary>
    public int CountdownSeconds { get; init; }

    /// <summary><c>None</c> (file only), <c>Copy</c> (clipboard only) or <c>CopyAndSave</c>.</summary>
    public string ClipboardMode { get; init; } = "None";

    public bool IncludeCursor { get; init; }

    /// <summary>Keep title bar and borders in window captures.</summary>
    public bool IncludeWindowFrame { get; init; }

    /// <summary><c>Auto</c> (capture HDR on HDR displays) or <c>Off</c> (always 8-bit).</summary>
    public string HdrMode { get; init; } = "Auto";

    /// <summary>Also keep the original HDR frame as a JPEG XR (<c>.jxr</c>) file next to the image.</summary>
    public bool SaveHdrSidecar { get; init; } = true;

    /// <summary>v2 "Window"/"Region" mode switch — read by the migration, never written.</summary>
    public string? Mode { get; init; }

    /// <summary>v2 option dropped in v3 (the Window hotkey captures the foreground window) — never written.</summary>
    public bool? AutoSwitchOnAltTab { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Window and behaviour preferences.</summary>
public sealed record UiSettings
{
    /// <summary>Start hidden in the tray.</summary>
    public bool StartMinimized { get; init; }

    /// <summary>The close button hides to the tray instead of exiting.</summary>
    public bool MinimizeToTray { get; init; } = true;

    public bool SoundEnabled { get; init; } = true;

    public bool OpenFolderAfterSave { get; init; }

    public bool AutoPinAfterCapture { get; init; }

    /// <summary>Show capture notifications.</summary>
    public bool ShowNotifications { get; init; } = true;

    /// <summary>Arm the hotkeys as soon as the app starts.</summary>
    public bool ArmOnStartup { get; init; }

    /// <summary>Mirror of the per-user Run key (the registry is the source of truth).</summary>
    public bool StartWithWindows { get; init; }

    /// <summary>Last main-window placement in device-independent pixels.</summary>
    public WindowPlacement? MainWindow { get; init; }

    /// <summary>v2 option dropped in v3 — never written.</summary>
    public bool? ShowLogViewer { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>A restorable window position and size (device-independent pixels).</summary>
public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);
