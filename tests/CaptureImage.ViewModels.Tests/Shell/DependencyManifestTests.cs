// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Shell;

/// <summary>
/// Dependency upgrades touch more than <c>Directory.Packages.props</c>: the third-party notices
/// (linked from the About page and shipped with the app) must list exactly the packages in the
/// app's lock file at the locked versions, and the <c>vpk</c> tool that packs a release must
/// match the Velopack library inside it.
/// </summary>
public sealed partial class DependencyManifestTests
{
    [Fact]
    public void ThirdPartyNotices_ListEveryShippedPackageAtItsLockedVersion()
    {
        var locked = LockedAppPackages();
        var listed = PackagesListedInNotices();

        locked.Should().NotBeEmpty();
        listed.Should().BeEquivalentTo(locked, "docs/legal/THIRD_PARTY_NOTICES.md must follow src/CaptureImage.App/packages.lock.json");
    }

    [Fact]
    public void VelopackLibrary_MatchesTheVpkTool()
    {
        var root = RepositoryRoot();
        var library = XDocument.Load(Path.Combine(root, "Directory.Packages.props")).Descendants("PackageVersion")
            .Single(e => (string?)e.Attribute("Include") == "Velopack")
            .Attribute("Version")!.Value;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".config", "dotnet-tools.json")));
        var tool = manifest.RootElement.GetProperty("tools").GetProperty("vpk").GetProperty("version").GetString();

        tool.Should().Be(library, "the release is packed by vpk and updated by the Velopack library, which must agree on the package format");
    }

    /// <summary>Package id → resolved version for everything the app's publish output contains.</summary>
    private static Dictionary<string, string> LockedAppPackages()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "CaptureImage.App", "packages.lock.json");
        using var lockFile = JsonDocument.Parse(File.ReadAllText(path));
        // One entry per framework plus one per framework/RID; the RID entry repeats packages with native assets.
        var graph = lockFile.RootElement.GetProperty("dependencies").EnumerateObject()
            .Single(framework => !framework.Name.Contains('/', StringComparison.Ordinal)).Value;
        return graph.EnumerateObject()
            .Where(package => package.Value.GetProperty("type").GetString() != "Project")
            .ToDictionary(package => package.Name, package => package.Value.GetProperty("resolved").GetString()!, StringComparer.Ordinal);
    }

    /// <summary>Package id → version from the notices' <c>| Package | Version | License | Project |</c> tables.</summary>
    private static Dictionary<string, string> PackagesListedInNotices()
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "legal", "THIRD_PARTY_NOTICES.md");
        return File.ReadLines(path)
            .Select(line => PackageRow().Match(line))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["id"].Value, match => match.Groups["version"].Value, StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CaptureImage.sln")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("CaptureImage.sln not found above the test output folder.");
    }

    [GeneratedRegex(@"^\| (?<id>[A-Za-z0-9][A-Za-z0-9.\-]*) \| (?<version>\d[^ |]*) \|")]
    private static partial Regex PackageRow();
}
