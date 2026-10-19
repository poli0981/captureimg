// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Profiles;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Profiles;

public class ProfileResolverTests
{
    private const uint EldenRingAppId = 1245620;

    private static readonly GameTarget SteamGame =
        TestTargets.Game(steam: new SteamAppInfo(EldenRingAppId, "ELDEN RING", "ELDEN RING"));

    private static GameProfile ByPath(string name, string path = TestTargets.GameExecutable) =>
        new() { Name = name, ExecutablePath = path };

    private static GameProfile ByName(string name, string executableName = "eldenring.exe") =>
        new() { Name = name, ExecutableName = executableName };

    private static GameProfile BySteam(string name, uint appId = EldenRingAppId) =>
        new() { Name = name, SteamAppId = appId };

    [Fact]
    public void ExecutablePath_BeatsNameAndSteamAppId_WhereverItIsListed()
    {
        GameProfile[] profiles = [BySteam("steam"), ByName("name"), ByPath("path")];

        ProfileResolver.Resolve(profiles, SteamGame)!.Name.Should().Be("path");
    }

    [Fact]
    public void ExecutableName_BeatsSteamAppId()
    {
        GameProfile[] profiles = [BySteam("steam"), ByName("name")];

        ProfileResolver.Resolve(profiles, SteamGame)!.Name.Should().Be("name");
    }

    [Fact]
    public void SteamAppId_MatchesWhenNothingMoreSpecificDoes()
    {
        GameProfile[] profiles = [ByName("other game", "other.exe"), BySteam("steam")];

        ProfileResolver.Resolve(profiles, SteamGame)!.Name.Should().Be("steam");
    }

    [Fact]
    public void SteamAppId_NeedsASteamTarget()
    {
        GameProfile[] profiles = [BySteam("steam")];

        ProfileResolver.Resolve(profiles, TestTargets.Game(steam: null)).Should().BeNull();
        ProfileResolver.Resolve(profiles, TestTargets.Game(steam: new SteamAppInfo(570, "Dota 2", "dota 2 beta"))).Should().BeNull();
    }

    [Theory]
    [InlineData(@"c:\games\elden ring\game\ELDENRING.EXE")]
    [InlineData("C:/Games/ELDEN RING/Game/eldenring.exe")]
    [InlineData(@"  C:\Games\ELDEN RING\Game\eldenring.exe  ")]
    public void ExecutablePath_IsMatchedCaseInsensitivelyAndNormalized(string profilePath)
    {
        ProfileResolver.Resolve([ByPath("path", profilePath)], SteamGame)!.Name.Should().Be("path");
    }

    [Theory]
    [InlineData("ELDENRING.EXE")]
    [InlineData(" eldenring.exe ")]
    public void ExecutableName_IsMatchedCaseInsensitivelyAndTrimmed(string executableName)
    {
        ProfileResolver.Resolve([ByName("name", executableName)], SteamGame)!.Name.Should().Be("name");
    }

    [Fact]
    public void ExecutableName_FallsBackToTheProcessName_WhenThePathIsUnknown()
    {
        var target = TestTargets.Game(processName: "eldenring", executablePath: string.Empty);

        ProfileResolver.Resolve([ByPath("path"), ByName("name")], target)!.Name.Should().Be("name");
    }

    [Fact]
    public void FirstMatchingProfile_WinsWithinTheSameRank()
    {
        GameProfile[] profiles = [ByName("first"), ByName("second"), BySteam("steam 1"), BySteam("steam 2")];

        ProfileResolver.Resolve(profiles, SteamGame)!.Name.Should().Be("first");
        ProfileResolver.Resolve(profiles[2..], SteamGame)!.Name.Should().Be("steam 1");
    }

    [Fact]
    public void DisabledProfiles_NeverMatch()
    {
        GameProfile[] profiles =
        [
            ByPath("disabled path") with { Enabled = false },
            ByName("disabled name") with { Enabled = false },
            BySteam("steam"),
        ];

        ProfileResolver.Resolve(profiles, SteamGame)!.Name.Should().Be("steam");
        ProfileResolver.Resolve(profiles[..2], SteamGame).Should().BeNull();
    }

    [Fact]
    public void ProfileWithoutCriteria_NeverMatches()
    {
        GameProfile[] profiles = [new() { Name = "empty" }, new() { Name = "blank", ExecutablePath = " ", ExecutableName = " " }];

        ProfileResolver.Resolve(profiles, SteamGame).Should().BeNull();
    }

    [Fact]
    public void NoTarget_OrNoProfiles_ResolvesNothing()
    {
        ProfileResolver.Resolve([ByName("name")], null).Should().BeNull();
        ProfileResolver.Resolve([], SteamGame).Should().BeNull();
    }

    [Fact]
    public void NullProfileList_Throws()
    {
        var act = () => ProfileResolver.Resolve(null!, SteamGame);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NewProfile_HasAStableUniqueIdAndSafeDefaults()
    {
        var a = new GameProfile();
        var b = new GameProfile();

        a.Id.Should().MatchRegex("^[0-9a-f]{32}$");
        a.Id.Should().NotBe(b.Id);
        a.Enabled.Should().BeTrue();
        a.AutoArm.Should().BeFalse("arming on focus is opt-in");
        a.LockTarget.Should().BeTrue();
        a.Format.Should().BeNull();
        a.HdrMode.Should().BeNull();
    }
}
