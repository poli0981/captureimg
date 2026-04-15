using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using CaptureImage.Core.Abstractions;
using CaptureImage.Infrastructure.Steam;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Steam;

public class SteamLibraryScannerTests
{
    private const string SteamRoot = @"C:\Program Files (x86)\Steam";
    private const string SecondaryLibrary = @"D:\SteamLibrary";

    private static MockFileSystem BuildMockFileSystem()
    {
        const string libraryFoldersVdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path" "C:\\Program Files (x86)\\Steam"
                    "apps" { "570" "100" }
                }
                "1"
                {
                    "path" "D:\\SteamLibrary"
                    "apps" { "730" "200" }
                }
            }
            """;

        const string dota2Manifest = """
            "AppState"
            {
                "appid" "570"
                "name" "Dota 2"
                "installdir" "dota 2 beta"
            }
            """;

        const string csgoManifest = """
            "AppState"
            {
                "appid" "730"
                "name" "Counter-Strike 2"
                "installdir" "Counter-Strike Global Offensive"
            }
            """;

        return new MockFileSystem(new Dictionary<string, MockFileData>
        {
            [$@"{SteamRoot}\steamapps\libraryfolders.vdf"] = new(libraryFoldersVdf),
            [$@"{SteamRoot}\steamapps\appmanifest_570.acf"] = new(dota2Manifest),
            [$@"{SecondaryLibrary}\steamapps\appmanifest_730.acf"] = new(csgoManifest),

            // Executables (content doesn't matter — existence of the folder does).
            [$@"{SteamRoot}\steamapps\common\dota 2 beta\game\bin\win64\dota2.exe"] = new("fake exe"),
            [$@"{SecondaryLibrary}\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe"] = new("fake exe"),
        });
    }

    private static SteamLibraryScanner BuildScanner(MockFileSystem fs, string? rootOverride = null)
    {
        var locator = Substitute.For<ISteamRootLocator>();
        locator.TryFindSteamRoot().Returns(rootOverride ?? SteamRoot);
        return new SteamLibraryScanner(fs, locator, NullLogger<SteamLibraryScanner>.Instance);
    }

    [Fact]
    public void Libraries_ReturnsEveryLibraryFromLibraryFoldersVdf()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var libs = scanner.Libraries;

        libs.Should().HaveCount(2);
        libs.Should().Contain(l => l.Path == SteamRoot);
        libs.Should().Contain(l => l.Path == SecondaryLibrary);
    }

    [Fact]
    public void Libraries_EachLibraryContainsItsOwnAppManifests()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var primary = System.Linq.Enumerable.First(scanner.Libraries, l => l.Path == SteamRoot);
        var secondary = System.Linq.Enumerable.First(scanner.Libraries, l => l.Path == SecondaryLibrary);

        primary.Apps.Should().ContainKey(570u);
        primary.Apps[570u].Name.Should().Be("Dota 2");
        primary.Apps[570u].InstallDir.Should().Be("dota 2 beta");

        secondary.Apps.Should().ContainKey(730u);
        secondary.Apps[730u].Name.Should().Be("Counter-Strike 2");
        secondary.Apps[730u].InstallDir.Should().Be("Counter-Strike Global Offensive");
    }

    [Fact]
    public void TryGetAppInfo_SteamInstalledExe_ReturnsSteamAppInfo()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var info = scanner.TryGetAppInfo(
            $@"{SteamRoot}\steamapps\common\dota 2 beta\game\bin\win64\dota2.exe");

        info.Should().NotBeNull();
        info!.AppId.Should().Be(570u);
        info.Name.Should().Be("Dota 2");
    }

    [Fact]
    public void TryGetAppInfo_ExeInSecondaryLibrary_ReturnsSteamAppInfo()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var info = scanner.TryGetAppInfo(
            $@"{SecondaryLibrary}\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe");

        info.Should().NotBeNull();
        info!.AppId.Should().Be(730u);
    }

    [Fact]
    public void TryGetAppInfo_NonSteamExe_ReturnsNull()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var info = scanner.TryGetAppInfo(@"C:\Windows\System32\notepad.exe");

        info.Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_EmptyPath_ReturnsNull()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        scanner.TryGetAppInfo(string.Empty).Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_IsCaseInsensitive()
    {
        var fs = BuildMockFileSystem();
        var scanner = BuildScanner(fs);

        var info = scanner.TryGetAppInfo(
            $@"{SteamRoot.ToUpperInvariant()}\STEAMAPPS\COMMON\DOTA 2 BETA\game\bin\win64\dota2.exe");

        info.Should().NotBeNull();
        info!.AppId.Should().Be(570u);
    }

    [Fact]
    public void Libraries_ReturnsEmpty_WhenSteamRootNotFound()
    {
        var fs = new MockFileSystem();
        var locator = Substitute.For<ISteamRootLocator>();
        locator.TryFindSteamRoot().Returns((string?)null);
        var scanner = new SteamLibraryScanner(fs, locator, NullLogger<SteamLibraryScanner>.Instance);

        scanner.Libraries.Should().BeEmpty();
        scanner.TryGetAppInfo(@"C:\any\path.exe").Should().BeNull();
    }

    [Fact]
    public void Libraries_ReturnsEmpty_WhenLibraryFoldersVdfMissing()
    {
        var fs = new MockFileSystem();
        fs.AddDirectory(SteamRoot);
        var scanner = BuildScanner(fs);

        scanner.Libraries.Should().BeEmpty();
    }
}
