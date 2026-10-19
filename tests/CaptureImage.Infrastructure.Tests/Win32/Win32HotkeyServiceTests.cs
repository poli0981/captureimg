// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using System.Reflection;
using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Tests.TestSupport;
using CaptureImage.Infrastructure.Win32;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Win32;

/// <summary>
/// <see cref="Win32HotkeyService"/> against the real <c>RegisterHotKey</c> API, using
/// Ctrl+Alt+Shift+F19…F24 combinations that are free on this desktop. Every test releases its
/// keys in <c>finally</c>; the class runs in the serialized collection because a combination
/// can be held only once per desktop. Key presses are simulated by posting <c>WM_HOTKEY</c> to
/// the service's message loop — no keyboard input is injected into the user's session.
/// </summary>
[Collection(ProcessWideStateCollection.Name)]
public sealed class Win32HotkeyServiceTests
{
    // Win32HotkeyService assigns ids FirstCaptureId + (int)mode and FirstTemporaryId + n (private constants).
    private const int WindowHotkeyId = 0x0100;
    private const int RegionHotkeyId = 0x0101;
    private const int MonitorHotkeyId = 0x0102;
    private const int FirstTemporaryHotkeyId = 0x0200;

    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Activate_FreeBinding_IsRegistered_AndUnboundModesSaySo()
    {
        var a = FreeBindings(1)[0];
        var service = CreateService();
        try
        {
            var result = service.Activate(Bindings(window: a));

            result.Should().Equal(
                new HotkeyRegistration(CaptureMode.Window, a, HotkeyRegistrationStatus.Registered),
                new HotkeyRegistration(CaptureMode.Region, HotkeyBinding.Unbound, HotkeyRegistrationStatus.Unbound),
                new HotkeyRegistration(CaptureMode.Monitor, HotkeyBinding.Unbound, HotkeyRegistrationStatus.Unbound));
            service.IsActive.Should().BeTrue();
            service.Registrations.Should().Equal(result);
            FreeHotkeys.IsFree(a).Should().BeFalse("the service holds the combination while active");
        }
        finally
        {
            service.Dispose();
        }

        FreeHotkeys.IsFree(a).Should().BeTrue("Dispose releases every key");
    }

    [Fact]
    public void Activate_EveryMode_RegistersEachBinding()
    {
        var free = FreeBindings(3);
        var service = CreateService();
        try
        {
            var result = service.Activate(Bindings(window: free[0], region: free[1], monitor: free[2]));

            result.Select(r => (r.Mode, r.Binding, r.Status)).Should().Equal(
                (CaptureMode.Window, free[0], HotkeyRegistrationStatus.Registered),
                (CaptureMode.Region, free[1], HotkeyRegistrationStatus.Registered),
                (CaptureMode.Monitor, free[2], HotkeyRegistrationStatus.Registered));
            free.Should().OnlyContain(binding => !FreeHotkeys.IsFree(binding));
        }
        finally
        {
            service.Dispose();
        }

        free.Should().OnlyContain(binding => FreeHotkeys.IsFree(binding));
    }

    [Fact]
    public void Deactivate_ReleasesTheKeys_AndTheServiceCanBeActivatedAgain()
    {
        var a = FreeBindings(1)[0];
        var service = CreateService();
        try
        {
            service.Activate(Bindings(window: a));

            service.Deactivate();

            service.IsActive.Should().BeFalse();
            service.Registrations.Should().BeEmpty();
            FreeHotkeys.IsFree(a).Should().BeTrue();

            service.Deactivate();
            service.Activate(Bindings(window: a)).Should().Contain(r => r.Mode == CaptureMode.Window && r.Status == HotkeyRegistrationStatus.Registered);
            service.IsActive.Should().BeTrue();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Activate_Again_ReplacesThePreviousBindings()
    {
        var free = FreeBindings(2);
        var service = CreateService();
        try
        {
            service.Activate(Bindings(window: free[0]));

            service.Activate(Bindings(window: free[1]));

            FreeHotkeys.IsFree(free[0]).Should().BeTrue("the old binding is released");
            FreeHotkeys.IsFree(free[1]).Should().BeFalse();
            service.Registrations.Should().Contain(new HotkeyRegistration(CaptureMode.Window, free[1], HotkeyRegistrationStatus.Registered));
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Activate_InvalidBindings_ReportInvalid_AndNothingIsRegistered()
    {
        var service = CreateService();
        try
        {
            var result = service.Activate(Bindings(
                window: new HotkeyBinding(HotkeyModifiers.None, 0x41),            // plain "A": needs a modifier
                region: new HotkeyBinding(HotkeyModifiers.Control, 0x11),          // Ctrl+Ctrl: modifier only
                monitor: new HotkeyBinding(HotkeyModifiers.Win, 0x4C)));           // Win+L: owned by Windows

            result.Should().OnlyContain(r => r.Status == HotkeyRegistrationStatus.Invalid);
            service.IsActive.Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Activate_CombinationHeldElsewhere_ReportsConflict()
    {
        var a = FreeBindings(1)[0];
        var owner = CreateService();
        var latecomer = CreateService();
        try
        {
            owner.Activate(Bindings(window: a));

            var result = latecomer.Activate(Bindings(region: a));

            result.Should().Contain(new HotkeyRegistration(CaptureMode.Region, a, HotkeyRegistrationStatus.Conflict));
            latecomer.IsActive.Should().BeFalse();
            latecomer.IsTakenByAnotherApp(a).Should().BeTrue();
            owner.IsTakenByAnotherApp(a).Should().BeFalse("a binding the service holds itself is not a conflict");
        }
        finally
        {
            latecomer.Dispose();
            owner.Dispose();
        }
    }

    [Fact]
    public void IsTakenByAnotherApp_FreeCombination_IsFalse_AndTheProbeIsReleased()
    {
        var a = FreeBindings(1)[0];
        var service = CreateService();
        try
        {
            service.IsTakenByAnotherApp(a).Should().BeFalse();

            FreeHotkeys.IsFree(a).Should().BeTrue("probing must not keep the key registered");
            service.IsActive.Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void IsTakenByAnotherApp_UnboundOrInvalidBinding_IsFalse()
    {
        var service = CreateService();
        try
        {
            service.IsTakenByAnotherApp(HotkeyBinding.Unbound).Should().BeFalse();
            service.IsTakenByAnotherApp(new HotkeyBinding(HotkeyModifiers.Alt, 0x73)).Should().BeFalse(); // Alt+F4
            service.IsTakenByAnotherApp(new HotkeyBinding(HotkeyModifiers.None, 0x41)).Should().BeFalse();
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void RegisterTemporary_HoldsTheKeysUntilDisposed()
    {
        var free = FreeBindings(2);
        var service = CreateService();
        try
        {
            var temporary = service.RegisterTemporary(free, _ => { });
            free.Should().OnlyContain(binding => !FreeHotkeys.IsFree(binding));
            service.IsActive.Should().BeFalse("temporary keys are not capture hotkeys");

            temporary.Dispose();
            temporary.Dispose();

            free.Should().OnlyContain(binding => FreeHotkeys.IsFree(binding));
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void RegisterTemporary_SkipsCombinationsHeldElsewhere()
    {
        var free = FreeBindings(2);
        var owner = CreateService();
        var service = CreateService();
        try
        {
            owner.Activate(Bindings(window: free[0]));

            using (service.RegisterTemporary(free, _ => { }))
            {
                FreeHotkeys.IsFree(free[1]).Should().BeFalse();
            }

            FreeHotkeys.IsFree(free[1]).Should().BeTrue();
            owner.IsActive.Should().BeTrue("releasing the temporary keys must not touch another registrant's key");
            FreeHotkeys.IsFree(free[0]).Should().BeFalse();
        }
        finally
        {
            service.Dispose();
            owner.Dispose();
        }
    }

    [Fact]
    public async Task Triggered_IsRaisedOffTheMessageLoop_WithTheModeOfThePressedKey()
    {
        var a = FreeBindings(1)[0];
        var service = CreateService();
        try
        {
            service.Activate(Bindings(monitor: a));
            var loopThread = LoopThreadId(service);
            var raised = new TaskCompletionSource<(CaptureMode Mode, uint Thread)>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Triggered += (_, pressedMode) => raised.TrySetResult((pressedMode, PInvoke.GetCurrentThreadId()));

            SimulateKeyPress(service, MonitorHotkeyId);

            var (mode, thread) = await raised.Task.WaitAsync(EventTimeout, Ct);
            mode.Should().Be(CaptureMode.Monitor);
            thread.Should().NotBe(loopThread, "a slow subscriber must not stall the hotkey message loop");
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task Triggered_IgnoresIdsThatAreNotRegisteredCaptureKeys()
    {
        var a = FreeBindings(1)[0];
        var service = CreateService();
        try
        {
            service.Activate(Bindings(window: a));
            var modes = new ConcurrentQueue<CaptureMode>();
            service.Triggered += (_, mode) => modes.Enqueue(mode);

            SimulateKeyPress(service, RegionHotkeyId); // Region is unbound, so its id is not registered
            SimulateKeyPress(service, 0x0042);         // not an id the service uses
            SimulateKeyPress(service, WindowHotkeyId);

            (await Eventually.TrueAsync(() => !modes.IsEmpty, EventTimeout)).Should().BeTrue();
            await Task.Delay(100, Ct); // a wrongly raised event would have been queued before Window's
            modes.Should().Equal(CaptureMode.Window);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task RegisterTemporary_PressedKey_InvokesTheCallbackWithItsBinding()
    {
        var free = FreeBindings(2);
        var service = CreateService();
        try
        {
            var pressed = new TaskCompletionSource<HotkeyBinding>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (service.RegisterTemporary(free, binding => pressed.TrySetResult(binding)))
            {
                SimulateKeyPress(service, FirstTemporaryHotkeyId + 1);

                (await pressed.Task.WaitAsync(EventTimeout, Ct)).Should().Be(free[1]);
            }
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public void Dispose_ReleasesTemporaryKeys_AndMakesTheServiceInert()
    {
        var free = FreeBindings(2);
        var service = CreateService();
        IDisposable temporary;
        try
        {
            service.Activate(Bindings(window: free[0]));
            temporary = service.RegisterTemporary([free[1]], _ => { });
        }
        finally
        {
            service.Dispose();
        }

        free.Should().OnlyContain(binding => FreeHotkeys.IsFree(binding));
        temporary.Invoking(t => t.Dispose()).Should().NotThrow();
        service.Invoking(s => s.Dispose()).Should().NotThrow();
        service.Invoking(s => s.Deactivate()).Should().NotThrow();
        service.IsTakenByAnotherApp(free[0]).Should().BeFalse();
        service.Invoking(s => s.Activate(Bindings(window: free[0]))).Should().Throw<ObjectDisposedException>();
        service.Invoking(s => s.RegisterTemporary([free[0]], _ => { })).Should().Throw<ObjectDisposedException>();
        FreeHotkeys.IsFree(free[0]).Should().BeTrue();
    }

    private static Win32HotkeyService CreateService() => new(NullLogger<Win32HotkeyService>.Instance);

    private static HotkeySettings Bindings(HotkeyBinding window = default, HotkeyBinding region = default, HotkeyBinding monitor = default) =>
        new() { Window = window, Region = region, Monitor = monitor };

    private static IReadOnlyList<HotkeyBinding> FreeBindings(int count)
    {
        var free = FreeHotkeys.Find(count);
        if (free.Count < count)
        {
            Assert.Skip($"Fewer than {count} of Ctrl+Alt+Shift+F19…F24 can be registered here (other programs hold them, or there is no interactive desktop).");
        }
        return free;
    }

    private static MessageLoopThread Loop(Win32HotkeyService service) =>
        typeof(Win32HotkeyService).GetField("_loop", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(service) as MessageLoopThread
        ?? throw new InvalidOperationException("Win32HotkeyService no longer keeps its MessageLoopThread in _loop; update this test helper.");

    private static uint LoopThreadId(Win32HotkeyService service) => Loop(service).Invoke(PInvoke.GetCurrentThreadId);

    /// <summary>Post <c>WM_HOTKEY</c> to the service's loop, exactly as Windows does for a pressed hotkey.</summary>
    private static void SimulateKeyPress(Win32HotkeyService service, int hotkeyId)
    {
        bool posted = PInvoke.PostThreadMessage(LoopThreadId(service), PInvoke.WM_HOTKEY, (nuint)hotkeyId, default);
        posted.Should().BeTrue();
    }
}
