// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using Microsoft.Extensions.Time.Testing;

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that also reports every timer it hands out. A test can
/// wait until code under test is really blocked on <c>Task.Delay(…, timeProvider, …)</c>
/// before advancing the clock, so no advance is ever lost to a race.
/// </summary>
internal sealed class SignalingTimeProvider(DateTimeOffset start) : TimeProvider, IDisposable
{
    private readonly SemaphoreSlim _timerCreated = new(0);

    public FakeTimeProvider Fake { get; } = new(start);

    public override TimeZoneInfo LocalTimeZone => Fake.LocalTimeZone;

    public override long TimestampFrequency => Fake.TimestampFrequency;

    public override DateTimeOffset GetUtcNow() => Fake.GetUtcNow();

    public override long GetTimestamp() => Fake.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = Fake.CreateTimer(callback, state, dueTime, period);
        _timerCreated.Release();
        return timer;
    }

    public void Advance(TimeSpan delta) => Fake.Advance(delta);

    /// <summary>Wait for the next timer to be created; false on timeout.</summary>
    public Task<bool> WaitForTimerAsync(CancellationToken cancellationToken) =>
        _timerCreated.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

    public void Dispose() => _timerCreated.Dispose();
}
