using Stateless;

namespace CaptureImage.Core.StateMachine;

/// <summary>
/// State machine for the capture pipeline. Thin wrapper over a <see cref="Stateless.StateMachine{TState, TTrigger}"/>
/// so the rest of the codebase doesn't need a <c>using Stateless</c>.
/// </summary>
/// <remarks>
/// <para>
/// The machine models the flow described in plan §5:
/// <c>Idle -> TargetsSelected -> Armed -> Capturing -> (Previewing?) -> Saving -> Complete -> Armed</c>
/// with <c>Failed</c> as the parallel error path.
/// </para>
/// <para>
/// Construct the machine with a <see cref="Func{TResult}"/> that reports whether the preview
/// gate is enabled — that decides whether <see cref="CaptureTrigger.FrameCaptured"/> routes
/// through <see cref="CaptureState.Previewing"/> or straight to <see cref="CaptureState.Saving"/>.
/// </para>
/// </remarks>
public sealed class CaptureStateMachine
{
    private readonly StateMachine<CaptureState, CaptureTrigger> _sm;
    private readonly Func<bool> _previewEnabled;

    public CaptureStateMachine(Func<bool> previewEnabled)
    {
        ArgumentNullException.ThrowIfNull(previewEnabled);
        _previewEnabled = previewEnabled;
        _sm = new StateMachine<CaptureState, CaptureTrigger>(CaptureState.Idle);
        Configure();
        _sm.OnTransitioned(t => StateChanged?.Invoke(this, new CaptureStateChanged(t.Source, t.Destination, t.Trigger)));
    }

    public CaptureState CurrentState => _sm.State;

    /// <summary>Raised after every successful transition. Thread affinity = whichever thread fired the trigger.</summary>
    public event EventHandler<CaptureStateChanged>? StateChanged;

    public bool CanFire(CaptureTrigger trigger) => _sm.CanFire(trigger);

    public void Fire(CaptureTrigger trigger) => _sm.Fire(trigger);

    /// <summary>Human-readable graph for logging and About-style diagnostic dumps.</summary>
    public override string ToString() => _sm.ToString();

    private void Configure()
    {
        _sm.Configure(CaptureState.Idle)
            .Permit(CaptureTrigger.SelectTargets, CaptureState.TargetsSelected);

        _sm.Configure(CaptureState.TargetsSelected)
            .Permit(CaptureTrigger.Arm, CaptureState.Armed)
            .Permit(CaptureTrigger.ClearTargets, CaptureState.Idle)
            .PermitReentry(CaptureTrigger.SelectTargets);

        _sm.Configure(CaptureState.Armed)
            .Permit(CaptureTrigger.Disarm, CaptureState.TargetsSelected)
            .Permit(CaptureTrigger.HotkeyPressed, CaptureState.Capturing)
            .Permit(CaptureTrigger.ClearTargets, CaptureState.Idle)
            .PermitReentry(CaptureTrigger.SelectTargets);

        _sm.Configure(CaptureState.Capturing)
            .PermitDynamic(CaptureTrigger.FrameCaptured,
                () => _previewEnabled() ? CaptureState.Previewing : CaptureState.Saving)
            .Permit(CaptureTrigger.ErrorOccurred, CaptureState.Failed);

        _sm.Configure(CaptureState.Previewing)
            .Permit(CaptureTrigger.PreviewAccepted, CaptureState.Saving)
            .Permit(CaptureTrigger.PreviewRejected, CaptureState.Armed);

        _sm.Configure(CaptureState.Saving)
            .Permit(CaptureTrigger.Saved, CaptureState.Complete)
            .Permit(CaptureTrigger.ErrorOccurred, CaptureState.Failed);

        _sm.Configure(CaptureState.Complete)
            .Permit(CaptureTrigger.Reset, CaptureState.Armed)
            .Permit(CaptureTrigger.ClearTargets, CaptureState.Idle);

        _sm.Configure(CaptureState.Failed)
            .Permit(CaptureTrigger.Reset, CaptureState.Armed)
            .Permit(CaptureTrigger.ClearTargets, CaptureState.Idle);
    }
}

/// <summary>Payload for <see cref="CaptureStateMachine.StateChanged"/>.</summary>
public sealed record CaptureStateChanged(CaptureState From, CaptureState To, CaptureTrigger Trigger);
