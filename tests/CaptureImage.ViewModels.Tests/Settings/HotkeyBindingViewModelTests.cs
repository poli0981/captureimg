// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Settings;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Settings;

public sealed class HotkeyBindingViewModelTests : IDisposable
{
    private const uint VkF9 = 0x78;
    private const uint VkG = 0x47;
    private const uint VkShift = 0x10;

    private readonly FakeSettingsStore _store = new();
    private readonly CoordinatorFixture _fixture;
    private readonly HotkeyBindingViewModel _window;

    public HotkeyBindingViewModelTests()
    {
        _fixture = new CoordinatorFixture(_store);
        _window = new HotkeyBindingViewModel(CaptureMode.Window, _store, _fixture.Coordinator, _fixture.Hotkeys, new KeyLocalization());
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Recording_ReleasesTheArmedHotkeys_AndAcceptingRestoresThem()
    {
        _fixture.Coordinator.Arm();
        _fixture.Hotkeys.ClearReceivedCalls();

        _window.StartRecordingCommand.Execute(null);
        _fixture.Hotkeys.Received(1).Deactivate();
        _window.DisplayText.Should().Be("Hotkey_PressKeys");

        var accepted = _window.Submit(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkF9);

        accepted.Should().BeTrue();
        _window.IsRecording.Should().BeFalse();
        _store.Current.Hotkeys.Window.Should().Be(new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkF9));
        _fixture.Hotkeys.Received().Activate(Arg.Is<HotkeySettings>(h => h.Window.VirtualKey == VkF9));
    }

    [Fact]
    public void ModifierOnlyPress_KeepsRecordingWithoutAMessage()
    {
        _window.StartRecordingCommand.Execute(null);

        _window.Submit(HotkeyModifiers.Shift, VkShift).Should().BeFalse();

        _window.IsRecording.Should().BeTrue();
        _window.HasError.Should().BeFalse();
    }

    [Fact]
    public void ReservedCombination_IsRejectedWithItsReason()
    {
        _window.StartRecordingCommand.Execute(null);

        _window.Submit(HotkeyModifiers.Win, VkG).Should().BeFalse();

        _window.Message.Should().Be("Hotkey_Validation_ReservedByWindows");
        _window.HasError.Should().BeTrue();
        _store.Current.Hotkeys.Window.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Window));
    }

    [Fact]
    public void CombinationOfAnotherMode_IsRejected()
    {
        _window.StartRecordingCommand.Execute(null);
        var region = HotkeyBinding.DefaultFor(CaptureMode.Region);

        _window.Submit(region.Modifiers, region.VirtualKey).Should().BeFalse();

        _window.Message.Should().Be("Hotkey_UsedByMode");
    }

    [Fact]
    public void CombinationOwnedByAnotherApp_IsRejected()
    {
        _fixture.Hotkeys.IsTakenByAnotherApp(Arg.Any<HotkeyBinding>()).Returns(true);
        _window.StartRecordingCommand.Execute(null);

        _window.Submit(HotkeyModifiers.Control, VkF9).Should().BeFalse();

        _window.Message.Should().Be("Hotkey_TakenByOtherApp");
        _window.IsRecording.Should().BeTrue();
    }

    [Fact]
    public void Clear_UnbindsTheMode()
    {
        _window.ClearCommand.Execute(null);

        _store.Current.Hotkeys.Window.IsUnbound.Should().BeTrue();
        _window.DisplayText.Should().Be("Hotkey_None");
    }

    [Fact]
    public void ResetToDefault_RestoresTheModeDefault()
    {
        _window.ClearCommand.Execute(null);

        _window.ResetToDefaultCommand.Execute(null);

        _store.Current.Hotkeys.Window.Should().Be(HotkeyBinding.DefaultFor(CaptureMode.Window));
        _window.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void CancelRecording_ResumesTheHotkeys()
    {
        _fixture.Coordinator.Arm();
        _window.StartRecordingCommand.Execute(null);
        _fixture.Hotkeys.ClearReceivedCalls();

        _window.CancelRecordingCommand.Execute(null);

        _fixture.Hotkeys.Received(1).Activate(Arg.Any<HotkeySettings>());
        _window.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void ConflictAfterArming_IsShown_AndClearedWhenRegistered()
    {
        _window.ApplyRegistration(new HotkeyRegistration(CaptureMode.Window, HotkeyBinding.Default, HotkeyRegistrationStatus.Conflict));
        _window.HasError.Should().BeTrue();
        _window.Message.Should().Be("Hotkey_StatusConflict");

        _window.ApplyRegistration(new HotkeyRegistration(CaptureMode.Window, HotkeyBinding.Default, HotkeyRegistrationStatus.Registered));
        _window.HasError.Should().BeFalse();
    }
}
