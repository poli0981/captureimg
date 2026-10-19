// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;

namespace CaptureImage.Core.Models;

/// <summary>Where a capture goes besides (or instead of) a file.</summary>
public enum ClipboardMode
{
    /// <summary>Save to a file only.</summary>
    None,

    /// <summary>Copy to the clipboard, don't write a file.</summary>
    Copy,

    /// <summary>Copy and save.</summary>
    CopyAndSave,
}

/// <summary>Lenient parsers for the string-typed settings (unknown values fall back to defaults).</summary>
public static class SettingValues
{
    public static readonly IReadOnlyList<int> CountdownChoices = [0, 3, 5, 10];

    public static readonly IReadOnlyList<string> LogLevels = ["Debug", "Information", "Warning", "Error"];

    public static readonly IReadOnlyList<string> Themes = ["System", "Light", "Dark", "HighContrast"];

    public static ClipboardMode ParseClipboardMode(string? value) =>
        Enum.TryParse<ClipboardMode>(value, ignoreCase: true, out var mode) && Enum.IsDefined(mode)
            ? mode
            : ClipboardMode.None;

    public static PngCompressionLevel ParsePngCompression(string? value) =>
        Enum.TryParse<PngCompressionLevel>(value, ignoreCase: true, out var level) && Enum.IsDefined(level)
            ? level
            : PngCompressionLevel.Balanced;

    /// <summary>HDR capture is on unless the value is exactly "Off" (case-insensitive).</summary>
    public static bool IsHdrEnabled(string? value) => !string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase);

    /// <summary>Snap to one of <see cref="CountdownChoices"/>; anything else disables the countdown.</summary>
    public static int NormalizeCountdown(int seconds) => CountdownChoices.Contains(seconds) ? seconds : 0;

    public static int ClampQuality(int value) => Math.Clamp(value, 1, 100);

    /// <summary>The canonical spelling of a log level, or <c>Information</c>.</summary>
    public static string NormalizeLogLevel(string? value) =>
        LogLevels.FirstOrDefault(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase)) ?? "Information";

    /// <summary>The canonical spelling of a theme, or <c>System</c>.</summary>
    public static string NormalizeTheme(string? value) =>
        Themes.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase)) ?? "System";
}
