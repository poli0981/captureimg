// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Infrastructure.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Windowing;

/// <summary>
/// <see cref="WinEventWindowWatcher"/> lifecycle: installing and removing the WinEvent hooks
/// must be idempotent and safe in any order, including after dispose.
/// </summary>
public sealed class WinEventWindowWatcherTests
{
    [Fact]
    public void StartAndStop_AreIdempotent()
    {
        using var watcher = CreateWatcher();
        watcher.IsRunning.Should().BeFalse();

        watcher.Start();
        watcher.Start();
        watcher.IsRunning.Should().BeTrue();

        watcher.Stop();
        watcher.Stop();
        watcher.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_AfterStop_RunsAgain()
    {
        using var watcher = CreateWatcher();

        watcher.Start();
        watcher.Stop();
        watcher.Start();

        watcher.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void Stop_WithoutStart_DoesNothing()
    {
        using var watcher = CreateWatcher();

        watcher.Invoking(w => w.Stop()).Should().NotThrow();
        watcher.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Dispose_StopsTheWatcher_AndIsIdempotent()
    {
        var watcher = CreateWatcher();
        watcher.Start();

        watcher.Dispose();
        watcher.Dispose();

        watcher.IsRunning.Should().BeFalse();
        watcher.Invoking(w => w.Stop()).Should().NotThrow();
        watcher.Invoking(w => w.Start()).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        var watcher = CreateWatcher();

        watcher.Invoking(w => w.Dispose()).Should().NotThrow();
    }

    private static WinEventWindowWatcher CreateWatcher() => new(NullLogger<WinEventWindowWatcher>.Instance);
}
