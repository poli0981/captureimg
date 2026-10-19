// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Security;
using System.Text;
using AwesomeAssertions;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Steam;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Steam;

/// <summary>
/// <see cref="SteamLibraryScanner"/> against a fake Steam installation in a temp directory:
/// <c>steamapps\libraryfolders.vdf</c>, <c>appmanifest_&lt;id&gt;.acf</c> files and
/// <c>steamapps\common\&lt;installdir&gt;\…\game.exe</c>. The real Steam install is never read.
/// </summary>
public sealed class SteamLibraryScannerTests : IDisposable
{
    private static readonly SteamAppInfo Dota = new(570, "Dota 2", "dota 2 beta");
    private static readonly SteamAppInfo CounterStrike = new(730, "Counter-Strike 2", "Counter-Strike Global Offensive");

    private readonly TempDirectory _temp = new();
    private readonly string _steamRoot;
    private readonly string _secondLibrary;

    public SteamLibraryScannerTests()
    {
        _steamRoot = _temp.CreateDirectory("Steam");
        _secondLibrary = _temp.CreateDirectory("SteamLibrary");
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void TryGetAppInfo_ExecutableInTheSteamFolderLibrary_ReturnsTheApp()
    {
        WriteLibraryFolders(_steamRoot, _secondLibrary);
        var exe = InstallApp(_steamRoot, Dota);

        CreateScanner().TryGetAppInfo(exe).Should().Be(Dota);
    }

    [Fact]
    public void TryGetAppInfo_ExecutableInASecondaryLibrary_ReturnsTheApp()
    {
        WriteLibraryFolders(_steamRoot, _secondLibrary);
        InstallApp(_steamRoot, Dota);
        var exe = InstallApp(_secondLibrary, CounterStrike);

        CreateScanner().TryGetAppInfo(exe).Should().Be(CounterStrike);
    }

    [Fact]
    public void TryGetAppInfo_IgnoresCaseAndSlashDirection()
    {
        WriteLibraryFolders(_steamRoot);
        var exe = InstallApp(_steamRoot, Dota);
        var scanner = CreateScanner();

        scanner.TryGetAppInfo(exe.ToUpperInvariant()).Should().Be(Dota);
        scanner.TryGetAppInfo(exe.Replace('\\', '/')).Should().Be(Dota);
    }

    [Fact]
    public void TryGetAppInfo_ExecutableOutsideEveryLibrary_ReturnsNull()
    {
        WriteLibraryFolders(_steamRoot);
        InstallApp(_steamRoot, Dota);
        var scanner = CreateScanner();

        // Same folder name as the installed app, but not under steamapps\common.
        scanner.TryGetAppInfo(_temp.Combine("Games", "dota 2 beta", "game.exe")).Should().BeNull();
        scanner.TryGetAppInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")).Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_FolderUnderCommonWithoutAManifest_ReturnsNull()
    {
        WriteLibraryFolders(_steamRoot);
        InstallApp(_steamRoot, Dota);

        var info = CreateScanner().TryGetAppInfo(Path.Combine(_steamRoot, "steamapps", "common", "Uninstalled Game", "game.exe"));

        info.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\bad\0path\\game.exe")]
    public void TryGetAppInfo_BlankOrInvalidPath_ReturnsNull(string path)
    {
        WriteLibraryFolders(_steamRoot);
        InstallApp(_steamRoot, Dota);

        CreateScanner().TryGetAppInfo(path).Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_SteamNotInstalled_ReturnsNull()
    {
        var exe = InstallApp(_steamRoot, Dota);

        CreateScanner(() => null).TryGetAppInfo(exe).Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(SecurityException))]
    public void TryGetAppInfo_SteamFolderLookupFails_ReturnsNull(Type exceptionType)
    {
        var exe = InstallApp(_steamRoot, Dota);
        var scanner = CreateScanner(() => throw (Exception)Activator.CreateInstance(exceptionType, "lookup failed")!);

        scanner.TryGetAppInfo(exe).Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_LibraryFoldersFileMissing_FallsBackToTheSteamFolder()
    {
        var exe = InstallApp(_steamRoot, Dota);

        CreateScanner().TryGetAppInfo(exe).Should().Be(Dota);
    }

    [Fact]
    public void TryGetAppInfo_GarbledLibraryFoldersFile_FallsBackToTheSteamFolder()
    {
        WriteFile(Path.Combine(_steamRoot, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" ");
        var inSteamFolder = InstallApp(_steamRoot, Dota);
        var inOtherLibrary = InstallApp(_secondLibrary, CounterStrike);
        var scanner = CreateScanner();

        scanner.TryGetAppInfo(inSteamFolder).Should().Be(Dota);
        scanner.TryGetAppInfo(inOtherLibrary).Should().BeNull("the other library is only listed in the unreadable file");
    }

    [Fact]
    public void TryGetAppInfo_GarbledManifest_SkipsOnlyThatApp()
    {
        WriteLibraryFolders(_steamRoot);
        var good = InstallApp(_steamRoot, Dota);
        var bad = InstallApp(_steamRoot, CounterStrike);
        File.WriteAllText(Path.Combine(_steamRoot, "steamapps", "appmanifest_730.acf"), "\"AppState\" { \"appid\" \"730\" ");
        var scanner = CreateScanner();

        scanner.TryGetAppInfo(good).Should().Be(Dota);
        scanner.TryGetAppInfo(bad).Should().BeNull();
    }

    [Theory]
    [InlineData("\"AppState\" { \"name\" \"No id\" \"installdir\" \"Game\" }")]
    [InlineData("\"AppState\" { \"appid\" \"not-a-number\" \"name\" \"Bad id\" \"installdir\" \"Game\" }")]
    [InlineData("\"AppState\" { \"appid\" \"42\" \"name\" \"No folder\" }")]
    [InlineData("\"AppState\" { \"appid\" \"42\" \"name\" \"Empty folder\" \"installdir\" \"\" }")]
    [InlineData("\"SomethingElse\" { \"appid\" \"42\" \"installdir\" \"Game\" }")]
    [InlineData("")]
    public void TryGetAppInfo_IncompleteManifest_IsIgnored(string manifest)
    {
        WriteLibraryFolders(_steamRoot);
        WriteFile(Path.Combine(_steamRoot, "steamapps", "appmanifest_42.acf"), manifest);
        var exe = Path.Combine(_steamRoot, "steamapps", "common", "Game", "game.exe");
        WriteFile(exe, "MZ");

        CreateScanner().TryGetAppInfo(exe).Should().BeNull();
    }

    [Fact]
    public void TryGetAppInfo_ManifestWithoutName_ReturnsAnEmptyName()
    {
        WriteLibraryFolders(_steamRoot);
        var exe = InstallApp(_steamRoot, new SteamAppInfo(42, string.Empty, "Game"), includeName: false);

        CreateScanner().TryGetAppInfo(exe).Should().Be(new SteamAppInfo(42, string.Empty, "Game"));
    }

    [Fact]
    public void TryGetAppInfo_LibraryThatNoLongerExists_IsSkipped()
    {
        WriteLibraryFolders(_temp.Combine("UnpluggedDrive", "SteamLibrary"), _steamRoot);
        var exe = InstallApp(_steamRoot, Dota);

        CreateScanner().TryGetAppInfo(exe).Should().Be(Dota);
    }

    [Fact]
    public async Task TryGetAppInfo_ScansOnceAndCachesTheResult_EvenUnderConcurrency()
    {
        WriteLibraryFolders(_steamRoot);
        var exe = InstallApp(_steamRoot, Dota);
        var lookups = 0;
        var scanner = CreateScanner(() =>
        {
            Interlocked.Increment(ref lookups);
            return _steamRoot;
        });

        var results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => scanner.TryGetAppInfo(exe), TestContext.Current.CancellationToken)));

        results.Should().AllBeEquivalentTo(Dota);
        lookups.Should().Be(1);

        // Installed after the scan: not seen until the next start.
        var later = InstallApp(_steamRoot, CounterStrike);
        scanner.TryGetAppInfo(later).Should().BeNull();
    }

    // ---- Fake Steam installation -------------------------------------------------------

    private SteamLibraryScanner CreateScanner(Func<string?>? steamRoot = null) =>
        new(steamRoot ?? (() => _steamRoot), NullLogger<SteamLibraryScanner>.Instance);

    /// <summary>The current (2021+) <c>libraryfolders.vdf</c> layout.</summary>
    private void WriteLibraryFolders(params string[] libraries)
    {
        var vdf = new StringBuilder("\"libraryfolders\"\n{\n  \"contentstatsid\"  \"-4529013957193720112\"\n");
        for (var i = 0; i < libraries.Length; i++)
        {
            var path = libraries[i].Replace(@"\", @"\\", StringComparison.Ordinal);
            vdf.Append(CultureInfo.InvariantCulture, $"  \"{i}\"\n  {{\n    \"path\"  \"{path}\"\n")
                .Append("    \"label\"  \"\"\n    \"totalsize\"  \"0\"\n    \"apps\"\n    {\n    }\n  }\n");
        }
        vdf.Append("}\n");
        WriteFile(Path.Combine(_steamRoot, "steamapps", "libraryfolders.vdf"), vdf.ToString());
    }

    /// <summary>Writes the app manifest and a fake executable; returns the executable's path.</summary>
    private static string InstallApp(string library, SteamAppInfo app, bool includeName = true)
    {
        var manifest = new StringBuilder("\"AppState\"\n{\n")
            .Append(CultureInfo.InvariantCulture, $"  \"appid\"  \"{app.AppId}\"\n  \"Universe\"  \"1\"\n");
        if (includeName)
        {
            manifest.Append(CultureInfo.InvariantCulture, $"  \"name\"  \"{app.Name}\"\n");
        }
        manifest.Append(CultureInfo.InvariantCulture, $"  \"StateFlags\"  \"4\"\n  \"installdir\"  \"{app.InstallDir}\"\n}}\n");
        WriteFile(Path.Combine(library, "steamapps", string.Create(CultureInfo.InvariantCulture, $"appmanifest_{app.AppId}.acf")), manifest.ToString());

        var exe = Path.Combine(library, "steamapps", "common", app.InstallDir, "game", "bin", "win64", "game.exe");
        WriteFile(exe, "MZ");
        return exe;
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
