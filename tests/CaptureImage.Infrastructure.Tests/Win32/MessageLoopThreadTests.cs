// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Infrastructure.Win32;
using Windows.Win32;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Win32;

/// <summary>
/// <see cref="MessageLoopThread"/>, the thread that owns hotkey registrations and WinEvent
/// hooks: marshalling work onto it, error propagation, <c>WM_HOTKEY</c> dispatch and disposal.
/// </summary>
public sealed class MessageLoopThreadTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Invoke_RunsOnTheLoopThread_AndReturnsTheResult()
    {
        using var loop = new MessageLoopThread("test loop");

        var (onLoop, threadId) = loop.Invoke(() => (loop.IsCurrentThread, Environment.CurrentManagedThreadId));

        loop.IsCurrentThread.Should().BeFalse();
        onLoop.Should().BeTrue();
        threadId.Should().NotBe(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public void Invoke_FromTheLoopThreadItself_RunsInlineInsteadOfDeadlocking()
    {
        using var loop = new MessageLoopThread("test loop");

        var result = loop.Invoke(() => loop.Invoke(() => 42));

        result.Should().Be(42);
    }

    [Fact]
    public void Invoke_PropagatesExceptions_AndTheLoopKeepsRunning()
    {
        using var loop = new MessageLoopThread("test loop");

        var act = () => loop.Invoke<int>(() => throw new InvalidOperationException("boom"));

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
        loop.Invoke(() => 7).Should().Be(7);
    }

    [Fact]
    public void Invoke_Action_RunsOnTheLoop()
    {
        using var loop = new MessageLoopThread("test loop");
        var ranOnLoop = false;

        loop.Invoke(() => { ranOnLoop = loop.IsCurrentThread; });

        ranOnLoop.Should().BeTrue();
    }

    [Fact]
    public async Task Post_RunsTheActionLaterOnTheLoop()
    {
        using var loop = new MessageLoopThread("test loop");
        var ran = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        loop.Post(() => ran.SetResult(loop.IsCurrentThread));

        (await ran.Task.WaitAsync(WaitTimeout, Ct)).Should().BeTrue();
    }

    [Fact]
    public void Post_ActionThatThrows_DoesNotStopTheLoop()
    {
        using var loop = new MessageLoopThread("test loop");

        loop.Post(() => throw new InvalidOperationException("escaped"));

        loop.Invoke(() => 1).Should().Be(1);
    }

    [Fact]
    public async Task WmHotkey_RaisesHotkeyPressedOnTheLoop_WithTheHotkeyId()
    {
        using var loop = new MessageLoopThread("test loop");
        var pressed = new TaskCompletionSource<(int Id, bool OnLoop)>(TaskCreationOptions.RunContinuationsAsynchronously);
        loop.HotkeyPressed += hotkeyId => pressed.TrySetResult((hotkeyId, loop.IsCurrentThread));

        bool posted = PInvoke.PostThreadMessage(loop.Invoke(PInvoke.GetCurrentThreadId), PInvoke.WM_HOTKEY, (nuint)0x1234, default);

        posted.Should().BeTrue();
        var (id, onLoop) = await pressed.Task.WaitAsync(WaitTimeout, Ct);
        id.Should().Be(0x1234);
        onLoop.Should().BeTrue();
    }

    [Fact]
    public void Dispose_IsIdempotent_AndLaterInvokesThrow()
    {
        var loop = new MessageLoopThread("test loop");

        loop.Dispose();
        loop.Dispose();

        loop.Invoking(l => l.Invoke(() => 1)).Should().Throw<ObjectDisposedException>();
    }
}
