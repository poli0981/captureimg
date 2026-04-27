namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Watches the system-wide foreground window and reports changes. Used by the dashboard
/// to auto-switch the selected target when the user Alt-Tabs to another app. Implementations
/// must filter out the host process's own windows and debounce rapid switches so the UI
/// only reacts after the user lands on a window.
/// </summary>
public interface IForegroundWindowWatcher : IDisposable
{
    /// <summary>
    /// Fires when the foreground window changes — after debouncing and self-process
    /// filtering. Subscribers may run on any thread; marshal to the UI thread before
    /// touching bindings.
    /// </summary>
    event EventHandler<nint>? ForegroundChanged;

    /// <summary><c>true</c> while the underlying hook is registered.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Register the foreground hook. Idempotent — calling while already running is a
    /// no-op. Must be called from a thread with a message pump (the WinUI 3 UI thread)
    /// because SetWinEventHook with WINEVENT_OUTOFCONTEXT routes callbacks via posted
    /// messages.
    /// </summary>
    void Start();

    /// <summary>Unhook and stop reporting events. Safe to call repeatedly.</summary>
    void Stop();
}
