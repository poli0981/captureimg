namespace CaptureImage.Core.Models;

/// <summary>
/// Kind of process-lifecycle event raised by <see cref="Abstractions.IProcessWatcher"/>.
/// </summary>
public enum ProcessChangeKind
{
    Started,
    Stopped,
}

/// <summary>
/// Describes a process-lifecycle event. For deletions, only <see cref="ProcessId"/> is reliable;
/// <see cref="ProcessName"/> may be empty if the process is already gone by the time the event arrives.
/// </summary>
/// <param name="Kind">Whether this was a start or stop event.</param>
/// <param name="ProcessId">OS process ID.</param>
/// <param name="ProcessName">Short process name (file name), may be empty on stop.</param>
public sealed record ProcessChange(ProcessChangeKind Kind, uint ProcessId, string ProcessName);
