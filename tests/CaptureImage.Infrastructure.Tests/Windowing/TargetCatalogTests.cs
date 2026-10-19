// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Windowing;

/// <summary>
/// <see cref="TargetCatalog"/> on the real desktop. The set of windows differs per machine, so
/// these tests check invariants of whatever is listed rather than specific windows.
/// </summary>
public sealed class TargetCatalogTests
{
    private static readonly SteamAppInfo FakeSteamApp = new(42, "Fake", "fake");

    private readonly ISteamDetector _steam = Substitute.For<ISteamDetector>();
    private readonly TargetCatalog _catalog;

    public TargetCatalogTests()
    {
        _steam.TryGetAppInfo(Arg.Any<string>()).Returns(FakeSteamApp);
        _catalog = new TargetCatalog(new IconExtractor(NullLogger<IconExtractor>.Instance), _steam, NullLogger<TargetCatalog>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnumerateAsync_ListsOnlyTitledWindowsOfOtherProcesses()
    {
        var targets = await _catalog.EnumerateAsync(Ct);

        targets.Should().OnlyContain(t => t.ProcessId != (uint)Environment.ProcessId, "the app never offers its own windows");
        targets.Should().OnlyContain(t => t.WindowHandle != 0 && !string.IsNullOrWhiteSpace(t.WindowTitle) && !string.IsNullOrEmpty(t.ProcessName));
        targets.Select(t => t.WindowHandle).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task EnumerateAsync_AsksTheSteamDetectorAboutEachExecutable()
    {
        var targets = await _catalog.EnumerateAsync(Ct);

        foreach (var target in targets.Where(t => t.ExecutablePath.Length > 0))
        {
            target.SteamInfo.Should().Be(FakeSteamApp);
            target.IsSteamGame.Should().BeTrue();
            _steam.Received().TryGetAppInfo(target.ExecutablePath);
        }
        targets.Where(t => t.ExecutablePath.Length == 0).Should().OnlyContain(t => t.SteamInfo == null && t.IconBytes == null);
    }

    [Fact]
    public async Task GetTargetAsync_DescribesAnEnumeratedWindowTheSameWay()
    {
        var targets = await _catalog.EnumerateAsync(Ct);
        if (targets.Count == 0)
        {
            Assert.Skip("No capturable windows are open in this session.");
        }

        // Windows can close or be renamed between the two calls; compare with one that is still there.
        foreach (var expected in targets)
        {
            var actual = await _catalog.GetTargetAsync(expected.WindowHandle, Ct);
            if (actual is null || actual.WindowTitle != expected.WindowTitle)
            {
                continue;
            }

            actual.ProcessId.Should().Be(expected.ProcessId);
            actual.ProcessName.Should().Be(expected.ProcessName);
            actual.ExecutablePath.Should().Be(expected.ExecutablePath);
            return;
        }
        Assert.Skip("Every window changed between the two calls.");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(0x7FFF_FFFF_FFF0L)]
    public async Task GetTargetAsync_NoOrUnknownWindow_ReturnsNull(long handle)
    {
        (await _catalog.GetTargetAsync((nint)handle, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task EnumerateAsync_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _catalog.EnumerateAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetForegroundWindow_IsEitherNothingOrARealWindow()
    {
        var foreground = _catalog.GetForegroundWindow();

        if (foreground != 0)
        {
            // It may belong to this process's console host or be a shell window, so it is not
            // necessarily a target; describing it must simply not fail.
            await _catalog.Invoking(c => c.GetTargetAsync(foreground, Ct)).Should().NotThrowAsync();
        }
    }
}
