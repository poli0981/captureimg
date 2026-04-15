namespace CaptureImage.Core.StateMachine;

/// <summary>
/// Discrete states of the capture pipeline. Order matches the plan's state diagram so the
/// two stay in sync — see section §5 of the approved plan.
/// </summary>
public enum CaptureState
{
    /// <summary>No targets selected.</summary>
    Idle,

    /// <summary>At least one target is selected, but the pipeline is not armed yet.</summary>
    TargetsSelected,

    /// <summary>Hotkey listener is active and waiting for a press.</summary>
    Armed,

    /// <summary>Hotkey received; engine is running the capture.</summary>
    Capturing,

    /// <summary>Frame captured and held in memory waiting for user accept/reject.</summary>
    Previewing,

    /// <summary>Frame accepted, encoder + file write in progress.</summary>
    Saving,

    /// <summary>Terminal success state — transitions back to <see cref="Armed"/>.</summary>
    Complete,

    /// <summary>Terminal failure state — transitions back to <see cref="Armed"/>.</summary>
    Failed,
}
