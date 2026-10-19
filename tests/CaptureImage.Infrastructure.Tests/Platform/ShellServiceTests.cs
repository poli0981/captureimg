// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Infrastructure.Platform;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Platform;

/// <summary>
/// <see cref="ShellService"/> refusals: inputs that must be rejected before anything is started
/// or deleted. (Opening real files, Explorer and the Recycle Bin would act on the user's
/// desktop, so the success paths are not exercised here.)
/// </summary>
public sealed class ShellServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ShellService _shell = new(NullLogger<ShellService>.Instance);

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPaths_AreRefused(string path)
    {
        _shell.Open(path).Should().BeFalse();
        _shell.RevealInExplorer(path).Should().BeFalse();
        _shell.MoveToRecycleBin(path).Should().BeFalse();
    }

    [Fact]
    public void MissingPaths_AreRefused()
    {
        var missingFile = _temp.Combine("missing-folder", "missing.png");

        _shell.Open(missingFile).Should().BeFalse();
        _shell.Open(_temp.Combine("missing-folder")).Should().BeFalse();
        _shell.RevealInExplorer(missingFile).Should().BeFalse("neither the file nor its folder exists");
        _shell.MoveToRecycleBin(missingFile).Should().BeFalse();
    }

    [Fact]
    public void MoveToRecycleBin_Folder_IsRefused()
    {
        var folder = _temp.CreateDirectory("not-a-file");

        _shell.MoveToRecycleBin(folder).Should().BeFalse();
        Directory.Exists(folder).Should().BeTrue();
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:privacy")]
    [InlineData("javascript:alert(1)")]
    [InlineData(@"\\server\share\tool.exe")]
    public void OpenUrl_OnlyAcceptsWebAndMailLinks(string url)
    {
        _shell.OpenUrl(new Uri(url)).Should().BeFalse();
    }

    [Fact]
    public void OpenUrl_Null_Throws()
    {
        _shell.Invoking(s => s.OpenUrl(null!)).Should().Throw<ArgumentNullException>();
    }
}
