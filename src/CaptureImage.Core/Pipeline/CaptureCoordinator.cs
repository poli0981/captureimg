// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Profiles;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Core.Pipeline;

/// <summary>What the coordinator is doing right now.</summary>
public enum CapturePhase
{
    Idle,
    Countdown,
    Selecting,
    Capturing,
    Previewing,
    Saving,
}

/// <summary>
/// Owns arming and runs every capture: hotkey or button → countdown → capture (window,
/// monitor, or region via frozen monitor snapshots) → HDR tone-mapping → optional preview →
/// save / clipboard → library, sound, pin, notification.
/// </summary>
/// <remarks>
/// <para>One capture runs at a time; requests while busy return
/// <see cref="CaptureOutcomeKind.Busy"/> instead of throwing. Nothing here throws to the
/// hotkey path — every failure becomes a <see cref="CaptureOutcome"/> plus a notification.</para>
/// <para>Thread-agnostic: events are raised on whatever thread made the change; view models
/// marshal to the UI thread themselves.</para>
/// </remarks>
public sealed partial class CaptureCoordinator : IDisposable
{
    private static readonly string SoftwareName =
        "CaptureImage " + (typeof(CaptureCoordinator).Assembly.GetName().Version?.ToString(3) ?? "dev");

    private readonly ISettingsStore _settings;
    private readonly IHotkeyService _hotkeys;
    private readonly IWindowWatcher _windows;
    private readonly ITargetCatalog _targets;
    private readonly ICaptureEngine _engine;
    private readonly IDisplayService _displays;
    private readonly IRegionSelector _regionSelector;
    private readonly IPreviewPresenter _preview;
    private readonly CaptureFileWriter _writer;
    private readonly ICaptureLibrary _library;
    private readonly IClipboardService _clipboard;
    private readonly IPinnedThumbnailHost _pinned;
    private readonly IToastService _toasts;
    private readonly ISoundService _sound;
    private readonly IShellService _shell;
    private readonly IPerformanceModeService _performance;
    private readonly ILocalizationService _localization;
    private readonly TimeProvider _time;
    private readonly ILogger<CaptureCoordinator> _logger;

    private readonly Lock _gate = new();
    private int _busy;
    private bool _armed;
    private bool _disposed;
    private GameTarget? _lockedTarget;
    private CapturePhase _phase;
    private int _countdownRemaining;
    private int _suspendCount;
    private HotkeySettings? _activeHotkeys;
    private IReadOnlyList<HotkeyRegistration> _registrations = [];
    private CancellationTokenSource? _current;

    public CaptureCoordinator(
        ISettingsStore settings,
        IHotkeyService hotkeys,
        IWindowWatcher windows,
        ITargetCatalog targets,
        ICaptureEngine engine,
        IDisplayService displays,
        IRegionSelector regionSelector,
        IPreviewPresenter preview,
        CaptureFileWriter writer,
        ICaptureLibrary library,
        IClipboardService clipboard,
        IPinnedThumbnailHost pinned,
        IToastService toasts,
        ISoundService sound,
        IShellService shell,
        IPerformanceModeService performance,
        ILocalizationService localization,
        TimeProvider time,
        ILogger<CaptureCoordinator> logger)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _windows = windows;
        _targets = targets;
        _engine = engine;
        _displays = displays;
        _regionSelector = regionSelector;
        _preview = preview;
        _writer = writer;
        _library = library;
        _clipboard = clipboard;
        _pinned = pinned;
        _toasts = toasts;
        _sound = sound;
        _shell = shell;
        _performance = performance;
        _localization = localization;
        _time = time;
        _logger = logger;

        _hotkeys.Triggered += OnHotkeyTriggered;
        _windows.ForegroundChanged += OnForegroundChanged;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>Arming, phase, countdown, locked target or hotkey registrations changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>A capture request finished (any outcome except <see cref="CaptureOutcomeKind.Busy"/>).</summary>
    public event EventHandler<CaptureOutcome>? CaptureCompleted;

    public bool IsArmed
    {
        get { lock (_gate) return _armed; }
    }

    public CapturePhase Phase
    {
        get { lock (_gate) return _phase; }
    }

    public bool IsBusy => Volatile.Read(ref _busy) == 1;

    /// <summary>Seconds left in the countdown (0 when not counting down).</summary>
    public int CountdownRemaining
    {
        get { lock (_gate) return _countdownRemaining; }
    }

    /// <summary>Window the Window hotkey captures; <c>null</c> = the foreground window.</summary>
    public GameTarget? LockedTarget
    {
        get { lock (_gate) return _lockedTarget; }
    }

    public IReadOnlyList<HotkeyRegistration> Registrations
    {
        get { lock (_gate) return _registrations; }
    }

    /// <summary>Register the capture hotkeys. Returns the per-mode registration results.</summary>
    public IReadOnlyList<HotkeyRegistration> Arm()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var hotkeys = _settings.Current.Hotkeys;
        bool suspended;
        lock (_gate)
        {
            suspended = _suspendCount > 0;
        }

        // While suspended (hotkey recorder open) only remember the request; resuming registers.
        IReadOnlyList<HotkeyRegistration> registrations = suspended ? [] : _hotkeys.Activate(hotkeys);
        lock (_gate)
        {
            _armed = true;
            _activeHotkeys = hotkeys;
            _registrations = registrations;
        }

        LogArmed(string.Join(", ", registrations.Select(r => $"{r.Mode}={r.Binding}:{r.Status}")));
        RaiseStateChanged();
        return registrations;
    }

    /// <summary>Release the hotkeys so the combinations reach other applications again.</summary>
    public void Disarm()
    {
        if (_disposed)
        {
            return;
        }

        _hotkeys.Deactivate();
        lock (_gate)
        {
            _armed = false;
            _activeHotkeys = null;
            _registrations = [];
        }

        LogDisarmed();
        RaiseStateChanged();
    }

    /// <summary>
    /// Release the hotkeys while the user records a new combination: a registered combination
    /// never reaches any window, including our own recorder. Dispose to register them again.
    /// </summary>
    public IDisposable SuspendHotkeys()
    {
        bool release;
        lock (_gate)
        {
            _suspendCount++;
            release = _armed && _suspendCount == 1;
        }

        if (release)
        {
            _hotkeys.Deactivate();
            lock (_gate)
            {
                _registrations = [];
            }
            RaiseStateChanged();
        }

        return new HotkeySuspension(this);
    }

    private void ResumeHotkeys()
    {
        bool rearm;
        lock (_gate)
        {
            _suspendCount = Math.Max(0, _suspendCount - 1);
            rearm = _armed && _suspendCount == 0 && !_disposed;
        }

        if (rearm)
        {
            Arm();
        }
    }

    /// <summary>Pin the Window-mode target; <c>null</c> captures the foreground window.</summary>
    public void LockTarget(GameTarget? target)
    {
        lock (_gate)
        {
            if (_lockedTarget?.WindowHandle == target?.WindowHandle && _lockedTarget?.WindowTitle == target?.WindowTitle)
            {
                return;
            }
            _lockedTarget = target;
        }

        RaiseStateChanged();
    }

    /// <summary>Cancel the running countdown / selection / preview, if any.</summary>
    public void CancelCurrent()
    {
        CancellationTokenSource? current;
        lock (_gate)
        {
            current = _current;
        }
        try
        {
            current?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The capture finished in the meantime.
        }
    }

    /// <summary>Run one capture. Never throws; the outcome says what happened.</summary>
    public Task<CaptureOutcome> CaptureAsync(CaptureMode mode, CancellationToken cancellationToken = default) =>
        CaptureAsync(mode, target: null, cancellationToken);

    /// <summary>
    /// Run one capture; for <see cref="CaptureMode.Window"/>, <paramref name="target"/> overrides
    /// the locked / foreground window (the Dashboard's "Capture now" button).
    /// </summary>
    public async Task<CaptureOutcome> CaptureAsync(CaptureMode mode, GameTarget? target, CancellationToken cancellationToken = default)
    {
        if (_disposed || Interlocked.Exchange(ref _busy, 1) == 1)
        {
            LogBusy(mode);
            return new CaptureOutcome(CaptureOutcomeKind.Busy, mode);
        }

        var started = _time.GetTimestamp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_gate)
        {
            _current = cts;
        }

        CaptureOutcome outcome;
        try
        {
            using (_performance.BeginForegroundWork())
            {
                outcome = await RunAsync(mode, target, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            LogCancelled(mode);
            outcome = new CaptureOutcome(CaptureOutcomeKind.Cancelled, mode);
        }
        catch (CaptureException ex)
        {
            outcome = Fail(mode, ex.ErrorCode, ex);
        }
        catch (Exception ex)
        {
            outcome = Fail(mode, CaptureError.Unknown, ex);
        }
        finally
        {
            lock (_gate)
            {
                _current = null;
                _countdownRemaining = 0;
                _phase = CapturePhase.Idle;
            }
            Volatile.Write(ref _busy, 0);
            RaiseStateChanged();
        }

        outcome = outcome with { Duration = _time.GetElapsedTime(started) };
        CaptureCompleted?.Invoke(this, outcome);
        return outcome;
    }

    private async Task<CaptureOutcome> RunAsync(CaptureMode mode, GameTarget? explicitTarget, CancellationToken ct)
    {
        var settings = _settings.Current;
        var context = explicitTarget is not null
            ? await _targets.GetTargetAsync(explicitTarget.WindowHandle, ct).ConfigureAwait(false)
                ?? throw new CaptureException(CaptureError.TargetGone, "The selected window was closed.")
            : await ResolveContextAsync(mode, ct).ConfigureAwait(false);
        var profile = ProfileResolver.Resolve(settings.Profiles, context);
        var title = context?.DisplayName ?? ModeName(mode);

        var countdown = SettingValues.NormalizeCountdown(settings.Capture.CountdownSeconds);
        if (countdown > 0)
        {
            await CountdownAsync(countdown, settings.UI.ShowNotifications, ct).ConfigureAwait(false);
        }

        var options = new CaptureOptions(
            PreferHdr: SettingValues.IsHdrEnabled(profile?.HdrMode ?? settings.Capture.HdrMode),
            IncludeCursor: settings.Capture.IncludeCursor,
            IncludeWindowFrame: settings.Capture.IncludeWindowFrame);

        var frame = mode switch
        {
            CaptureMode.Window => await CaptureWindowAsync(context, options, ct).ConfigureAwait(false),
            CaptureMode.Monitor => await CaptureMonitorAsync(options, ct).ConfigureAwait(false),
            CaptureMode.Region => await CaptureRegionAsync(options, ct).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        if (frame is null)
        {
            LogCancelled(mode);
            return new CaptureOutcome(CaptureOutcomeKind.Cancelled, mode);
        }

        var displayFrame = frame.IsHdr ? HdrToneMapper.ToSdr(frame) : frame;

        if (settings.Capture.PreviewBeforeSave)
        {
            SetPhase(CapturePhase.Previewing);
            if (!await _preview.ShowAsync(displayFrame, title, ct).ConfigureAwait(false))
            {
                LogDiscarded(mode);
                return new CaptureOutcome(CaptureOutcomeKind.Discarded, mode);
            }
        }

        SetPhase(CapturePhase.Saving);
        var capturedAt = _time.GetLocalNow();
        var clipboardMode = SettingValues.ParseClipboardMode(settings.Capture.ClipboardMode);

        CaptureRecord? record = null;
        if (clipboardMode != ClipboardMode.Copy)
        {
            record = await _writer.SaveAsync(
                new SaveRequest(
                    displayFrame,
                    frame.IsHdr && settings.Capture.SaveHdrSidecar ? frame : null,
                    ResolveOutputDirectory(settings, profile),
                    string.IsNullOrWhiteSpace(profile?.FileNameTemplate) ? settings.Capture.FileNameTemplate : profile.FileNameTemplate,
                    profile?.Format ?? settings.Capture.DefaultFormat,
                    new EncoderSettings(
                        settings.Capture.JpegQuality,
                        settings.Capture.WebpQuality,
                        SettingValues.ParsePngCompression(settings.Capture.PngCompression),
                        new ImageMetadata(context?.DisplayName, SoftwareName, capturedAt)),
                    context,
                    mode,
                    capturedAt),
                ct).ConfigureAwait(false);
            LogSaved(mode, record.FileName, record.Width, record.Height, record.SizeBytes);
            await AddToLibraryAsync(record).ConfigureAwait(false);
        }

        var copied = clipboardMode != ClipboardMode.None
            && await _clipboard.CopyImageAsync(displayFrame, CancellationToken.None).ConfigureAwait(false);

        AfterCapture(settings, displayFrame, record, copied, title);
        return new CaptureOutcome(record is null ? CaptureOutcomeKind.Copied : CaptureOutcomeKind.Saved, mode) { Record = record };
    }

    private async Task<GameTarget?> ResolveContextAsync(CaptureMode mode, CancellationToken ct)
    {
        var locked = LockedTarget;
        if (locked is not null)
        {
            // Refresh title / minimized state; drop the lock if the window is gone.
            var refreshed = await _targets.GetTargetAsync(locked.WindowHandle, ct).ConfigureAwait(false);
            if (refreshed is not null)
            {
                return refreshed;
            }

            LockTarget(null);
            if (mode == CaptureMode.Window)
            {
                throw new CaptureException(CaptureError.TargetGone, "The locked window was closed.");
            }
        }

        var foreground = _targets.GetForegroundWindow();
        return foreground == 0 ? null : await _targets.GetTargetAsync(foreground, ct).ConfigureAwait(false);
    }

    private async Task CountdownAsync(int seconds, bool notify, CancellationToken ct)
    {
        if (notify)
        {
            _toasts.Show(ToastKind.Info, _localization["Toast_CountdownTitle"], _localization.Format("Toast_Countdown", seconds), TimeSpan.FromSeconds(seconds));
        }

        for (var remaining = seconds; remaining > 0; remaining--)
        {
            lock (_gate)
            {
                _phase = CapturePhase.Countdown;
                _countdownRemaining = remaining;
            }
            RaiseStateChanged();
            await Task.Delay(TimeSpan.FromSeconds(1), _time, ct).ConfigureAwait(false);
        }
    }

    private async Task<CapturedFrame> CaptureWindowAsync(GameTarget? target, CaptureOptions options, CancellationToken ct)
    {
        if (target is null)
        {
            throw new CaptureException(CaptureError.NoTarget, "No window is locked and no capturable window is in the foreground.");
        }

        if (target.IsMinimized)
        {
            throw new CaptureException(CaptureError.TargetMinimized, "The window is minimized.");
        }

        SetPhase(CapturePhase.Capturing);
        var monitor = _displays.GetMonitorForWindow(target.WindowHandle);
        var effective = options with { PreferHdr = options.PreferHdr && monitor?.Hdr.IsHdrActive == true };
        try
        {
            var frame = await _engine.CaptureAsync(new WindowCaptureSource(target.WindowHandle), effective, ct).ConfigureAwait(false);
            return WithHdrInfo(frame, monitor);
        }
        catch (CaptureException ex) when (ex.ErrorCode == CaptureError.TargetGone && LockedTarget?.WindowHandle == target.WindowHandle)
        {
            LockTarget(null);
            throw;
        }
    }

    private async Task<CapturedFrame> CaptureMonitorAsync(CaptureOptions options, CancellationToken ct)
    {
        SetPhase(CapturePhase.Capturing);
        var monitor = _displays.GetMonitorAtCursor()
            ?? throw new CaptureException(CaptureError.TargetGone, "No monitor found under the cursor.");
        var effective = options with { PreferHdr = options.PreferHdr && monitor.Hdr.IsHdrActive, IncludeWindowFrame = true };
        var frame = await _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), effective, ct).ConfigureAwait(false);
        return WithHdrInfo(frame, monitor);
    }

    /// <summary>
    /// Freeze every monitor first, then let the user drag on the frozen pictures — what they
    /// select is exactly what was on screen when they pressed the hotkey.
    /// </summary>
    private async Task<CapturedFrame?> CaptureRegionAsync(CaptureOptions options, CancellationToken ct)
    {
        SetPhase(CapturePhase.Capturing);
        var frames = new List<(MonitorInfo Monitor, CapturedFrame Frame)>();
        foreach (var monitor in _displays.GetMonitors())
        {
            try
            {
                var effective = options with { PreferHdr = options.PreferHdr && monitor.Hdr.IsHdrActive, IncludeWindowFrame = true };
                var frame = await _engine.CaptureAsync(new MonitorCaptureSource(monitor.Handle), effective, ct).ConfigureAwait(false);
                frames.Add((monitor, WithHdrInfo(frame, monitor)));
            }
            catch (CaptureException ex)
            {
                LogMonitorSkipped(ex, monitor.DeviceName);
            }
        }

        if (frames.Count == 0)
        {
            throw new CaptureException(CaptureError.NoFrameArrived, "None of the monitors could be captured.");
        }

        var snapshots = frames
            .Select(f => new MonitorSnapshot(f.Monitor, f.Frame.IsHdr ? HdrToneMapper.ToSdr(f.Frame) : f.Frame))
            .ToList();

        SetPhase(CapturePhase.Selecting);
        var selection = await _regionSelector.SelectAsync(snapshots, ct).ConfigureAwait(false);
        if (selection is null)
        {
            return null;
        }

        SetPhase(CapturePhase.Capturing);
        var source = frames.First(f => f.Monitor.Handle == selection.Monitor.Handle).Frame;
        return FrameOperations.Crop(source, selection.Area);
    }

    /// <summary>HDR frames carry the display's white level so tone-mapping matches what the user saw.</summary>
    private static CapturedFrame WithHdrInfo(CapturedFrame frame, MonitorInfo? monitor) =>
        frame.IsHdr && monitor is not null ? frame with { Hdr = monitor.Hdr } : frame;

    private static string ResolveOutputDirectory(AppSettings settings, GameProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(profile?.OutputDirectory))
        {
            return profile.OutputDirectory;
        }

        return string.IsNullOrWhiteSpace(settings.Capture.OutputDirectory)
            ? AppPaths.DefaultOutputDirectory
            : settings.Capture.OutputDirectory;
    }

    private async Task AddToLibraryAsync(CaptureRecord record)
    {
        try
        {
            await _library.AddAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The file is saved; a stale library index is not worth failing the capture for.
            LogLibraryFailed(ex);
        }
    }

    private void AfterCapture(AppSettings settings, CapturedFrame frame, CaptureRecord? record, bool copied, string title)
    {
        if (settings.UI.SoundEnabled)
        {
            _sound.PlayCaptureSound();
        }

        if (record is not null && settings.UI.OpenFolderAfterSave)
        {
            _shell.RevealInExplorer(record.FilePath);
        }

        if (settings.UI.AutoPinAfterCapture)
        {
            _pinned.Show(frame, record?.FilePath, title);
        }

        if (!settings.UI.ShowNotifications)
        {
            return;
        }

        if (record is not null)
        {
            var message = copied
                ? _localization.Format("Toast_CaptureSavedCopied", record.FileName)
                : record.FileName;
            _toasts.Show(ToastKind.Success, _localization["Toast_CaptureSaved"], message);
        }
        else if (copied)
        {
            _toasts.Show(ToastKind.Success, _localization["Toast_ClipboardCopied"], title);
        }
    }

    private CaptureOutcome Fail(CaptureMode mode, CaptureError error, Exception exception)
    {
        if (error == CaptureError.Cancelled)
        {
            return new CaptureOutcome(CaptureOutcomeKind.Cancelled, mode);
        }

        if (error == CaptureError.Unknown)
        {
            LogUnexpectedFailure(exception, mode);
        }
        else
        {
            LogFailed(exception, mode, error);
        }

        // Errors are always shown: a silent failure while gaming is worse than a pop-up.
        _toasts.Show(ToastKind.Error, _localization["Toast_CaptureFailed"], _localization[$"Error_{error}"]);
        return new CaptureOutcome(CaptureOutcomeKind.Failed, mode) { Error = error, ErrorMessage = exception.Message };
    }

    private string ModeName(CaptureMode mode) => _localization[$"Mode_{mode}"];

    private void SetPhase(CapturePhase phase)
    {
        lock (_gate)
        {
            if (_phase == phase)
            {
                return;
            }
            _phase = phase;
        }
        RaiseStateChanged();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OnHotkeyTriggered(object? sender, CaptureMode mode) => _ = CaptureAsync(mode);

    private void OnForegroundChanged(object? sender, nint windowHandle) => _ = AutoArmAsync(windowHandle);

    /// <summary>Arm (and lock the target) when a game with an auto-arm profile comes to the front.</summary>
    private async Task AutoArmAsync(nint windowHandle)
    {
        try
        {
            var profiles = _settings.Current.Profiles;
            if (_disposed || windowHandle == 0 || !profiles.Any(p => p.Enabled && p.AutoArm))
            {
                return;
            }

            var target = await _targets.GetTargetAsync(windowHandle).ConfigureAwait(false);
            var profile = ProfileResolver.Resolve(profiles, target);
            if (target is null || profile is not { AutoArm: true })
            {
                return;
            }

            if (!IsArmed)
            {
                Arm();
                LogAutoArmed(profile.Name);
                if (_settings.Current.UI.ShowNotifications)
                {
                    _toasts.Show(ToastKind.Info, _localization["Toast_AutoArmed"], profile.Name);
                }
            }

            if (profile.LockTarget)
            {
                LockTarget(target);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogAutoArmFailed(ex);
        }
    }

    /// <summary>Re-register when the hotkey bindings change while armed.</summary>
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        HotkeySettings? active;
        lock (_gate)
        {
            if (!_armed)
            {
                return;
            }
            active = _activeHotkeys;
        }

        if (!Equals(active, _settings.Current.Hotkeys))
        {
            Arm();
        }
    }

    private sealed class HotkeySuspension(CaptureCoordinator owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.ResumeHotkeys();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _hotkeys.Triggered -= OnHotkeyTriggered;
        _windows.ForegroundChanged -= OnForegroundChanged;
        _settings.Changed -= OnSettingsChanged;
        CancelCurrent();
        _hotkeys.Deactivate();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Armed: {Registrations}.")]
    private partial void LogArmed(string registrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disarmed.")]
    private partial void LogDisarmed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Mode} capture ignored: another capture is still running.")]
    private partial void LogBusy(CaptureMode mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Mode} capture cancelled.")]
    private partial void LogCancelled(CaptureMode mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Mode} capture discarded in preview.")]
    private partial void LogDiscarded(CaptureMode mode);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Mode} capture saved: {FileName} ({Width}x{Height}, {Size} bytes).")]
    private partial void LogSaved(CaptureMode mode, string fileName, int width, int height, long size);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Mode} capture failed: {Error}.")]
    private partial void LogFailed(Exception exception, CaptureMode mode, CaptureError error);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Mode} capture failed unexpectedly.")]
    private partial void LogUnexpectedFailure(Exception exception, CaptureMode mode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping monitor {Device} for region capture.")]
    private partial void LogMonitorSkipped(Exception exception, string device);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add the capture to the library index.")]
    private partial void LogLibraryFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-armed for profile {Profile}.")]
    private partial void LogAutoArmed(string profile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-arm check failed.")]
    private partial void LogAutoArmFailed(Exception exception);
}
