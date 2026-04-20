using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Observes process create/destroy events so the Dashboard can stay live without polling.
/// Implementations raise <see cref="Changed"/> off the UI thread; subscribers are responsible
/// for marshalling to the UI via <see cref="IUIThreadDispatcher"/>.
/// </summary>
public interface IProcessWatcher : IDisposable
{
    /// <summary>
    /// Raised when a process starts or stops. May fire from an arbitrary thread.
    /// </summary>
    event EventHandler<ProcessChange>? Changed;

    /// <summary>Begin observing. Safe to call multiple times; subsequent calls are no-ops.</summary>
    void Start();

    /// <summary>Stop observing. Can be restarted with <see cref="Start"/>.</summary>
    void Stop();
}
