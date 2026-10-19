// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.TestSupport;

/// <summary>Polling for conditions that become true on another thread (debounced writes, timers).</summary>
internal static class Eventually
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>True as soon as <paramref name="condition"/> holds; false once <paramref name="timeout"/> elapsed.</summary>
    public static async Task<bool> TrueAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(start) > timeout)
            {
                return false;
            }
            await Task.Delay(PollInterval, TestContext.Current.CancellationToken);
        }
        return true;
    }
}
