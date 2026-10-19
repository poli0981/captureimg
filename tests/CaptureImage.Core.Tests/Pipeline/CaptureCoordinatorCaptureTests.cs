// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Profiles;
using CaptureImage.Core.Tests.TestSupport;
using NSubstitute;
using Xunit;

namespace CaptureImage.Core.Tests.Pipeline;

/// <summary>Running captures: window, monitor and region modes, failures, cancellation, preview, countdown.</summary>
public sealed class CaptureCoordinatorCaptureTests : IDisposable
{
    private readonly CoordinatorHarness _h = new();

    public void Dispose() => _h.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CaptureCoordinator Coordinator => _h.Coordinator;

    private string GameFile(string extension = ".png") => _h.OutputFile(CoordinatorHarness.GameStem + extension);

    /// <summary>Replace the engine's behaviour; the callback gets the source and the capture's token.</summary>
    private void EngineReturns(Func<CaptureSource, CancellationToken, Task<CapturedFrame>> capture) =>
        _h.Engine.CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => capture(call.ArgAt<CaptureSource>(0), call.ArgAt<CancellationToken>(2)));

    private void EngineFails(Exception exception) => EngineReturns((_, _) => Task.FromException<CapturedFrame>(exception));

    private void AssertNoErrorToast() =>
        _h.Toasts.DidNotReceive().Show(ToastKind.Error, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());

    private static async Task<CapturedFrame> WaitUntilCancelled(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The delay never ends without cancellation.");
    }

    // ---- Window mode: happy path ------------------------------------------------------------

    [Fact]
    public async Task Window_CapturesTheForegroundWindow_SavesIt_AndNotifies()
    {
        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        outcome.Mode.Should().Be(CaptureMode.Window);
        outcome.IsSuccess.Should().BeTrue();
        outcome.Error.Should().BeNull();
        outcome.Record.Should().Be(new CaptureRecord(
            GameFile(),
            CoordinatorHarness.Start,
            64,
            36,
            ImageFormat.Png,
            FakeImageEncoder.ContentFor(ImageFormat.Png).Length,
            CaptureMode.Window,
            "Elden Ring"));
        (await File.ReadAllBytesAsync(GameFile(), Ct)).Should().Equal(FakeImageEncoder.ContentFor(ImageFormat.Png));

        _ = _h.Engine.Received(1).CaptureAsync(
            new WindowCaptureSource(CoordinatorHarness.GameWindow),
            new CaptureOptions(PreferHdr: false, IncludeCursor: false, IncludeWindowFrame: false),
            Arg.Any<CancellationToken>());
        _h.Encoder.Calls.Should().ContainSingle().Which.Frame.Should().BeSameAs(_h.WindowFrame);
        _ = _h.Library.Received(1).AddAsync(outcome.Record!, Arg.Any<CancellationToken>());
        _h.Toasts.Received(1).Show(ToastKind.Success, "L:Toast_CaptureSaved", outcome.Record!.FileName, null);
        _h.Sound.Received(1).PlayCaptureSound();
        AssertNoErrorToast();
        _ = _h.Clipboard.DidNotReceive().CopyImageAsync(Arg.Any<CapturedFrame>(), Arg.Any<CancellationToken>());
        _h.Shell.DidNotReceive().RevealInExplorer(Arg.Any<string>());
        _h.Pinned.DidNotReceive().Show(Arg.Any<CapturedFrame>(), Arg.Any<string?>(), Arg.Any<string>());

        _h.Completed.Should().ContainSingle().Which.Should().BeSameAs(outcome);
        _h.Phases.Should().Equal(CapturePhase.Capturing, CapturePhase.Saving, CapturePhase.Idle);
        Coordinator.IsBusy.Should().BeFalse();
        Coordinator.Phase.Should().Be(CapturePhase.Idle);
        _h.Performance.Received(1).BeginForegroundWork();
        _h.ForegroundWork.Received(1).Dispose();
    }

    [Fact]
    public async Task Window_PassesTheEncoderSettingsAndMetadata()
    {
        _h.UpdateCapture(c => c with { JpegQuality = 70, WebpQuality = 60, PngCompression = "smallest" });

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        var settings = _h.Encoder.Calls.Should().ContainSingle().Subject.Settings;
        settings.JpegQuality.Should().Be(70);
        settings.WebpQuality.Should().Be(60);
        settings.PngCompression.Should().Be(PngCompressionLevel.Smallest);
        settings.Metadata!.Title.Should().Be("Elden Ring");
        settings.Metadata.Software.Should().StartWith("CaptureImage ");
        settings.Metadata.CapturedAt.Should().Be(CoordinatorHarness.Start);
    }

    [Fact]
    public async Task Window_CursorAndFrameOptions_ReachTheEngine()
    {
        _h.UpdateCapture(c => c with { IncludeCursor = true, IncludeWindowFrame = true });

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(
            Arg.Any<CaptureSource>(),
            Arg.Is<CaptureOptions>(o => o.IncludeCursor && o.IncludeWindowFrame),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Window_LockedTarget_IsCapturedInsteadOfTheForegroundWindow()
    {
        var locked = TestTargets.Game(windowHandle: 0x2002, title: "Locked Game", processName: "locked");
        _h.Targets.GetTargetAsync(0x2002, Arg.Any<CancellationToken>()).Returns(locked);
        Coordinator.LockTarget(locked);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Record!.Game.Should().Be("Locked Game");
        _ = _h.Engine.Received(1).CaptureAsync(new WindowCaptureSource(0x2002), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
        _h.Targets.DidNotReceive().GetForegroundWindow();
        Coordinator.LockedTarget.Should().Be(locked);
    }

    [Fact]
    public async Task Window_LockedWindowWasClosed_FailsWithTargetGone_AndUnlocks()
    {
        Coordinator.LockTarget(TestTargets.Game(windowHandle: 0x2002));
        _h.Targets.GetTargetAsync(0x2002, Arg.Any<CancellationToken>()).Returns((GameTarget?)null);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Failed);
        outcome.Error.Should().Be(CaptureError.TargetGone);
        Coordinator.LockedTarget.Should().BeNull();
        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", "L:Error_TargetGone", null);
    }

    [Fact]
    public async Task Window_EngineReportsTheLockedWindowGone_Unlocks()
    {
        var locked = TestTargets.Game(windowHandle: 0x2002);
        _h.Targets.GetTargetAsync(0x2002, Arg.Any<CancellationToken>()).Returns(locked);
        Coordinator.LockTarget(locked);
        EngineFails(new CaptureException(CaptureError.TargetGone, "window destroyed"));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Error.Should().Be(CaptureError.TargetGone);
        Coordinator.LockedTarget.Should().BeNull();
    }

    [Fact]
    public async Task Window_ExplicitTarget_OverridesTheForegroundWindow()
    {
        var picked = TestTargets.Game(windowHandle: 0x3003, title: "Picked");
        _h.Targets.GetTargetAsync(0x3003, Arg.Any<CancellationToken>()).Returns(picked);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, picked, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        outcome.Record!.Game.Should().Be("Picked");
        _ = _h.Engine.Received(1).CaptureAsync(new WindowCaptureSource(0x3003), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Window_ExplicitTargetThatClosed_FailsWithTargetGone()
    {
        var picked = TestTargets.Game(windowHandle: 0x3003);
        _h.Targets.GetTargetAsync(0x3003, Arg.Any<CancellationToken>()).Returns((GameTarget?)null);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, picked, Ct);

        outcome.Error.Should().Be(CaptureError.TargetGone);
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Window_NothingInTheForeground_FailsWithNoTarget()
    {
        _h.Targets.GetForegroundWindow().Returns(0);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Error.Should().Be(CaptureError.NoTarget);
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", "L:Error_NoTarget", null);
    }

    [Fact]
    public async Task Window_MinimizedWindow_FailsWithTargetMinimized()
    {
        _h.Targets.GetTargetAsync(CoordinatorHarness.GameWindow, Arg.Any<CancellationToken>())
            .Returns(TestTargets.Game(windowHandle: CoordinatorHarness.GameWindow, minimized: true));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Error.Should().Be(CaptureError.TargetMinimized);
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
    }

    // ---- HDR --------------------------------------------------------------------------------------

    private void UseHdrDisplay()
    {
        var hdrMonitor = CoordinatorHarness.PrimaryMonitor with { Hdr = CoordinatorHarness.HdrDisplay };
        _h.Displays.GetMonitorForWindow(Arg.Any<nint>()).Returns(hdrMonitor);
        _h.Displays.GetMonitorAtCursor().Returns(hdrMonitor);
        EngineReturns((_, _) => Task.FromResult(Frames.SolidHalf(32, 18, new RgbaPixel(3f, 1.5f, 0.75f, 1f))));
    }

    [Fact]
    public async Task Window_OnAnHdrDisplay_SavesAToneMappedImage_PlusAJpegXrSidecar()
    {
        UseHdrDisplay();

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Is<CaptureOptions>(o => o.PreferHdr), Arg.Any<CancellationToken>());
        var record = outcome.Record!;
        record.FilePath.Should().Be(GameFile());
        record.HdrFilePath.Should().Be(GameFile(".jxr"));
        File.Exists(record.HdrFilePath).Should().BeTrue();

        var png = _h.Encoder.Calls.Single(c => c.Format == ImageFormat.Png).Frame;
        png.Format.Should().Be(FramePixelFormat.Bgra8);
        var jxr = _h.Encoder.Calls.Single(c => c.Format == ImageFormat.Jxr).Frame;
        jxr.Format.Should().Be(FramePixelFormat.RgbaHalf);
        jxr.Hdr.Should().Be(CoordinatorHarness.HdrDisplay, "the frame carries the display's white level");
        Frames.ReadBgra(png, 0, 0).Should().Be(Frames.ReadBgra(HdrToneMapper.ToSdr(jxr), 0, 0));
    }

    [Fact]
    public async Task Window_HdrSidecarSwitchedOff_SavesOnlyTheImage()
    {
        UseHdrDisplay();
        _h.UpdateCapture(c => c with { SaveHdrSidecar = false });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Record!.HdrFilePath.Should().BeNull();
        _h.Encoder.Calls.Should().ContainSingle().Which.Format.Should().Be(ImageFormat.Png);
    }

    [Fact]
    public async Task Window_HdrModeOff_RequestsAn8BitFrame()
    {
        UseHdrDisplay();
        _h.UpdateCapture(c => c with { HdrMode = "Off" });

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Is<CaptureOptions>(o => !o.PreferHdr), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Window_ProfileHdrModeOff_OverridesTheGlobalSetting()
    {
        UseHdrDisplay();
        _h.UpdateSettings(
            s => s with { Profiles = [new GameProfile { Name = "Elden Ring", ExecutableName = "eldenring.exe", HdrMode = "Off" }] },
            raiseChanged: false);

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Is<CaptureOptions>(o => !o.PreferHdr), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Window_OnAnSdrDisplay_NeverAsksForHdr()
    {
        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Is<CaptureOptions>(o => !o.PreferHdr), Arg.Any<CancellationToken>());
    }

    // ---- profiles ----------------------------------------------------------------------------------

    [Fact]
    public async Task Profile_OverridesFolderFormatAndFileName()
    {
        var folder = _h.Output.Combine("Elden Ring shots");
        _h.UpdateSettings(
            s => s with
            {
                Profiles =
                [
                    new GameProfile
                    {
                        Name = "Elden Ring",
                        ExecutableName = "eldenring.exe",
                        OutputDirectory = folder,
                        Format = ImageFormat.Jpeg,
                        FileNameTemplate = "{Process}-{yyyy}",
                    },
                ],
            },
            raiseChanged: false);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Record!.FilePath.Should().Be(Path.Combine(folder, "eldenring-2026.jpg"));
        outcome.Record.Format.Should().Be(ImageFormat.Jpeg);
        File.Exists(outcome.Record.FilePath).Should().BeTrue();
    }

    // ---- Monitor mode ---------------------------------------------------------------------------------

    [Fact]
    public async Task Monitor_CapturesTheMonitorUnderTheCursor_IncludingEverythingOnIt()
    {
        _h.Displays.GetMonitorAtCursor().Returns(CoordinatorHarness.SecondMonitor);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Monitor, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        outcome.Record!.Mode.Should().Be(CaptureMode.Monitor);
        outcome.Record.Width.Should().Be(16);
        _ = _h.Engine.Received(1).CaptureAsync(
            new MonitorCaptureSource(CoordinatorHarness.SecondMonitor.Handle),
            Arg.Is<CaptureOptions>(o => o.IncludeWindowFrame),
            Arg.Any<CancellationToken>());
        _h.Encoder.Calls.Should().ContainSingle().Which.Frame.Should().BeSameAs(_h.MonitorFrames[CoordinatorHarness.SecondMonitor.Handle]);
    }

    [Fact]
    public async Task Monitor_NoMonitorUnderTheCursor_FailsWithTargetGone()
    {
        _h.Displays.GetMonitorAtCursor().Returns((MonitorInfo?)null);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Monitor, Ct);

        outcome.Error.Should().Be(CaptureError.TargetGone);
    }

    [Fact]
    public async Task Monitor_WithoutAForegroundGame_IsNamedAfterTheMode()
    {
        _h.Targets.GetForegroundWindow().Returns(0);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Monitor, Ct);

        outcome.Record!.FileName.Should().Be("Monitor_2026-10-19_07-15-00.png");
        outcome.Record.Game.Should().BeNull();
    }

    [Fact]
    public async Task HotkeyPress_RunsACaptureForThatMode()
    {
        _h.Hotkeys.Triggered += Raise.Event<EventHandler<CaptureMode>>(_h.Hotkeys, CaptureMode.Monitor);

        var outcome = await _h.NextCompletionAsync(Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        outcome.Mode.Should().Be(CaptureMode.Monitor);
    }

    // ---- Region mode ----------------------------------------------------------------------------------

    [Fact]
    public async Task Region_FreezesEveryMonitor_ThenSavesTheSelectedArea()
    {
        IReadOnlyList<MonitorSnapshot>? offered = null;
        CapturePhase phaseWhileSelecting = default;
        _h.RegionSelector.SelectAsync(Arg.Any<IReadOnlyList<MonitorSnapshot>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                offered = call.Arg<IReadOnlyList<MonitorSnapshot>>();
                phaseWhileSelecting = Coordinator.Phase;
                return new RegionSelection(CoordinatorHarness.SecondMonitor, new PixelRect(2, 1, 4, 3));
            });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        offered.Should().NotBeNull();
        offered!.Select(s => s.Monitor).Should().Equal(CoordinatorHarness.PrimaryMonitor, CoordinatorHarness.SecondMonitor);
        offered.Select(s => s.DisplayFrame).Should().Equal(
            _h.MonitorFrames[CoordinatorHarness.PrimaryMonitor.Handle],
            _h.MonitorFrames[CoordinatorHarness.SecondMonitor.Handle]);
        phaseWhileSelecting.Should().Be(CapturePhase.Selecting);

        var saved = _h.Encoder.Calls.Should().ContainSingle().Subject.Frame;
        saved.Width.Should().Be(4);
        saved.Height.Should().Be(3);
        Frames.ReadBgra(saved, 0, 0).Should().Be(new BgraPixel(2, 1, (byte)CoordinatorHarness.SecondMonitor.Handle, 255));
        Frames.ReadBgra(saved, 3, 2).Should().Be(new BgraPixel(5, 3, (byte)CoordinatorHarness.SecondMonitor.Handle, 255));
        outcome.Record!.Mode.Should().Be(CaptureMode.Region);
        outcome.Record.Width.Should().Be(4);
        outcome.Record.Height.Should().Be(3);
        _h.Phases.Should().Equal(
            CapturePhase.Capturing, CapturePhase.Selecting, CapturePhase.Capturing, CapturePhase.Saving, CapturePhase.Idle);
    }

    [Fact]
    public async Task Region_SelectionCancelled_ReturnsCancelled_WithoutSavingOrComplaining()
    {
        _h.RegionSelector.SelectAsync(Arg.Any<IReadOnlyList<MonitorSnapshot>>(), Arg.Any<CancellationToken>())
            .Returns((RegionSelection?)null);

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
        outcome.IsSuccess.Should().BeFalse();
        outcome.Record.Should().BeNull();
        _h.Output.RelativeFiles().Should().BeEmpty();
        _ = _h.Library.DidNotReceive().AddAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>());
        _h.Sound.DidNotReceive().PlayCaptureSound();
        AssertNoErrorToast();
        _h.Completed.Should().ContainSingle().Which.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
    }

    [Fact]
    public async Task Region_MonitorThatCannotBeCaptured_IsLeftOut()
    {
        var primary = new MonitorCaptureSource(CoordinatorHarness.PrimaryMonitor.Handle);
        EngineReturns((source, _) => source == primary
            ? Task.FromException<CapturedFrame>(new CaptureException(CaptureError.ProtectedContent, "protected"))
            : Task.FromResult(_h.MonitorFrames[CoordinatorHarness.SecondMonitor.Handle]));
        IReadOnlyList<MonitorSnapshot>? offered = null;
        _h.RegionSelector.SelectAsync(Arg.Do<IReadOnlyList<MonitorSnapshot>>(s => offered = s), Arg.Any<CancellationToken>())
            .Returns((RegionSelection?)null);

        await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        offered.Should().ContainSingle().Which.Monitor.Should().Be(CoordinatorHarness.SecondMonitor);
    }

    [Fact]
    public async Task Region_NoMonitorCanBeCaptured_FailsWithNoFrameArrived()
    {
        EngineFails(new CaptureException(CaptureError.GraphicsDeviceFailure, "device lost"));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        outcome.Error.Should().Be(CaptureError.NoFrameArrived);
        _ = _h.RegionSelector.DidNotReceive().SelectAsync(Arg.Any<IReadOnlyList<MonitorSnapshot>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Region_OnAnHdrMonitor_ShowsAToneMappedSnapshot_AndKeepsTheHdrCrop()
    {
        var hdrMonitor = CoordinatorHarness.PrimaryMonitor with { Hdr = CoordinatorHarness.HdrDisplay };
        _h.Displays.GetMonitors().Returns([hdrMonitor]);
        EngineReturns((_, _) => Task.FromResult(Frames.CreateHalf(8, 6, (x, y) => new RgbaPixel(x, y, 1f, 1f))));
        IReadOnlyList<MonitorSnapshot>? offered = null;
        _h.RegionSelector.SelectAsync(Arg.Do<IReadOnlyList<MonitorSnapshot>>(s => offered = s), Arg.Any<CancellationToken>())
            .Returns(new RegionSelection(hdrMonitor, new PixelRect(1, 1, 2, 2)));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Is<CaptureOptions>(o => o.PreferHdr), Arg.Any<CancellationToken>());
        offered.Should().ContainSingle().Which.DisplayFrame.Format.Should().Be(FramePixelFormat.Bgra8);
        var sidecar = _h.Encoder.Calls.Single(c => c.Format == ImageFormat.Jxr).Frame;
        sidecar.Format.Should().Be(FramePixelFormat.RgbaHalf);
        sidecar.Width.Should().Be(2);
        sidecar.Hdr.Should().Be(CoordinatorHarness.HdrDisplay);
        Frames.ReadHalf(sidecar, 0, 0).Should().Be(new RgbaPixel(1f, 1f, 1f, 1f));
        _h.Encoder.Calls.Single(c => c.Format == ImageFormat.Png).Frame.Format.Should().Be(FramePixelFormat.Bgra8);
        outcome.Record!.HasHdr.Should().BeTrue();
    }

    // ---- failures ------------------------------------------------------------------------------------

    [Theory]
    [InlineData(CaptureError.TargetGone)]
    [InlineData(CaptureError.ProcessExited)]
    [InlineData(CaptureError.ProtectedContent)]
    [InlineData(CaptureError.NoFrameArrived)]
    [InlineData(CaptureError.GraphicsDeviceFailure)]
    [InlineData(CaptureError.EncodingFailure)]
    [InlineData(CaptureError.FileWriteFailure)]
    [InlineData(CaptureError.TargetMinimized)]
    [InlineData(CaptureError.NoTarget)]
    [InlineData(CaptureError.NotSupported)]
    public async Task CaptureException_FailsTheCapture_AndShowsTheLocalizedError(CaptureError error)
    {
        EngineFails(new CaptureException(error, "engine detail"));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Failed);
        outcome.IsSuccess.Should().BeFalse();
        outcome.Error.Should().Be(error);
        outcome.ErrorMessage.Should().Be("engine detail");
        outcome.Record.Should().BeNull();
        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", $"L:Error_{error}", null);
        _ = _h.Library.DidNotReceive().AddAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>());
        _h.Sound.DidNotReceive().PlayCaptureSound();
        _h.Output.RelativeFiles().Should().BeEmpty();
        _h.Completed.Should().ContainSingle().Which.Should().BeSameAs(outcome);
        Coordinator.IsBusy.Should().BeFalse();
        Coordinator.Phase.Should().Be(CapturePhase.Idle);
    }

    [Fact]
    public async Task UnexpectedException_FailsAsUnknown()
    {
        EngineFails(new InvalidOperationException("driver bug"));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Failed);
        outcome.Error.Should().Be(CaptureError.Unknown);
        outcome.ErrorMessage.Should().Be("driver bug");
        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", "L:Error_Unknown", null);
    }

    [Fact]
    public async Task SaveFailure_IsReportedLikeAnyOtherFailure()
    {
        _h.Encoder.Midway = (_, _) => throw new IOException("disk full");

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Error.Should().Be(CaptureError.FileWriteFailure);
        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", "L:Error_FileWriteFailure", null);
        _h.Output.RelativeFiles().Should().BeEmpty();
    }

    [Fact]
    public async Task Failures_AreShownEvenWithNotificationsOff()
    {
        _h.UpdateUi(ui => ui with { ShowNotifications = false });
        EngineFails(new CaptureException(CaptureError.ProtectedContent, "protected"));

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _h.Toasts.Received(1).Show(ToastKind.Error, "L:Toast_CaptureFailed", "L:Error_ProtectedContent", null);
    }

    [Fact]
    public async Task LibraryIndexFailure_DoesNotFailASavedCapture()
    {
        _h.Library.AddAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("library.json is locked")));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        File.Exists(outcome.Record!.FilePath).Should().BeTrue();
        AssertNoErrorToast();
    }

    // ---- cancellation and concurrency ----------------------------------------------------------------

    [Fact]
    public async Task CaptureExceptionCancelled_IsACancellation_NotAnError()
    {
        EngineFails(new CaptureException(CaptureError.Cancelled, "cancelled"));

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
        outcome.Error.Should().BeNull();
        AssertNoErrorToast();
    }

    [Fact]
    public async Task CallerCancellation_ReturnsCancelled_WithoutAnErrorToast()
    {
        EngineReturns((_, token) => WaitUntilCancelled(token));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var capture = Coordinator.CaptureAsync(CaptureMode.Window, cts.Token);
        capture.IsCompleted.Should().BeFalse("the engine is still waiting for a frame");
        Coordinator.Phase.Should().Be(CapturePhase.Capturing);
        await cts.CancelAsync();
        var outcome = await capture.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
        AssertNoErrorToast();
        _h.Output.RelativeFiles().Should().BeEmpty();
        Coordinator.IsBusy.Should().BeFalse();
        Coordinator.Phase.Should().Be(CapturePhase.Idle);
    }

    [Fact]
    public async Task CancelCurrent_StopsTheRunningCapture()
    {
        EngineReturns((_, token) => WaitUntilCancelled(token));

        var capture = Coordinator.CaptureAsync(CaptureMode.Monitor, Ct);
        Coordinator.CancelCurrent();
        var outcome = await capture.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
        AssertNoErrorToast();
    }

    [Fact]
    public void CancelCurrent_WithNothingRunning_IsHarmless()
    {
        FluentActions.Invoking(Coordinator.CancelCurrent).Should().NotThrow();
    }

    [Fact]
    public async Task SecondCaptureWhileOneRuns_IsBusy()
    {
        var frame = new TaskCompletionSource<CapturedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        EngineReturns((_, _) => frame.Task);

        var first = Coordinator.CaptureAsync(CaptureMode.Window, Ct);
        Coordinator.IsBusy.Should().BeTrue();
        var second = await Coordinator.CaptureAsync(CaptureMode.Region, Ct);

        second.Kind.Should().Be(CaptureOutcomeKind.Busy);
        second.Mode.Should().Be(CaptureMode.Region);
        second.IsSuccess.Should().BeFalse();
        _h.Completed.Should().BeEmpty("a busy request is not a completed capture");

        frame.SetResult(_h.WindowFrame);
        (await first.WaitAsync(TimeSpan.FromSeconds(10), Ct)).Kind.Should().Be(CaptureOutcomeKind.Saved);

        Coordinator.IsBusy.Should().BeFalse();
        _h.Completed.Should().ContainSingle();
        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NextCapture_IsAcceptedOnceThePreviousOneFinished()
    {
        var first = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);
        var second = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        first.Kind.Should().Be(CaptureOutcomeKind.Saved);
        second.Kind.Should().Be(CaptureOutcomeKind.Saved);
        second.Record!.FilePath.Should().Be(_h.OutputFile(CoordinatorHarness.GameStem + "_1.png"), "the first file is never overwritten");
    }

    // ---- preview -------------------------------------------------------------------------------------

    [Fact]
    public async Task Preview_Discard_ReturnsDiscarded_AndSavesNothing()
    {
        _h.UpdateCapture(c => c with { PreviewBeforeSave = true });
        var phaseDuringPreview = CapturePhase.Idle;
        _h.Preview.ShowAsync(Arg.Any<CapturedFrame>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                phaseDuringPreview = Coordinator.Phase;
                return false;
            });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Discarded);
        outcome.IsSuccess.Should().BeFalse();
        phaseDuringPreview.Should().Be(CapturePhase.Previewing);
        _ = _h.Preview.Received(1).ShowAsync(_h.WindowFrame, "Elden Ring", Arg.Any<CancellationToken>());
        _h.Output.RelativeFiles().Should().BeEmpty();
        _ = _h.Library.DidNotReceive().AddAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>());
        _h.Toasts.DidNotReceive().Show(Arg.Any<ToastKind>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
        _h.Sound.DidNotReceive().PlayCaptureSound();
    }

    [Fact]
    public async Task Preview_Save_SavesTheCapture()
    {
        _h.UpdateCapture(c => c with { PreviewBeforeSave = true });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        File.Exists(GameFile()).Should().BeTrue();
        _h.Phases.Should().Equal(CapturePhase.Capturing, CapturePhase.Previewing, CapturePhase.Saving, CapturePhase.Idle);
    }

    [Fact]
    public async Task Preview_OfAnHdrCapture_ShowsTheToneMappedFrame()
    {
        UseHdrDisplay();
        _h.UpdateCapture(c => c with { PreviewBeforeSave = true });

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _ = _h.Preview.Received(1).ShowAsync(
            Arg.Is<CapturedFrame>(f => f.Format == FramePixelFormat.Bgra8),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ---- countdown -----------------------------------------------------------------------------------

    [Fact]
    public async Task Countdown_WaitsOnTheInjectedClock_BeforeCapturing()
    {
        _h.UpdateCapture(c => c with { CountdownSeconds = 3 });
        _h.Localization["Toast_Countdown"].Returns("Capturing in {0} s");

        var capture = Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        (await _h.Time.WaitForTimerAsync(Ct)).Should().BeTrue();
        capture.IsCompleted.Should().BeFalse();
        Coordinator.Phase.Should().Be(CapturePhase.Countdown);
        Coordinator.CountdownRemaining.Should().Be(3);
        _h.Toasts.Received(1).Show(ToastKind.Info, "L:Toast_CountdownTitle", "Capturing in 3 s", TimeSpan.FromSeconds(3));

        _h.Time.Advance(TimeSpan.FromSeconds(1));
        (await _h.Time.WaitForTimerAsync(Ct)).Should().BeTrue();
        Coordinator.CountdownRemaining.Should().Be(2);

        _h.Time.Advance(TimeSpan.FromSeconds(1));
        (await _h.Time.WaitForTimerAsync(Ct)).Should().BeTrue();
        Coordinator.CountdownRemaining.Should().Be(1);
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());

        _h.Time.Advance(TimeSpan.FromSeconds(1));
        var outcome = await capture.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        outcome.Duration.Should().Be(TimeSpan.FromSeconds(3));
        outcome.Record!.CapturedAt.Should().Be(CoordinatorHarness.Start.AddSeconds(3), "the frame is taken when the countdown ends");
        _ = _h.Engine.Received(1).CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
        _h.CountdownTicks.Should().Equal(3, 2, 1);
        Coordinator.CountdownRemaining.Should().Be(0);
    }

    [Fact]
    public async Task Countdown_CanBeCancelled_BeforeAnythingIsCaptured()
    {
        _h.UpdateCapture(c => c with { CountdownSeconds = 5 });

        var capture = Coordinator.CaptureAsync(CaptureMode.Window, Ct);
        (await _h.Time.WaitForTimerAsync(Ct)).Should().BeTrue();
        Coordinator.CancelCurrent();
        var outcome = await capture.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Cancelled);
        _ = _h.Engine.DidNotReceive().CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>());
        Coordinator.CountdownRemaining.Should().Be(0);
        Coordinator.Phase.Should().Be(CapturePhase.Idle);
        AssertNoErrorToast();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task Countdown_OddValue_CapturesImmediately(int seconds)
    {
        _h.UpdateCapture(c => c with { CountdownSeconds = seconds });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        _h.CountdownTicks.Should().BeEmpty();
    }

    // ---- clipboard and after-capture options ---------------------------------------------------------

    [Fact]
    public async Task ClipboardCopy_CopiesWithoutWritingAFile()
    {
        _h.UpdateCapture(c => c with { ClipboardMode = "Copy" });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Copied);
        outcome.IsSuccess.Should().BeTrue();
        outcome.Record.Should().BeNull();
        _ = _h.Clipboard.Received(1).CopyImageAsync(_h.WindowFrame, Arg.Any<CancellationToken>());
        _h.Output.RelativeFiles().Should().BeEmpty();
        _ = _h.Library.DidNotReceive().AddAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>());
        _h.Toasts.Received(1).Show(ToastKind.Success, "L:Toast_ClipboardCopied", "Elden Ring", null);
    }

    [Fact]
    public async Task ClipboardCopyAndSave_DoesBoth()
    {
        _h.UpdateCapture(c => c with { ClipboardMode = "CopyAndSave" });
        _h.Localization["Toast_CaptureSavedCopied"].Returns("{0} (copied)");

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        outcome.Kind.Should().Be(CaptureOutcomeKind.Saved);
        File.Exists(GameFile()).Should().BeTrue();
        _ = _h.Clipboard.Received(1).CopyImageAsync(_h.WindowFrame, Arg.Any<CancellationToken>());
        _h.Toasts.Received(1).Show(ToastKind.Success, "L:Toast_CaptureSaved", CoordinatorHarness.GameStem + ".png (copied)", null);
    }

    [Fact]
    public async Task AfterCaptureOptions_AreHonored()
    {
        _h.UpdateUi(ui => ui with { SoundEnabled = false, OpenFolderAfterSave = true, AutoPinAfterCapture = true, ShowNotifications = false });

        var outcome = await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _h.Sound.DidNotReceive().PlayCaptureSound();
        _h.Shell.Received(1).RevealInExplorer(outcome.Record!.FilePath);
        _h.Pinned.Received(1).Show(_h.WindowFrame, outcome.Record.FilePath, "Elden Ring");
        _h.Toasts.DidNotReceive().Show(Arg.Any<ToastKind>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task AutoPin_OfAClipboardOnlyCapture_HasNoFile()
    {
        _h.UpdateCapture(c => c with { ClipboardMode = "Copy" });
        _h.UpdateUi(ui => ui with { AutoPinAfterCapture = true, OpenFolderAfterSave = true });

        await Coordinator.CaptureAsync(CaptureMode.Window, Ct);

        _h.Pinned.Received(1).Show(_h.WindowFrame, null, "Elden Ring");
        _h.Shell.DidNotReceive().RevealInExplorer(Arg.Any<string>());
    }
}
