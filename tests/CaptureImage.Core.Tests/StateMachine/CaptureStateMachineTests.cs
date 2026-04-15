using System;
using CaptureImage.Core.StateMachine;
using FluentAssertions;
using Stateless;
using Xunit;

namespace CaptureImage.Core.Tests.StateMachine;

public class CaptureStateMachineTests
{
    private static CaptureStateMachine NewMachine(bool previewEnabled = false) =>
        new(() => previewEnabled);

    [Fact]
    public void InitialState_IsIdle()
    {
        var sm = NewMachine();
        sm.CurrentState.Should().Be(CaptureState.Idle);
    }

    [Fact]
    public void HappyPath_WithoutPreview_ReachesCompleteThenArmed()
    {
        var sm = NewMachine(previewEnabled: false);

        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.FrameCaptured);
        sm.CurrentState.Should().Be(CaptureState.Saving);

        sm.Fire(CaptureTrigger.Saved);
        sm.CurrentState.Should().Be(CaptureState.Complete);

        sm.Fire(CaptureTrigger.Reset);
        sm.CurrentState.Should().Be(CaptureState.Armed);
    }

    [Fact]
    public void HappyPath_WithPreview_RoutesThroughPreviewingState()
    {
        var sm = NewMachine(previewEnabled: true);

        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.FrameCaptured);
        sm.CurrentState.Should().Be(CaptureState.Previewing);

        sm.Fire(CaptureTrigger.PreviewAccepted);
        sm.CurrentState.Should().Be(CaptureState.Saving);

        sm.Fire(CaptureTrigger.Saved);
        sm.CurrentState.Should().Be(CaptureState.Complete);
    }

    [Fact]
    public void PreviewRejected_DiscardsFrameAndReturnsToArmed()
    {
        var sm = NewMachine(previewEnabled: true);
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.FrameCaptured);

        sm.Fire(CaptureTrigger.PreviewRejected);

        sm.CurrentState.Should().Be(CaptureState.Armed);
    }

    [Fact]
    public void Disarm_ReturnsToTargetsSelected()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);

        sm.Fire(CaptureTrigger.Disarm);

        sm.CurrentState.Should().Be(CaptureState.TargetsSelected);
    }

    [Fact]
    public void ErrorDuringCapturing_TransitionsToFailed()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);

        sm.Fire(CaptureTrigger.ErrorOccurred);

        sm.CurrentState.Should().Be(CaptureState.Failed);
    }

    [Fact]
    public void ErrorDuringSaving_TransitionsToFailed()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.FrameCaptured);

        sm.Fire(CaptureTrigger.ErrorOccurred);

        sm.CurrentState.Should().Be(CaptureState.Failed);
    }

    [Fact]
    public void Reset_FromComplete_ReturnsToArmed()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.FrameCaptured);
        sm.Fire(CaptureTrigger.Saved);

        sm.Fire(CaptureTrigger.Reset);

        sm.CurrentState.Should().Be(CaptureState.Armed);
    }

    [Fact]
    public void Reset_FromFailed_ReturnsToArmed()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);
        sm.Fire(CaptureTrigger.HotkeyPressed);
        sm.Fire(CaptureTrigger.ErrorOccurred);

        sm.Fire(CaptureTrigger.Reset);

        sm.CurrentState.Should().Be(CaptureState.Armed);
    }

    [Fact]
    public void ClearTargets_FromArmed_ReturnsToIdle()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);
        sm.Fire(CaptureTrigger.Arm);

        sm.Fire(CaptureTrigger.ClearTargets);

        sm.CurrentState.Should().Be(CaptureState.Idle);
    }

    [Fact]
    public void IllegalTransition_ArmFromIdle_Throws()
    {
        var sm = NewMachine();

        var act = () => sm.Fire(CaptureTrigger.Arm);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IllegalTransition_HotkeyFromTargetsSelected_Throws()
    {
        var sm = NewMachine();
        sm.Fire(CaptureTrigger.SelectTargets);

        var act = () => sm.Fire(CaptureTrigger.HotkeyPressed);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StateChanged_FiresWithCorrectPayload()
    {
        var sm = NewMachine();
        CaptureStateChanged? captured = null;
        sm.StateChanged += (_, e) => captured = e;

        sm.Fire(CaptureTrigger.SelectTargets);

        captured.Should().NotBeNull();
        captured!.From.Should().Be(CaptureState.Idle);
        captured.To.Should().Be(CaptureState.TargetsSelected);
        captured.Trigger.Should().Be(CaptureTrigger.SelectTargets);
    }

    [Fact]
    public void CanFire_ReflectsPermittedTransitions()
    {
        var sm = NewMachine();
        sm.CanFire(CaptureTrigger.Arm).Should().BeFalse("Arm requires TargetsSelected first");

        sm.Fire(CaptureTrigger.SelectTargets);
        sm.CanFire(CaptureTrigger.Arm).Should().BeTrue();
        sm.CanFire(CaptureTrigger.HotkeyPressed).Should().BeFalse();
    }
}
