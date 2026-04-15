namespace CaptureImage.Core.Models;

/// <summary>
/// Persisted user settings for CaptureImage. Written to
/// <c>%LocalAppData%\CaptureImage\settings.json</c> via <c>JsonSettingsStore</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Versioning:</b> Never renumber fields. When the schema changes, bump
/// <see cref="Version"/> and add a migration step in the store. Never remove a field
/// without a deprecation period — unknown fields are ignored on load but must still round-trip.
/// </para>
/// <para>
/// <b>Source-gen:</b> This type is serialized via <see cref="System.Text.Json"/> source
/// generators (see <c>SettingsJsonContext</c>). Keep properties simple: records, primitives,
/// strings, enums. No collections of reference types unless they also have generated metadata.
/// </para>
/// </remarks>
public sealed record AppSettings
{
    /// <summary>Schema version. Bump when adding migration steps.</summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// IETF BCP-47 culture code for UI (e.g. <c>en-US</c>, <c>vi-VN</c>, <c>ar-SA</c>).
    /// </summary>
    public string Culture { get; init; } = "en-US";

    /// <summary>Theme preference: <c>Light</c>, <c>Dark</c>, or <c>System</c>.</summary>
    public string Theme { get; init; } = "System";

    public HotkeyBinding CaptureHotkey { get; init; } = HotkeyBinding.Default;

    public CaptureSettings Capture { get; init; } = new();

    public UiSettings UI { get; init; } = new();
}

/// <summary>Capture-specific persisted options.</summary>
public sealed record CaptureSettings
{
    public ImageFormat DefaultFormat { get; init; } = ImageFormat.Png;

    /// <summary>JPEG quality 1-100. Ignored for non-JPEG formats.</summary>
    public int JpegQuality { get; init; } = 90;

    /// <summary>WebP quality 1-100. Ignored for non-WebP formats.</summary>
    public int WebpQuality { get; init; } = 85;

    /// <summary>
    /// Absolute output directory. Empty string means "use the default under
    /// <c>%USERPROFILE%\Pictures\CaptureImage</c>" — resolved at load time.
    /// </summary>
    public string OutputDirectory { get; init; } = string.Empty;

    public string FileNameTemplate { get; init; } = "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}";

    /// <summary>
    /// When <c>true</c>, the user must approve each capture via the preview dialog before it
    /// lands on disk. When <c>false</c>, captures are saved immediately.
    /// </summary>
    public bool PreviewBeforeSave { get; init; } = false;
}

/// <summary>UI / behaviour preferences not tied to a specific tab.</summary>
public sealed record UiSettings
{
    public bool StartMinimized { get; init; } = false;

    public bool MinimizeToTray { get; init; } = true;

    public bool ShowLogViewer { get; init; } = false;

    public bool SoundEnabled { get; init; } = true;
}
