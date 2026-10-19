// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.Concurrent;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CaptureImage.Core.Tests.Pipeline;

/// <summary>
/// A <see cref="CaptureCoordinator"/> wired to NSubstitute fakes. The file writer is real
/// (it is sealed) and saves into a temp directory through <see cref="FakeImageEncoder"/>.
/// Localized strings come back as <c>L:&lt;key&gt;</c> so tests can assert which key was used.
/// </summary>
internal sealed class CoordinatorHarness : IDisposable
{
    public const nint GameWindow = 0x1001;

    public static readonly DateTimeOffset Start = new(2026, 10, 19, 7, 15, 0, TimeSpan.Zero);

    /// <summary>File name stem the default template gives the foreground game at <see cref="Start"/>.</summary>
    public const string GameStem = "Elden Ring_2026-10-19_07-15-00";

    public static readonly HdrInfo HdrDisplay = new(IsHdrActive: true, SdrWhiteNits: 240f, MaxLuminanceNits: 1000f);

    public static readonly MonitorInfo PrimaryMonitor = new(
        0x10, @"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 96, true, HdrInfo.Sdr);

    public static readonly MonitorInfo SecondMonitor = new(
        0x20, @"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), 144, false, HdrInfo.Sdr);

    private readonly ConcurrentQueue<CaptureOutcome> _completed = new();
    private readonly SemaphoreSlim _completedSignal = new(0);
    private readonly ConcurrentQueue<CapturePhase> _phases = new();
    private readonly ConcurrentQueue<int> _countdown = new();
    private AppSettings _settings;

    public CoordinatorHarness()
    {
        _settings = new AppSettings { Capture = new CaptureSettings { OutputDirectory = Output.FullPath } };
        Settings.Current.Returns(_ => _settings);

        Hotkeys.Activate(Arg.Any<HotkeySettings>()).Returns(call => RegistrationsFor(call.Arg<HotkeySettings>()));

        Targets.GetForegroundWindow().Returns(GameWindow);
        Targets.GetTargetAsync(GameWindow, Arg.Any<CancellationToken>()).Returns(Target);

        Displays.GetMonitorForWindow(Arg.Any<nint>()).Returns(PrimaryMonitor);
        Displays.GetMonitorAtCursor().Returns(PrimaryMonitor);
        Displays.GetMonitors().Returns([PrimaryMonitor, SecondMonitor]);

        Engine.CaptureAsync(Arg.Any<CaptureSource>(), Arg.Any<CaptureOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => FrameFor(call.Arg<CaptureSource>()));

        Preview.ShowAsync(Arg.Any<CapturedFrame>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Clipboard.CopyImageAsync(Arg.Any<CapturedFrame>(), Arg.Any<CancellationToken>()).Returns(true);
        Performance.BeginForegroundWork().Returns(ForegroundWork);

        Localization.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        Localization[Arg.Any<string>()].Returns(call => "L:" + call.Arg<string>());

        Writer = new CaptureFileWriter([Encoder], new FileNameStrategy(File.Exists), NullLogger<CaptureFileWriter>.Instance);
        Coordinator = new CaptureCoordinator(
            Settings,
            Hotkeys,
            Windows,
            Targets,
            Engine,
            Displays,
            RegionSelector,
            Preview,
            Writer,
            Library,
            Clipboard,
            Pinned,
            Toasts,
            Sound,
            Shell,
            Performance,
            Localization,
            Time,
            NullLogger<CaptureCoordinator>.Instance);

        Coordinator.StateChanged += (_, _) =>
        {
            var phase = Coordinator.Phase;
            _phases.Enqueue(phase);
            if (phase == CapturePhase.Countdown)
            {
                _countdown.Enqueue(Coordinator.CountdownRemaining);
            }
        };
        Coordinator.CaptureCompleted += (_, outcome) =>
        {
            _completed.Enqueue(outcome);
            _completedSignal.Release();
        };
    }

    public TempDirectory Output { get; } = new();

    public SignalingTimeProvider Time { get; } = new(Start);

    public FakeImageEncoder Encoder { get; } = new();

    public GameTarget Target { get; } = TestTargets.Game(windowHandle: GameWindow);

    public CapturedFrame WindowFrame { get; set; } = Frames.SolidBgra(64, 36, new BgraPixel(40, 50, 60, 255));

    /// <summary>What each monitor shows: pixels encode (x, y, monitor handle).</summary>
    public Dictionary<nint, CapturedFrame> MonitorFrames { get; } = new()
    {
        [PrimaryMonitor.Handle] = MonitorPicture(PrimaryMonitor.Handle),
        [SecondMonitor.Handle] = MonitorPicture(SecondMonitor.Handle),
    };

    public ISettingsStore Settings { get; } = Substitute.For<ISettingsStore>();

    public IHotkeyService Hotkeys { get; } = Substitute.For<IHotkeyService>();

    public IWindowWatcher Windows { get; } = Substitute.For<IWindowWatcher>();

    public ITargetCatalog Targets { get; } = Substitute.For<ITargetCatalog>();

    public ICaptureEngine Engine { get; } = Substitute.For<ICaptureEngine>();

    public IDisplayService Displays { get; } = Substitute.For<IDisplayService>();

    public IRegionSelector RegionSelector { get; } = Substitute.For<IRegionSelector>();

    public IPreviewPresenter Preview { get; } = Substitute.For<IPreviewPresenter>();

    public ICaptureLibrary Library { get; } = Substitute.For<ICaptureLibrary>();

    public IClipboardService Clipboard { get; } = Substitute.For<IClipboardService>();

    public IPinnedThumbnailHost Pinned { get; } = Substitute.For<IPinnedThumbnailHost>();

    public IToastService Toasts { get; } = Substitute.For<IToastService>();

    public ISoundService Sound { get; } = Substitute.For<ISoundService>();

    public IShellService Shell { get; } = Substitute.For<IShellService>();

    public IPerformanceModeService Performance { get; } = Substitute.For<IPerformanceModeService>();

    public IDisposable ForegroundWork { get; } = Substitute.For<IDisposable>();

    public ILocalizationService Localization { get; } = Substitute.For<ILocalizationService>();

    public CaptureFileWriter Writer { get; }

    public CaptureCoordinator Coordinator { get; }

    public AppSettings CurrentSettings => _settings;

    /// <summary>Phases seen through <see cref="CaptureCoordinator.StateChanged"/>, in order.</summary>
    public IReadOnlyList<CapturePhase> Phases => [.. _phases];

    /// <summary>Countdown values seen while counting down, in order.</summary>
    public IReadOnlyList<int> CountdownTicks => [.. _countdown];

    /// <summary>Outcomes raised through <see cref="CaptureCoordinator.CaptureCompleted"/>.</summary>
    public IReadOnlyList<CaptureOutcome> Completed => [.. _completed];

    public string OutputFile(string fileName) => Path.Combine(Output.FullPath, fileName);

    /// <summary>Change the settings; by default also raise <see cref="ISettingsStore.Changed"/> like the store does.</summary>
    public void UpdateSettings(Func<AppSettings, AppSettings> change, bool raiseChanged = true)
    {
        _settings = change(_settings);
        if (raiseChanged)
        {
            Settings.Changed += Raise.Event();
        }
    }

    public void UpdateCapture(Func<CaptureSettings, CaptureSettings> change) =>
        UpdateSettings(s => s with { Capture = change(s.Capture) }, raiseChanged: false);

    public void UpdateUi(Func<UiSettings, UiSettings> change) =>
        UpdateSettings(s => s with { UI = change(s.UI) }, raiseChanged: false);

    /// <summary>Wait for the next <see cref="CaptureCoordinator.CaptureCompleted"/> (for hotkey-triggered captures).</summary>
    public async Task<CaptureOutcome> NextCompletionAsync(CancellationToken cancellationToken)
    {
        if (!await _completedSignal.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
        {
            throw new TimeoutException("No capture completed.");
        }

        return _completed.Last();
    }

    public void ClearHistory()
    {
        _phases.Clear();
        _countdown.Clear();
    }

    public static IReadOnlyList<HotkeyRegistration> RegistrationsFor(HotkeySettings hotkeys) =>
    [
        .. Enum.GetValues<CaptureMode>().Select(mode =>
        {
            var binding = hotkeys.Get(mode);
            return new HotkeyRegistration(
                mode,
                binding,
                binding.IsUnbound ? HotkeyRegistrationStatus.Unbound : HotkeyRegistrationStatus.Registered);
        }),
    ];

    public void Dispose()
    {
        Coordinator.Dispose();
        _completedSignal.Dispose();
        Time.Dispose();
        Output.Dispose();
    }

    private static CapturedFrame MonitorPicture(nint handle) =>
        Frames.CreateBgra(16, 12, (x, y) => new BgraPixel((byte)x, (byte)y, (byte)handle, 255));

    private CapturedFrame FrameFor(CaptureSource source) => source switch
    {
        WindowCaptureSource => WindowFrame,
        MonitorCaptureSource monitor => MonitorFrames[monitor.MonitorHandle],
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
