using System;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Owns the system tray icon and its context menu. Lifecycle:
/// <list type="number">
///   <item>App startup calls <see cref="Initialize"/> once the main window exists.</item>
///   <item>Menu items post actions back to the main window (Show / Arm / Exit).</item>
///   <item>App shutdown disposes the host.</item>
/// </list>
/// </summary>
public interface ITrayIconHost : IDisposable
{
    /// <summary>
    /// Attach the tray icon to the application. The host takes a weak reference to the
    /// main window so close-to-tray works.
    /// </summary>
    void Initialize(object mainWindow);
}
