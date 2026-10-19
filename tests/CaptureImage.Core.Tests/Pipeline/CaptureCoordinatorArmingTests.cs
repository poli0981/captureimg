// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Profiles;
using CaptureImage.Core.Tests.TestSupport;
using NSubstitute;
using Xunit;

namespace CaptureImage.Core.Tests.Pipeline;

/// <summary>Arming, hotkey suspension, settings changes, auto-arm and disposal.</summary>
public sealed class CaptureCoordinatorArmingTests : IDisposable
{
    private static readonly HotkeyBinding AltF9 = new(HotkeyModifiers.Alt, 0x78);

    private readonly CoordinatorHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CaptureCoordinator Coordinator => _h.Coordinator;

    // ---- Arm / Disarm ---------------------------------------------------------------------

    [Fact]
    public void Arm_ActivatesTheConfiguredHotkeys_AndReportsTheRegistrations()
    {
        var stateChanges = 0;
        Coordinator.StateChanged += (_, _) => stateChanges++;

        var registrations = Coordinator.Arm();

        _h.Hotkeys.Received(1).Activate(_h.CurrentSettings.Hotkeys);
        registrations.Should().Equal(CoordinatorHarness.RegistrationsFor(_h.CurrentSettings.Hotkeys));
        Coordinator.Registrations.Should().BeSameAs(registrations);
        Coordinator.IsArmed.Should().BeTrue();
        stateChanges.Should().Be(1);
    }

    [Fact]
    public void Arm_PassesConflictsThrough()
    {
        var hotkeys = _h.CurrentSettings.Hotkeys;
        IReadOnlyList<HotkeyRegistration> reported =
        [
            new(CaptureMode.Window, hotkeys.Window, HotkeyRegistrationStatus.Registered),
            new(CaptureMode.Region, hotkeys.Region, HotkeyRegistrationStatus.Conflict),
            new(CaptureMode.Monitor, hotkeys.Monitor, HotkeyRegistrationStatus.Registered),
        ];
        _h.Hotkeys.Activate(Arg.Any<HotkeySettings>()).Returns(reported);

        Coordinator.Arm().Should().BeSameAs(reported);
        Coordinator.Registrations.Should().Contain(r => r.Mode == CaptureMode.Region && r.Status == HotkeyRegistrationStatus.Conflict);
        Coordinator.IsArmed.Should().BeTrue("a conflict on one mode does not disarm the others");
    }

    [Fact]
    public void Disarm_DeactivatesTheHotkeys_AndForgetsTheRegistrations()
    {
        Coordinator.Arm();
        var stateChanges = 0;
        Coordinator.StateChanged += (_, _) => stateChanges++;

        Coordinator.Disarm();

        _h.Hotkeys.Received(1).Deactivate();
        Coordinator.IsArmed.Should().BeFalse();
        Coordinator.Registrations.Should().BeEmpty();
        stateChanges.Should().Be(1);
    }

    // ---- SuspendHotkeys ---------------------------------------------------------------------

    [Fact]
    public void SuspendHotkeys_WhileArmed_ReleasesThem_AndDisposingReArms()
    {
        Coordinator.Arm();
        _h.Hotkeys.ClearReceivedCalls();

        var suspension = Coordinator.SuspendHotkeys();

        _h.Hotkeys.Received(1).Deactivate();
        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        Coordinator.IsArmed.Should().BeTrue("the suspension is temporary");
        Coordinator.Registrations.Should().BeEmpty();

        suspension.Dispose();

        _h.Hotkeys.Received(1).Activate(_h.CurrentSettings.Hotkeys);
        Coordinator.Registrations.Should().HaveCount(3);
        Coordinator.IsArmed.Should().BeTrue();

        suspension.Dispose();
        _h.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
    }

    [Fact]
    public void SuspendHotkeys_Nested_ReArmsOnlyWhenTheLastSuspensionEnds()
    {
        Coordinator.Arm();
        _h.Hotkeys.ClearReceivedCalls();

        var outer = Coordinator.SuspendHotkeys();
        var inner = Coordinator.SuspendHotkeys();
        _h.Hotkeys.Received(1).Deactivate();

        inner.Dispose();
        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        Coordinator.Registrations.Should().BeEmpty();

        outer.Dispose();
        _h.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
        Coordinator.Registrations.Should().NotBeEmpty();
    }

    [Fact]
    public void SuspendHotkeys_WhileDisarmed_ChangesNothing()
    {
        using (Coordinator.SuspendHotkeys())
        {
            _h.Hotkeys.DidNotReceive().Deactivate();
        }

        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        Coordinator.IsArmed.Should().BeFalse();
    }

    [Fact]
    public void Arm_WhileSuspended_RegistersWhenTheSuspensionEnds()
    {
        var suspension = Coordinator.SuspendHotkeys();

        Coordinator.Arm().Should().BeEmpty();

        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        Coordinator.IsArmed.Should().BeTrue();

        suspension.Dispose();

        _h.Hotkeys.Received(1).Activate(_h.CurrentSettings.Hotkeys);
        Coordinator.Registrations.Should().HaveCount(3);
    }

    [Fact]
    public void Disarm_WhileSuspended_StaysDisarmedAfterwards()
    {
        Coordinator.Arm();
        var suspension = Coordinator.SuspendHotkeys();

        Coordinator.Disarm();
        suspension.Dispose();

        _h.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
        Coordinator.IsArmed.Should().BeFalse();
    }

    // ---- settings changes ---------------------------------------------------------------------

    [Fact]
    public void HotkeyChange_WhileArmed_ReRegistersTheNewBindings()
    {
        Coordinator.Arm();
        var updated = _h.CurrentSettings.Hotkeys.With(CaptureMode.Region, AltF9);

        _h.UpdateSettings(s => s with { Hotkeys = updated });

        _h.Hotkeys.Received(1).Activate(updated);
        Coordinator.Registrations.Should().Contain(r => r.Mode == CaptureMode.Region && r.Binding == AltF9);
    }

    [Fact]
    public void OtherSettingsChange_WhileArmed_DoesNotReRegister()
    {
        Coordinator.Arm();

        _h.UpdateSettings(s => s with { Theme = "Dark", UI = s.UI with { SoundEnabled = false } });

        _h.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
    }

    [Fact]
    public void HotkeyChange_WhileDisarmed_RegistersNothing()
    {
        _h.UpdateSettings(s => s with { Hotkeys = s.Hotkeys.With(CaptureMode.Window, AltF9) });

        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        Coordinator.IsArmed.Should().BeFalse();
    }

    // ---- auto-arm -----------------------------------------------------------------------------

    private void UseProfiles(params GameProfile[] profiles) =>
        _h.UpdateSettings(s => s with { Profiles = profiles }, raiseChanged: false);

    private void BringToFront(nint window) =>
        _h.Windows.ForegroundChanged += Raise.Event<EventHandler<nint>>(_h.Windows, window);

    private static GameProfile AutoArmProfile(bool lockTarget = true) =>
        new() { Name = "Elden Ring", ExecutableName = "eldenring.exe", AutoArm = true, LockTarget = lockTarget };

    [Fact]
    public void AutoArm_GameWithAnAutoArmProfileComesToTheFront_ArmsAndLocksIt()
    {
        UseProfiles(AutoArmProfile());

        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeTrue();
        _h.Hotkeys.Received(1).Activate(_h.CurrentSettings.Hotkeys);
        Coordinator.LockedTarget.Should().Be(_h.Target);
        _h.Toasts.Received(1).Show(ToastKind.Info, "L:Toast_AutoArmed", "Elden Ring", null);
    }

    [Fact]
    public void AutoArm_WithoutLockTarget_ArmsButKeepsFollowingTheForeground()
    {
        UseProfiles(AutoArmProfile(lockTarget: false));

        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeTrue();
        Coordinator.LockedTarget.Should().BeNull();
    }

    [Fact]
    public void AutoArm_AlreadyArmed_OnlyLocksTheTarget()
    {
        UseProfiles(AutoArmProfile());
        Coordinator.Arm();

        BringToFront(CoordinatorHarness.GameWindow);

        _h.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
        _h.Toasts.DidNotReceive().Show(Arg.Any<ToastKind>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
        Coordinator.LockedTarget.Should().Be(_h.Target);
    }

    [Fact]
    public void AutoArm_WithNotificationsOff_ArmsSilently()
    {
        UseProfiles(AutoArmProfile());
        _h.UpdateUi(ui => ui with { ShowNotifications = false });

        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeTrue();
        _h.Toasts.DidNotReceive().Show(Arg.Any<ToastKind>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public void AutoArm_ProfileWithoutAutoArm_DoesNothing()
    {
        UseProfiles(AutoArmProfile() with { AutoArm = false }, new GameProfile { Name = "Other", ExecutableName = "other.exe", AutoArm = true });

        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeFalse();
        Coordinator.LockedTarget.Should().BeNull();
    }

    [Fact]
    public void AutoArm_DisabledProfile_DoesNothing()
    {
        UseProfiles(AutoArmProfile() with { Enabled = false });

        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeFalse();
        _ = _h.Targets.DidNotReceive().GetTargetAsync(Arg.Any<nint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AutoArm_WithoutAutoArmProfiles_DoesNotEvenLookUpTheWindow()
    {
        BringToFront(CoordinatorHarness.GameWindow);

        Coordinator.IsArmed.Should().BeFalse();
        _ = _h.Targets.DidNotReceive().GetTargetAsync(Arg.Any<nint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AutoArm_NoForegroundWindow_IsIgnored()
    {
        UseProfiles(AutoArmProfile());

        BringToFront(0);

        Coordinator.IsArmed.Should().BeFalse();
    }

    [Fact]
    public void AutoArm_WindowThatVanished_IsIgnored()
    {
        UseProfiles(AutoArmProfile());
        _h.Targets.GetTargetAsync(0x7777, Arg.Any<CancellationToken>()).Returns((GameTarget?)null);

        BringToFront(0x7777);

        Coordinator.IsArmed.Should().BeFalse();
    }

    [Fact]
    public void AutoArm_FailingLookup_IsSwallowed()
    {
        UseProfiles(AutoArmProfile());
        _h.Targets.GetTargetAsync(CoordinatorHarness.GameWindow, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GameTarget?>(new InvalidOperationException("window enumeration failed")));

        var act = () => BringToFront(CoordinatorHarness.GameWindow);

        act.Should().NotThrow();
        Coordinator.IsArmed.Should().BeFalse();
    }

    // ---- LockTarget -----------------------------------------------------------------------------

    [Fact]
    public void LockTarget_RaisesStateChangedOnlyWhenTheTargetChanges()
    {
        var stateChanges = 0;
        Coordinator.StateChanged += (_, _) => stateChanges++;
        var target = TestTargets.Game(windowHandle: 0x2002);

        Coordinator.LockTarget(target);
        Coordinator.LockTarget(target with { ProcessId = 1 });
        Coordinator.LockTarget(target with { WindowTitle = "Renamed" });
        Coordinator.LockTarget(null);
        Coordinator.LockTarget(null);

        stateChanges.Should().Be(3);
        Coordinator.LockedTarget.Should().BeNull();
    }

    // ---- Dispose ----------------------------------------------------------------------------------

    [Fact]
    public async Task Dispose_ReleasesTheHotkeys_AndStopsReactingToEvents()
    {
        UseProfiles(AutoArmProfile());
        Coordinator.Arm();
        _h.Hotkeys.ClearReceivedCalls();

        Coordinator.Dispose();

        _h.Hotkeys.Received(1).Deactivate();
        (await Coordinator.CaptureAsync(CaptureMode.Window, Ct)).Kind.Should().Be(CaptureOutcomeKind.Busy);
        FluentActions.Invoking(() => Coordinator.Arm()).Should().Throw<ObjectDisposedException>();

        Coordinator.Disarm();
        _h.Hotkeys.Triggered += Raise.Event<EventHandler<CaptureMode>>(_h.Hotkeys, CaptureMode.Window);
        BringToFront(CoordinatorHarness.GameWindow);
        _h.UpdateSettings(s => s with { Hotkeys = s.Hotkeys.With(CaptureMode.Window, AltF9) });
        Coordinator.Dispose();

        _h.Hotkeys.Received(1).Deactivate();
        _h.Hotkeys.DidNotReceive().Activate(Arg.Any<HotkeySettings>());
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
        _h.Completed.Should().BeEmpty();
    }
}
