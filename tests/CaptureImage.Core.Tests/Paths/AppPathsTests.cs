// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Paths;

/// <summary>Path layout and <c>--data-dir</c>; nothing here reads the environment variable.</summary>
public class AppPathsTests
{
    [Fact]
    public void FilesLiveInsideTheDataDirectory()
    {
        using var temp = new TempDirectory();

        var paths = new AppPaths(temp.FullPath);

        paths.DataDirectory.Should().Be(temp.FullPath);
        paths.SettingsFile.Should().Be(temp.Combine("settings.json"));
        paths.LogsDirectory.Should().Be(temp.Combine("logs"));
        paths.LibraryIndexFile.Should().Be(temp.Combine("library.json"));
        paths.ThumbnailCacheDirectory.Should().Be(temp.Combine("thumbnails"));
        paths.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public void RelativeDataDirectory_IsResolvedToAFullPath()
    {
        var paths = new AppPaths(Path.Combine("portable", "data"));

        paths.DataDirectory.Should().Be(Path.GetFullPath(Path.Combine("portable", "data")));
        Path.IsPathFullyQualified(paths.DataDirectory).Should().BeTrue();
    }

    [Fact]
    public void BlankDataDirectory_IsRejected()
    {
        FluentActions.Invoking(() => new AppPaths(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AppPaths(string.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AppPaths("   ")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnsureCreated_CreatesTheDataAndLogsFolders()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Combine("nested", "CaptureImage"));

        paths.EnsureCreated();
        paths.EnsureCreated();

        Directory.Exists(paths.DataDirectory).Should().BeTrue();
        Directory.Exists(paths.LogsDirectory).Should().BeTrue();
    }

    [Fact]
    public void Defaults_PointAtTheUsersFolders()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        AppPaths.DefaultDataDirectory.Should().Be(Path.Combine(localAppData, "poli0981", "CaptureImage"));
        AppPaths.LegacySettingsFile.Should().Be(Path.Combine(localAppData, "CaptureImage", "settings.json"));
        AppPaths.DefaultOutputDirectory.Should().Be(Path.Combine(pictures, "CaptureImage"));
        AppPaths.DataDirectoryVariable.Should().Be("CAPTUREIMAGE_DATA_DIR");
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--DATA-DIR")]
    public void FromCommandLine_DataDirFlag_OverridesTheLocation(string flag)
    {
        using var temp = new TempDirectory();

        var paths = AppPaths.FromCommandLine(["--minimized", flag, temp.FullPath, "--smoke-test"]);

        paths.DataDirectory.Should().Be(temp.FullPath);
        paths.IsOverridden.Should().BeTrue();
    }

    [Fact]
    public void FromCommandLine_FirstDataDirWins()
    {
        using var first = new TempDirectory();
        using var second = new TempDirectory();

        var paths = AppPaths.FromCommandLine(["--data-dir", first.FullPath, "--data-dir", second.FullPath]);

        paths.DataDirectory.Should().Be(first.FullPath);
    }

    [Fact]
    public void FromCommandLine_RelativeDataDir_IsResolved()
    {
        var paths = AppPaths.FromCommandLine(["--data-dir", "data"]);

        paths.DataDirectory.Should().Be(Path.GetFullPath("data"));
    }

    [Fact]
    public void FromCommandLine_Null_Throws()
    {
        FluentActions.Invoking(() => AppPaths.FromCommandLine(null!)).Should().Throw<ArgumentNullException>();
    }
}

/// <summary>
/// Tests that set <c>CAPTUREIMAGE_DATA_DIR</c>. They run on their own (no other test runs in
/// parallel) and always restore the variable's previous value.
/// </summary>
[Collection(EnvironmentVariableCollection.Name)]
public class AppPathsEnvironmentTests
{
    [Fact]
    public void EnvironmentVariable_OverridesTheDefault()
    {
        using var temp = new TempDirectory();

        var paths = WithDataDirVariable(temp.FullPath, () => AppPaths.FromCommandLine([]));

        paths.DataDirectory.Should().Be(temp.FullPath);
        paths.IsOverridden.Should().BeTrue();
    }

    [Fact]
    public void CommandLine_BeatsTheEnvironmentVariable()
    {
        using var fromVariable = new TempDirectory();
        using var fromFlag = new TempDirectory();

        var paths = WithDataDirVariable(fromVariable.FullPath, () => AppPaths.FromCommandLine(["--data-dir", fromFlag.FullPath]));

        paths.DataDirectory.Should().Be(fromFlag.FullPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void NoOverride_UsesTheDefaultDataDirectory(string? variable)
    {
        var paths = WithDataDirVariable(variable, () => AppPaths.FromCommandLine([]));

        paths.DataDirectory.Should().Be(AppPaths.DefaultDataDirectory);
        paths.IsOverridden.Should().BeFalse();
    }

    [Theory]
    [InlineData("--data-dir")]
    [InlineData("--data-dir", "")]
    [InlineData("--data-dir", "   ")]
    public void DataDirFlagWithoutAValue_IsIgnored(params string[] args)
    {
        using var temp = new TempDirectory();

        var paths = WithDataDirVariable(temp.FullPath, () => AppPaths.FromCommandLine(args));

        paths.DataDirectory.Should().Be(temp.FullPath, "the environment variable still applies");
    }

    private static AppPaths WithDataDirVariable(string? value, Func<AppPaths> resolve)
    {
        var original = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable);
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, value);
            return resolve();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, original);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentVariableCollection
{
    public const string Name = "Process environment variables";
}
