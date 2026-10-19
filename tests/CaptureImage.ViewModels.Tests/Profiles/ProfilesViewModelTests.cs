// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Profiles;
using CaptureImage.ViewModels.Profiles;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.Messaging;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Profiles;

public sealed class ProfilesViewModelTests : IDisposable
{
    private readonly FakeSettingsStore _store = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IMessenger _messenger = new StrongReferenceMessenger();
    private readonly ProfilesViewModel _vm;

    public ProfilesViewModelTests()
    {
        _vm = new ProfilesViewModel(_store, _dialogs, new InlineDispatcher(), new KeyLocalization(), _messenger);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void Add_CreatesAndSelectsAProfile()
    {
        _vm.AddCommand.Execute(null);

        _store.Current.Profiles.Should().ContainSingle().Which.Name.Should().Be("Profiles_NewProfile");
        _vm.SelectedProfile.Should().NotBeNull();
        _vm.MatchesNothing.Should().BeTrue();
    }

    [Fact]
    public void Editing_WritesTheSelectedProfile()
    {
        _vm.AddCommand.Execute(null);

        _vm.Name = "Elden Ring";
        _vm.ExecutableName = "eldenring.exe";
        _vm.Format = nameof(ImageFormat.Webp);
        _vm.HdrMode = "Off";
        _vm.AutoArm = true;

        var profile = _store.Current.Profiles.Single();
        profile.Name.Should().Be("Elden Ring");
        profile.ExecutableName.Should().Be("eldenring.exe");
        profile.Format.Should().Be(ImageFormat.Webp);
        profile.HdrMode.Should().Be("Off");
        profile.AutoArm.Should().BeTrue();
        _vm.MatchesNothing.Should().BeFalse();
        _vm.Profiles.Single().Name.Should().Be("Elden Ring");
    }

    [Fact]
    public void ClearingAnOverride_FallsBackToTheGlobalSetting()
    {
        _vm.AddCommand.Execute(null);
        _vm.Format = nameof(ImageFormat.Png);
        _vm.OutputDirectory = @"D:\Shots";

        _vm.Format = string.Empty;
        _vm.ClearOutputFolderCommand.Execute(null);

        var profile = _store.Current.Profiles.Single();
        profile.Format.Should().BeNull();
        profile.OutputDirectory.Should().BeNull();
    }

    [Fact]
    public void InvalidSteamAppId_IsFlaggedAndNotSaved()
    {
        _vm.AddCommand.Execute(null);

        _vm.SteamAppId = "12ab";

        _vm.SteamAppIdInvalid.Should().BeTrue();
        _store.Current.Profiles.Single().SteamAppId.Should().BeNull();

        _vm.SteamAppId = "1245620";
        _vm.SteamAppIdInvalid.Should().BeFalse();
        _store.Current.Profiles.Single().SteamAppId.Should().Be(1245620u);
    }

    [Fact]
    public void CreateProfileMessage_PrefillsFromTheWindow()
    {
        var target = new GameTarget(10, 10, "eldenring", "ELDEN RING™", @"C:\Steam\common\ELDEN RING\Game\eldenring.exe", null,
            new SteamAppInfo(1245620, "ELDEN RING", "ELDEN RING"));

        _messenger.Send(new CreateProfileMessage(target));

        var profile = _store.Current.Profiles.Single();
        profile.Name.Should().Be("ELDEN RING");
        profile.ExecutablePath.Should().Be(target.ExecutablePath);
        profile.ExecutableName.Should().Be("eldenring.exe");
        profile.SteamAppId.Should().Be(1245620u);
        _vm.SelectedProfile!.Id.Should().Be(profile.Id);
    }

    [Fact]
    public void CreateProfileMessage_SelectsAnExistingMatch()
    {
        _store.Update(s => s with { Profiles = [new GameProfile { Name = "Existing", ExecutableName = "notepad.exe" }] });
        var target = new GameTarget(5, 5, "notepad", "Notepad", @"C:\Windows\notepad.exe", null, null);

        _messenger.Send(new CreateProfileMessage(target));

        _store.Current.Profiles.Should().ContainSingle();
        _vm.SelectedProfile!.Name.Should().Be("Existing");
    }

    [Fact]
    public async Task Delete_AsksFirst()
    {
        _vm.AddCommand.Execute(null);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await _vm.DeleteCommand.ExecuteAsync(null);
        _store.Current.Profiles.Should().ContainSingle();

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await _vm.DeleteCommand.ExecuteAsync(null);
        _store.Current.Profiles.Should().BeEmpty();
        _vm.SelectedProfile.Should().BeNull();
        _vm.HasProfiles.Should().BeFalse();
    }

    [Fact]
    public async Task BrowseExecutable_FillsPathNameAndAName()
    {
        _vm.AddCommand.Execute(null);
        _dialogs.PickFileAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(@"D:\Games\Hades\Hades.exe");

        await _vm.BrowseExecutableCommand.ExecuteAsync(null);

        var profile = _store.Current.Profiles.Single();
        profile.ExecutablePath.Should().Be(@"D:\Games\Hades\Hades.exe");
        profile.ExecutableName.Should().Be("Hades.exe");
        profile.Name.Should().Be("Hades");
    }

    [Fact]
    public void ExternalChange_ReloadsAndKeepsTheSelection()
    {
        _vm.AddCommand.Execute(null);
        var id = _vm.SelectedProfile!.Id;

        _store.ReplaceExternally(_store.Current with
        {
            Profiles = [.. _store.Current.Profiles, new GameProfile { Name = "Other", ExecutableName = "other.exe" }],
        });

        _vm.Profiles.Should().HaveCount(2);
        _vm.SelectedProfile!.Id.Should().Be(id);
    }
}
