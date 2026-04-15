namespace CaptureImage.Core.Models;

/// <summary>
/// A process + window combination that the user can pick as a capture target.
/// </summary>
/// <param name="ProcessId">OS process ID.</param>
/// <param name="WindowHandle">
/// Native HWND (stored as <see cref="nint"/> so the Core assembly stays portable).
/// Infrastructure converts between <see cref="nint"/> and <c>IntPtr</c> at the boundary.
/// </param>
/// <param name="ProcessName">Short process name without extension (e.g. <c>notepad</c>).</param>
/// <param name="WindowTitle">Current window title as reported by <c>GetWindowText</c>.</param>
/// <param name="ExecutablePath">Absolute path to the executable, or empty if it could not be read.</param>
/// <param name="IconBytes">
/// PNG-encoded icon for the executable, or <c>null</c> if extraction failed.
/// Stored as bytes so this record can cross the <c>CaptureImage.UI</c> boundary without
/// pulling in Avalonia or System.Drawing types.
/// </param>
/// <param name="SteamInfo">Non-null if the executable lives under a Steam library folder.</param>
public sealed record GameTarget(
    uint ProcessId,
    nint WindowHandle,
    string ProcessName,
    string WindowTitle,
    string ExecutablePath,
    byte[]? IconBytes,
    SteamAppInfo? SteamInfo)
{
    /// <summary>Display label: prefer the window title, fall back to the process name.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(WindowTitle) ? ProcessName : WindowTitle;

    /// <summary>True when the executable lives under a Steam library folder.</summary>
    public bool IsSteamGame => SteamInfo is not null;
}
