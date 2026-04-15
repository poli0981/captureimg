using System;
using System.Threading.Tasks;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Abstraction over the UI thread so portable VM/service code can marshal work
/// without depending on Avalonia directly. The UI project provides the real impl.
/// </summary>
public interface IUIThreadDispatcher
{
    /// <summary>True if the current thread is the UI thread.</summary>
    bool IsOnUIThread { get; }

    /// <summary>Fire-and-forget post to the UI thread.</summary>
    void Post(Action action);

    /// <summary>Await-able invoke on the UI thread.</summary>
    Task InvokeAsync(Action action);
}
