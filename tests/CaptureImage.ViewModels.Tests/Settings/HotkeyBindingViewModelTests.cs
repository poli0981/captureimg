using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Settings;

public class HotkeyBindingViewModelTests
{
    private static HotkeyBindingViewModel NewVm(
        FakeSettingsStore store,
        IHotkeyService? hotkeys = null,
        IHotkeyConflictSniffer? sniffer = null) =>
        new(
            store,
            hotkeys  ?? Substitute.For<IHotkeyService>(),
            sniffer  ?? Substitute.For<IHotkeyConflictSniffer>(),
            new NullLocalizationService(),
            NullLogger<HotkeyBindingViewModel>.Instance);

    [Fact]
    public void Constructor_SeedsCurrentBinding_FromSettings()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings { CaptureHotkey = new HotkeyBinding(HotkeyModifiers.Control, 0x7B) },
        };

        var vm = NewVm(store);

        vm.CurrentBinding.Should().Be(store.Current.CaptureHotkey);
        vm.IsRecording.Should().BeFalse();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.Ok);
        vm.ConflictDetected.Should().BeFalse();
    }

    [Fact]
    public void StartRecording_Flips_IsRecording()
    {
        var vm = NewVm(new FakeSettingsStore());

        vm.StartRecordingCommand.Execute(null);

        vm.IsRecording.Should().BeTrue();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.Ok);
        vm.ConflictDetected.Should().BeFalse();
    }

    [Fact]
    public void CancelRecording_ClearsState()
    {
        var vm = NewVm(new FakeSettingsStore());
        vm.StartRecordingCommand.Execute(null);
        vm.IsRecording.Should().BeTrue();

        vm.CancelRecordingCommand.Execute(null);

        vm.IsRecording.Should().BeFalse();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.Ok);
    }

    [Fact]
    public void TryCommitRecorded_NotRecording_IsIgnored()
    {
        var store = new FakeSettingsStore();
        var original = store.Current.CaptureHotkey;
        var vm = NewVm(store);

        vm.TryCommitRecorded(new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78));

        store.UpdateCallCount.Should().Be(0);
        vm.CurrentBinding.Should().Be(original);
    }

    [Fact]
    public void TryCommitRecorded_ValidNoConflict_PersistsAndClearsRecording()
    {
        var store = new FakeSettingsStore();
        var sniffer = Substitute.For<IHotkeyConflictSniffer>();
        sniffer.IsConflicted(Arg.Any<HotkeyBinding>()).Returns(false);
        var vm = NewVm(store, sniffer: sniffer);
        vm.StartRecordingCommand.Execute(null);

        var candidate = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78);
        vm.TryCommitRecorded(candidate);

        store.Current.CaptureHotkey.Should().Be(candidate);
        vm.CurrentBinding.Should().Be(candidate);
        vm.IsRecording.Should().BeFalse();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.Ok);
        vm.ConflictDetected.Should().BeFalse();
        sniffer.Received(1).IsConflicted(candidate);
    }

    [Fact]
    public void TryCommitRecorded_Conflict_SavesButFlagsWarning()
    {
        var store = new FakeSettingsStore();
        var sniffer = Substitute.For<IHotkeyConflictSniffer>();
        sniffer.IsConflicted(Arg.Any<HotkeyBinding>()).Returns(true);
        var vm = NewVm(store, sniffer: sniffer);
        vm.StartRecordingCommand.Execute(null);

        var candidate = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78);
        vm.TryCommitRecorded(candidate);

        vm.ConflictDetected.Should().BeTrue();
        vm.CurrentBinding.Should().Be(candidate);
        store.Current.CaptureHotkey.Should().Be(candidate);
        vm.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void TryCommitRecorded_Reserved_KeepsRecordingAndExposesError()
    {
        var store = new FakeSettingsStore();
        var original = store.Current.CaptureHotkey;
        var vm = NewVm(store);
        vm.StartRecordingCommand.Execute(null);

        // Win+L — reserved by the Windows shell
        vm.TryCommitRecorded(new HotkeyBinding(HotkeyModifiers.Win, 0x4C));

        vm.IsRecording.Should().BeTrue();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.ReservedByWindows);
        vm.HasError.Should().BeTrue();
        store.UpdateCallCount.Should().Be(0);
        store.Current.CaptureHotkey.Should().Be(original);
    }

    [Fact]
    public void TryCommitRecorded_ModifierOnly_KeepsRecording()
    {
        var store = new FakeSettingsStore();
        var vm = NewVm(store);
        vm.StartRecordingCommand.Execute(null);

        // VK_CONTROL as the "primary" — not a real combo
        vm.TryCommitRecorded(new HotkeyBinding(HotkeyModifiers.Control, 0x11));

        vm.IsRecording.Should().BeTrue();
        vm.LastValidationResult.Should().Be(HotkeyValidationResult.ModifierOnlyKey);
    }

    [Fact]
    public void ResetToDefault_PersistsDefaultBinding()
    {
        var store = new FakeSettingsStore
        {
            Current = new AppSettings { CaptureHotkey = new HotkeyBinding(HotkeyModifiers.Alt, 0x77) },
        };
        var vm = NewVm(store);

        vm.ResetToDefaultCommand.Execute(null);

        store.Current.CaptureHotkey.Should().Be(HotkeyBinding.Default);
        vm.CurrentBinding.Should().Be(HotkeyBinding.Default);
    }

    [Fact]
    public void ExternalSettingsChange_UpdatesCurrentBinding()
    {
        var store = new FakeSettingsStore();
        var vm = NewVm(store);

        var imported = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x75);
        store.Current = store.Current with { CaptureHotkey = imported };
        store.RaiseChanged();

        vm.CurrentBinding.Should().Be(imported);
    }

    [Fact]
    public void TryCommitRecorded_WhileHotkeyServiceRunning_LiveRebinds()
    {
        var store = new FakeSettingsStore();
        var hotkeys = Substitute.For<IHotkeyService>();
        // Service "running" means CurrentBinding is non-null.
        hotkeys.CurrentBinding.Returns(HotkeyBinding.Default);
        var vm = NewVm(store, hotkeys: hotkeys);
        vm.StartRecordingCommand.Execute(null);

        var candidate = new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78);
        vm.TryCommitRecorded(candidate);

        hotkeys.Received(1).SetBinding(candidate);
    }

    [Fact]
    public void TryCommitRecorded_WhileHotkeyServiceStopped_SkipsLiveRebind()
    {
        var store = new FakeSettingsStore();
        var hotkeys = Substitute.For<IHotkeyService>();
        hotkeys.CurrentBinding.Returns((HotkeyBinding?)null);
        var vm = NewVm(store, hotkeys: hotkeys);
        vm.StartRecordingCommand.Execute(null);

        vm.TryCommitRecorded(new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x78));

        hotkeys.DidNotReceive().SetBinding(Arg.Any<HotkeyBinding>());
    }

    // -- helpers --------------------------------------------------------------

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public AppSettings Current { get; set; } = new();
        public int UpdateCallCount { get; private set; }
        public event EventHandler? Changed;
        public string SettingsFilePath => "fake://settings.json";

        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportAsync(string p, CancellationToken ct = default) => Task.CompletedTask;
        public Task ImportAsync(string p, CancellationToken ct = default) => Task.CompletedTask;

        public void Update(Func<AppSettings, AppSettings> mutator)
        {
            UpdateCallCount++;
            Current = mutator(Current);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class NullLocalizationService : ILocalizationService
    {
        public CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public TextFlowDirection CurrentFlowDirection => TextFlowDirection.LeftToRight;
        public IReadOnlyList<CultureInfo> SupportedCultures { get; } = new[] { CultureInfo.InvariantCulture };
        public string this[string key] => string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
        public event EventHandler? CultureChanged { add { } remove { } }

        public void SetCulture(CultureInfo culture) { }
    }
}
