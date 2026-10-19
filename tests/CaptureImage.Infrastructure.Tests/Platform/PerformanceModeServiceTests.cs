// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using AwesomeAssertions;
using CaptureImage.Infrastructure.Platform;
using CaptureImage.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Platform;

/// <summary>
/// <see cref="PerformanceModeService"/>: below-normal priority (plus EcoQoS) while idle in the
/// tray, normal priority for the duration of every capture. It changes this test process's
/// priority, so it runs in the serialized collection and restores the priority afterwards.
/// </summary>
[Collection(ProcessWideStateCollection.Name)]
public sealed class PerformanceModeServiceTests : IDisposable
{
    private readonly ProcessPriorityClass _originalPriority = CurrentPriority();
    private readonly PerformanceModeService _service = new(NullLogger<PerformanceModeService>.Instance);

    public void Dispose()
    {
        _service.SetBackground(false);
        using var process = Process.GetCurrentProcess();
        process.PriorityClass = _originalPriority;
    }

    [Fact]
    public void SetBackground_LowersAndRestoresThePriority()
    {
        _service.SetBackground(true);
        CurrentPriority().Should().Be(ProcessPriorityClass.BelowNormal);

        _service.SetBackground(false);
        CurrentPriority().Should().Be(ProcessPriorityClass.Normal);
    }

    [Fact]
    public void BeginForegroundWork_RunsAtNormalPriorityUntilTheLastScopeEnds()
    {
        _service.SetBackground(true);

        var outer = _service.BeginForegroundWork();
        CurrentPriority().Should().Be(ProcessPriorityClass.Normal);
        using (_service.BeginForegroundWork())
        {
            CurrentPriority().Should().Be(ProcessPriorityClass.Normal);
        }
        CurrentPriority().Should().Be(ProcessPriorityClass.Normal, "the outer capture is still running");

        outer.Dispose();
        CurrentPriority().Should().Be(ProcessPriorityClass.BelowNormal);
    }

    [Fact]
    public void ForegroundScope_DisposedTwice_IsCountedOnce()
    {
        _service.SetBackground(true);
        var scope = _service.BeginForegroundWork();

        scope.Dispose();
        scope.Dispose();
        using (_service.BeginForegroundWork())
        {
            CurrentPriority().Should().Be(ProcessPriorityClass.Normal, "a double dispose must not leave the counter negative");
        }

        CurrentPriority().Should().Be(ProcessPriorityClass.BelowNormal);
    }

    [Fact]
    public void ForegroundWork_WhileNotInBackground_StaysNormal()
    {
        _service.SetBackground(false);

        using (_service.BeginForegroundWork())
        {
            CurrentPriority().Should().Be(ProcessPriorityClass.Normal);
        }

        CurrentPriority().Should().Be(ProcessPriorityClass.Normal);
    }

    private static ProcessPriorityClass CurrentPriority()
    {
        using var process = Process.GetCurrentProcess();
        return process.PriorityClass;
    }
}
