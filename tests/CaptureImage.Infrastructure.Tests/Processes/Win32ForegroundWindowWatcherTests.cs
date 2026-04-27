using CaptureImage.Infrastructure.Processes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Processes;

/// <summary>
/// Smoke tests for <see cref="Win32ForegroundWindowWatcher"/> lifecycle. Verifies that
/// construction, idempotent stop, and disposal don't throw — the actual SetWinEventHook
/// path needs a UI thread with a message pump and is exercised manually during QA.
/// </summary>
public class Win32ForegroundWindowWatcherTests
{
    [Fact]
    public void NewInstance_IsNotRunning()
    {
        using var watcher = new Win32ForegroundWindowWatcher(
            NullLogger<Win32ForegroundWindowWatcher>.Instance);

        watcher.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Stop_WithoutStart_IsNoOp()
    {
        using var watcher = new Win32ForegroundWindowWatcher(
            NullLogger<Win32ForegroundWindowWatcher>.Instance);

        var act = () => watcher.Stop();

        act.Should().NotThrow();
        watcher.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var watcher = new Win32ForegroundWindowWatcher(
            NullLogger<Win32ForegroundWindowWatcher>.Instance);

        watcher.Dispose();
        var act = () => watcher.Dispose();

        act.Should().NotThrow();
    }
}
