// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Profiles;
using CaptureImage.ViewModels.Dashboard;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Dashboard;

public sealed class DashboardViewModelTests : IDisposable
{
    private readonly FakeSettingsStore _store = new();
    private readonly CoordinatorFixture _fixture;
    private readonly IMessenger _messenger = new StrongReferenceMessenger();
    private readonly DashboardViewModel _vm;
    private List<GameTarget> _windows =
    [
        Target(1, "Elden Ring", "eldenring", steam: true),
        Target(2, "Notepad", "notepad"),
        Target(3, "Minimized game", "game", minimized: true),
    ];

    public DashboardViewModelTests()
    {
        _fixture = new CoordinatorFixture(_store);
        _fixture.Targets.EnumerateAsync(Arg.Any<CancellationToken>()).Returns(_ => (IReadOnlyList<GameTarget>)_windows);
        _vm = new DashboardViewModel(
            _fixture.Coordinator,
            _fixture.Targets,
            _fixture.Watcher,
            _store,
            new InlineDispatcher(),
            new KeyLocalization(),
            _messenger,
            NullLogger<DashboardViewModel>.Instance);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task Refresh_ListsWindowsWithBadges()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.Targets.Select(t => t.DisplayName).Should().Equal("Elden Ring", "Notepad", "Minimized game");
        _vm.Targets[0].IsSteamGame.Should().BeTrue();
        _vm.Targets[2].IsMinimized.Should().BeTrue();
        _vm.HasNoTargets.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_UpdatesItemsInPlace()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        var notepad = _vm.Targets[1];
        _vm.SelectedTarget = notepad;

        _windows = [Target(2, "Notepad — renamed", "notepad"), Target(4, "New window", "new")];
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.Targets.Should().HaveCount(2);
        _vm.Targets[0].Should().BeSameAs(notepad);
        notepad.DisplayName.Should().Be("Notepad — renamed");
        _vm.SelectedTarget.Should().BeSameAs(notepad);
    }

    [Fact]
    public async Task Search_FiltersByTitleOrProcess()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SearchText = "NOTE";
        _vm.Targets.Select(t => t.ProcessName).Should().Equal("notepad");

        _vm.SearchText = "eldenring";
        _vm.Targets.Select(t => t.DisplayName).Should().Equal("Elden Ring");

        _vm.SearchText = string.Empty;
        _vm.Targets.Should().HaveCount(3);
    }

    [Fact]
    public void WindowChanges_RefreshOnlyWhileThePageIsVisible()
    {
        _fixture.Watcher.WindowsChanged += Raise.Event();
        _fixture.Targets.DidNotReceive().EnumerateAsync(Arg.Any<CancellationToken>());

        _vm.IsPageActive = true;

        _fixture.Targets.Received(1).EnumerateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ToggleArm_ArmsAndDisarms()
    {
        _vm.ToggleArmCommand.Execute(null);
        _vm.IsArmed.Should().BeTrue();
        _vm.ArmButtonText.Should().Be("Dashboard_Disarm");
        _vm.StatusText.Should().Be("Status_Armed");
        _vm.Hotkeys.Should().OnlyContain(h => h.StatusText == "Hotkey_StatusActive");

        _vm.ToggleArmCommand.Execute(null);
        _vm.IsArmed.Should().BeFalse();
        _vm.StatusText.Should().Be("Status_Idle");
        _vm.Hotkeys.Should().OnlyContain(h => h.StatusText == "Hotkey_StatusInactive");
    }

    [Fact]
    public void HotkeyConflict_IsFlagged()
    {
        _fixture.Hotkeys.Activate(Arg.Any<HotkeySettings>()).Returns(
        [
            new HotkeyRegistration(CaptureMode.Window, HotkeyBinding.Default, HotkeyRegistrationStatus.Conflict),
            new HotkeyRegistration(CaptureMode.Region, HotkeyBinding.DefaultFor(CaptureMode.Region), HotkeyRegistrationStatus.Registered),
            new HotkeyRegistration(CaptureMode.Monitor, HotkeyBinding.DefaultFor(CaptureMode.Monitor), HotkeyRegistrationStatus.Registered),
        ]);

        _vm.ToggleArmCommand.Execute(null);

        _vm.HasHotkeyProblem.Should().BeTrue();
        _vm.Hotkeys.Single(h => h.Mode == CaptureMode.Window).IsProblem.Should().BeTrue();
    }

    [Fact]
    public async Task LockSelected_ShowsTheLockedTarget()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedTarget = _vm.Targets[0];

        _vm.LockSelectedCommand.Execute(null);

        _vm.LockedTargetName.Should().Be("Elden Ring");
        _vm.Targets[0].IsLocked.Should().BeTrue();

        _vm.UnlockCommand.Execute(null);
        _vm.LockedTargetName.Should().BeNull();
        _vm.Targets[0].IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProfile_AsksTheProfilesPage()
    {
        var created = new List<CreateProfileMessage>();
        var navigated = new List<NavigateMessage>();
        _messenger.Register<CreateProfileMessage>(this, (_, m) => created.Add(m));
        _messenger.Register<NavigateMessage>(this, (_, m) => navigated.Add(m));
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedTarget = _vm.Targets[0];

        _vm.CreateProfileCommand.Execute(null);

        created.Single().Target.WindowHandle.Should().Be(1);
        navigated.Single().PageKey.Should().Be(PageKeys.Profiles);
    }

    [Fact]
    public async Task ProfileBadge_FollowsTheSettings()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _store.Update(s => s with { Profiles = [new GameProfile { Name = "Elden", ExecutableName = "eldenring.exe" }] });

        _vm.Targets[0].HasProfile.Should().BeTrue();
        _vm.Targets[0].ProfileName.Should().Be("Elden");
        _vm.Targets[1].HasProfile.Should().BeFalse();
    }

    [Fact]
    public void Commands_ThatNeedASelection_AreDisabledWithout()
    {
        _vm.CaptureSelectedCommand.CanExecute(null).Should().BeFalse();
        _vm.LockSelectedCommand.CanExecute(null).Should().BeFalse();
        _vm.CreateProfileCommand.CanExecute(null).Should().BeFalse();
    }

    private static GameTarget Target(nint handle, string title, string process, bool steam = false, bool minimized = false) =>
        new(
            (uint)handle,
            handle,
            process,
            title,
            $@"C:\Games\{process}\{process}.exe",
            null,
            steam ? new SteamAppInfo(1245620, title, process) : null,
            minimized);
}
