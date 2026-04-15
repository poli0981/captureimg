namespace CaptureImage.Core.StateMachine;

/// <summary>
/// Inputs the state machine accepts. Naming follows the plan's §5 trigger list verbatim.
/// </summary>
public enum CaptureTrigger
{
    /// <summary>User selected one or more targets.</summary>
    SelectTargets,

    /// <summary>User clicked Arm (or equivalent) — enable hotkey listener.</summary>
    Arm,

    /// <summary>User clicked Disarm — stop hotkey listener.</summary>
    Disarm,

    /// <summary>Hotkey fired while armed.</summary>
    HotkeyPressed,

    /// <summary>Engine delivered a frame successfully.</summary>
    FrameCaptured,

    /// <summary>User accepted the preview.</summary>
    PreviewAccepted,

    /// <summary>User rejected the preview — discard frame.</summary>
    PreviewRejected,

    /// <summary>File has been flushed to disk.</summary>
    Saved,

    /// <summary>Any error during capture or save.</summary>
    ErrorOccurred,

    /// <summary>Return from Complete / Failed to Armed for the next shot.</summary>
    Reset,

    /// <summary>Drop all targets (e.g. process ended). Goes back to Idle.</summary>
    ClearTargets,
}
