using System;
using System.Collections.Generic;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Abstractions;

/// <summary>
/// Bounded ring buffer of recent log events, exposed to the log viewer VM. Implemented
/// by the Serilog <c>InMemorySink</c> in the Infrastructure project.
/// </summary>
public interface ILogBufferSource
{
    /// <summary>Return a copy of the current buffer contents in chronological order.</summary>
    IReadOnlyList<LogEntry> Snapshot();

    /// <summary>Raised when a new entry is added (unless <see cref="Paused"/> is true).</summary>
    event EventHandler<LogEntry>? Emitted;

    /// <summary>
    /// When <c>true</c>, new entries still land in the buffer but <see cref="Emitted"/>
    /// is suppressed so the viewer's list stops updating.
    /// </summary>
    bool Paused { get; set; }

    /// <summary>Empty the buffer.</summary>
    void Clear();
}
