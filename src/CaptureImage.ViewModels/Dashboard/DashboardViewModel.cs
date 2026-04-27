using System.Collections.ObjectModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.StateMachine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Dashboard;

/// <summary>
/// Dashboard tab. Ties together the live process list, the capture state machine,
/// settings, hotkey triggers, the capture engine, the preview gate, and toast feedback.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan AutoResetDelay = TimeSpan.FromMilliseconds(800);

    private readonly IProcessDetector _detector;
    private readonly IProcessWatcher _watcher;
    private readonly IForegroundWindowWatcher _foregroundWatcher;
    private readonly IHotkeyService _hotkeys;
    private readonly ICaptureEngine _captureEngine;
    private readonly IReadOnlyList<IImageEncoder> _encoders;
    private readonly FileNameStrategy _fileNameStrategy;
    private readonly IPreviewPresenter _previewPresenter;
    private readonly ISettingsStore _settings;
    private readonly IToastService _toasts;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly CaptureStateMachine _stateMachine;

    private CancellationTokenSource? _pendingRefresh;
    private bool _disposed;

    public ILocalizationService Localization { get; }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private CaptureState _currentState = CaptureState.Idle;

    [ObservableProperty]
    private GameTargetViewModel? _selectedTarget;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _lastCapturePath;

    public ObservableCollection<GameTargetViewModel> Targets { get; } = new();

    /// <summary>Localized "{0} target(s) visible" line at the bottom of the dashboard.</summary>
    public string TargetsCountText =>
        string.Format(Localization["Dashboard_TargetsCount"], Targets.Count);

    /// <summary>Localized "Loading…" hint shown while the process enumerator is running.</summary>
    public string LoadingText => Localization["Dashboard_Loading"];

    /// <summary>Localized hint shown when the target list is empty (no visible windows).</summary>
    public string EmptyStateText => Localization["Dashboard_EmptyState"];

    public bool HasNoTargets => Targets.Count == 0 && !IsLoading;

    public bool IsArmed => CurrentState is CaptureState.Armed or CaptureState.Capturing
                                         or CaptureState.Previewing or CaptureState.Saving
                                         or CaptureState.Complete or CaptureState.Failed;

    public bool IsCapturing => CurrentState is CaptureState.Capturing or CaptureState.Saving;

    public DashboardViewModel(
        IProcessDetector detector,
        IProcessWatcher watcher,
        IForegroundWindowWatcher foregroundWatcher,
        IHotkeyService hotkeys,
        ICaptureEngine captureEngine,
        IEnumerable<IImageEncoder> encoders,
        FileNameStrategy fileNameStrategy,
        IPreviewPresenter previewPresenter,
        ISettingsStore settings,
        IToastService toasts,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        ILogger<DashboardViewModel> logger)
    {
        _detector = detector;
        _watcher = watcher;
        _foregroundWatcher = foregroundWatcher;
        _hotkeys = hotkeys;
        _captureEngine = captureEngine;
        _encoders = new List<IImageEncoder>(encoders).AsReadOnly();
        _fileNameStrategy = fileNameStrategy;
        _previewPresenter = previewPresenter;
        _settings = settings;
        _toasts = toasts;
        _dispatcher = dispatcher;
        Localization = localization;
        _logger = logger;

        _stateMachine = new CaptureStateMachine(() => _settings.Current.Capture.PreviewBeforeSave);
        _stateMachine.StateChanged += OnStateChanged;
        StatusMessage = Localization["Dashboard_StatusIdle"];

        _watcher.Changed += OnProcessChanged;
        _watcher.Start();
        _hotkeys.Triggered += OnHotkeyTriggered;
        _settings.Changed += OnSettingsChanged;
        _foregroundWatcher.ForegroundChanged += OnForegroundChanged;
        ApplyAutoSwitchSetting();

        Localization.PropertyChanged += OnLocalizationChanged;

        // TargetsCountText depends on Targets.Count — refresh whenever the collection changes.
        Targets.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TargetsCountText));
            OnPropertyChanged(nameof(HasNoTargets));
        };

        // Fire the first refresh without blocking the constructor.
        _ = RefreshAsync();
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            // StatusMessage is a stored [ObservableProperty], not a computed getter —
            // re-raising PropertyChanged alone would hand the UI the same stale string.
            // Re-derive it from the state machine so the new culture's template wins.
            UpdateStatusForState();
            OnPropertyChanged(nameof(TargetsCountText));
            OnPropertyChanged(nameof(LoadingText));
            OnPropertyChanged(nameof(EmptyStateText));
            // Forces `{Binding Localization[Key]}` bindings on DashboardView to
            // re-resolve the indexer path in-place (Title, Subtitle, Refresh / Arm /
            // Disarm button labels). See v1.1.1 hotfix notes.
            OnPropertyChanged(nameof(Localization));
        }
    }

    // HasNoTargets depends on IsLoading — refresh when the loading flag flips.
    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoTargets));
    }

    // -- nav / refresh -------------------------------------------------------

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_disposed) return;
        try
        {
            IsLoading = true;
            var targets = await _detector.EnumerateTargetsAsync().ConfigureAwait(true);
            ApplyTargets(targets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate capture targets.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyTargets(IReadOnlyList<GameTarget> newTargets)
    {
        var previousPid = SelectedTarget?.ProcessId;

        foreach (var stale in Targets)
        {
            stale.Dispose();
        }
        Targets.Clear();
        foreach (var target in newTargets)
        {
            Targets.Add(new GameTargetViewModel(target, Localization));
        }

        if (previousPid is { } pid)
        {
            foreach (var vm in Targets)
            {
                if (vm.ProcessId == pid)
                {
                    SelectedTarget = vm;
                    break;
                }
            }
        }

        TrySyncTargetState();
        _logger.LogDebug("Dashboard reconciled to {Count} target(s).", Targets.Count);
    }

    /// <summary>
    /// Drive <see cref="CaptureStateMachine"/> to reflect the current selection. Called on
    /// SelectedTarget and Targets changes so state transitions happen at the right moment.
    /// </summary>
    private void TrySyncTargetState()
    {
        if (_disposed) return;

        var hasTarget = SelectedTarget is not null;
        switch (_stateMachine.CurrentState)
        {
            case CaptureState.Idle when hasTarget:
                _stateMachine.Fire(CaptureTrigger.SelectTargets);
                break;
            case CaptureState.TargetsSelected when !hasTarget:
                _stateMachine.Fire(CaptureTrigger.ClearTargets);
                break;
        }
    }

    // -- arm / disarm --------------------------------------------------------

    [RelayCommand(CanExecute = nameof(CanArm))]
    private void Arm()
    {
        if (!_stateMachine.CanFire(CaptureTrigger.Arm)) return;
        _hotkeys.SetBinding(_settings.Current.CaptureHotkey);
        _stateMachine.Fire(CaptureTrigger.Arm);
    }

    [RelayCommand(CanExecute = nameof(CanDisarm))]
    private void Disarm()
    {
        if (!_stateMachine.CanFire(CaptureTrigger.Disarm)) return;
        _hotkeys.Stop();
        _stateMachine.Fire(CaptureTrigger.Disarm);
    }

    private bool CanArm() =>
        SelectedTarget is not null && _stateMachine.CanFire(CaptureTrigger.Arm);

    private bool CanDisarm() =>
        _stateMachine.CanFire(CaptureTrigger.Disarm);

    // -- state machine observer ---------------------------------------------

    private void OnStateChanged(object? sender, CaptureStateChanged e)
    {
        _dispatcher.Post(() =>
        {
            CurrentState = e.To;
            OnPropertyChanged(nameof(IsArmed));
            OnPropertyChanged(nameof(IsCapturing));
            UpdateStatusForState();
            ArmCommand.NotifyCanExecuteChanged();
            DisarmCommand.NotifyCanExecuteChanged();
        });

        // Schedule an auto-reset from Complete/Failed back to Armed.
        if (e.To is CaptureState.Complete or CaptureState.Failed)
        {
            _ = AutoResetAsync();
        }
    }

    private async Task AutoResetAsync()
    {
        try
        {
            await Task.Delay(AutoResetDelay).ConfigureAwait(false);
            _dispatcher.Post(() =>
            {
                if (_stateMachine.CanFire(CaptureTrigger.Reset))
                {
                    _stateMachine.Fire(CaptureTrigger.Reset);
                }
            });
        }
        catch
        {
            // ignore
        }
    }

    private void UpdateStatusForState()
    {
        StatusMessage = _stateMachine.CurrentState switch
        {
            CaptureState.Idle            => Localization["Dashboard_StatusIdle"],
            CaptureState.TargetsSelected => Localization["Dashboard_StatusDisarmed"],
            CaptureState.Armed           => string.Format(
                Localization["Dashboard_StatusArmed"],
                _settings.Current.CaptureHotkey,
                SelectedTarget?.DisplayName ?? "?"),
            CaptureState.Capturing       => string.Format(
                Localization["Dashboard_StatusCapturing"],
                SelectedTarget?.DisplayName ?? "?"),
            CaptureState.Previewing      => Localization["Preview_Prompt"],
            _                            => StatusMessage,
        };
    }

    // -- selection / property changed hooks ---------------------------------

    partial void OnSelectedTargetChanged(GameTargetViewModel? value)
    {
        TrySyncTargetState();
        ArmCommand.NotifyCanExecuteChanged();
        DisarmCommand.NotifyCanExecuteChanged();

        if (_stateMachine.CurrentState == CaptureState.Armed)
        {
            UpdateStatusForState();
        }
    }

    // -- hotkey trigger ------------------------------------------------------

    private void OnHotkeyTriggered(object? sender, EventArgs e)
    {
        _dispatcher.Post(async () => await CaptureOnceAsync().ConfigureAwait(true));
    }

    private async Task CaptureOnceAsync()
    {
        if (_disposed || SelectedTarget is null) return;
        if (_stateMachine.CurrentState != CaptureState.Armed) return;

        var target = SelectedTarget.Target;
        var settings = _settings.Current;
        _stateMachine.Fire(CaptureTrigger.HotkeyPressed);

        CapturedFrame? frame;
        try
        {
            frame = await _captureEngine.CaptureAsync(target).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture engine threw.");
            _stateMachine.Fire(CaptureTrigger.ErrorOccurred);
            _toasts.ShowError(Localization["Toast_CaptureFailed"], ex.Message);
            return;
        }

        _stateMachine.Fire(CaptureTrigger.FrameCaptured);

        // Preview gate — only runs if settings say so AND the state machine routed us there.
        if (_stateMachine.CurrentState == CaptureState.Previewing)
        {
            var accepted = await _previewPresenter.ShowAsync(frame, target).ConfigureAwait(true);
            if (!accepted)
            {
                _stateMachine.Fire(CaptureTrigger.PreviewRejected);
                return;
            }
            _stateMachine.Fire(CaptureTrigger.PreviewAccepted);
        }

        // Saving ------------------------------------------------------------
        try
        {
            var result = await SaveAsync(frame, target, settings).ConfigureAwait(true);
            switch (result)
            {
                case CaptureResult.Success ok:
                    LastCapturePath = ok.FilePath;
                    _stateMachine.Fire(CaptureTrigger.Saved);
                    StatusMessage = string.Format(
                        Localization["Dashboard_StatusSaved"],
                        Path.GetFileName(ok.FilePath),
                        ok.Width, ok.Height, (int)ok.Duration.TotalMilliseconds);
                    _toasts.ShowSuccess(
                        Localization["Toast_CaptureSaved"],
                        Path.GetFileName(ok.FilePath));
                    PlayCaptureSoundIfEnabled();
                    break;
                case CaptureResult.Failure fail:
                    _stateMachine.Fire(CaptureTrigger.ErrorOccurred);
                    StatusMessage = string.Format(
                        Localization["Dashboard_StatusFailed"],
                        fail.ErrorCode, fail.DeveloperMessage);
                    _toasts.ShowError(
                        Localization["Toast_CaptureFailed"],
                        fail.DeveloperMessage);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save path threw unexpectedly.");
            _stateMachine.Fire(CaptureTrigger.ErrorOccurred);
            _toasts.ShowError(Localization["Toast_CaptureFailed"], ex.Message);
        }
    }

    /// <summary>
    /// Run the orchestrator-equivalent save path inline: pick an encoder, write to a tmp
    /// file, atomically rename, return a CaptureResult. We don't reuse CaptureOrchestrator
    /// directly because preview has already consumed the capture and we want to feed its
    /// frame back in.
    /// </summary>
    private async Task<CaptureResult> SaveAsync(CapturedFrame frame, GameTarget target, AppSettings settings)
    {
        var format = settings.Capture.DefaultFormat;
        var encoder = ResolveEncoder(format);
        if (encoder is null)
        {
            return new CaptureResult.Failure(
                Core.Errors.CaptureError.EncodingFailure,
                $"No encoder registered for format {format}.");
        }

        var outputDir = string.IsNullOrWhiteSpace(settings.Capture.OutputDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "CaptureImage")
            : settings.Capture.OutputDirectory;
        Directory.CreateDirectory(outputDir);

        var finalPath = _fileNameStrategy.BuildFilePath(
            outputDir,
            settings.Capture.FileNameTemplate,
            target,
            DateTimeOffset.Now,
            format);

        var tempPath = finalPath + ".tmp";
        long fileSize;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using (var fs = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                await encoder.EncodeAsync(
                    frame, format,
                    settings.Capture.JpegQuality,
                    settings.Capture.WebpQuality,
                    fs).ConfigureAwait(false);
                await fs.FlushAsync().ConfigureAwait(false);
                fileSize = fs.Length;
            }
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
            return new CaptureResult.Failure(Core.Errors.CaptureError.FileWriteFailure, ex.Message, ex);
        }
        stopwatch.Stop();

        return new CaptureResult.Success(finalPath, frame.Width, frame.Height, fileSize, stopwatch.Elapsed);
    }

    private IImageEncoder? ResolveEncoder(ImageFormat format)
    {
        foreach (var e in _encoders)
        {
            if (e.Supports(format)) return e;
        }
        return null;
    }

    // -- process watch ------------------------------------------------------

    private void OnProcessChanged(object? sender, ProcessChange e)
    {
        _pendingRefresh?.Cancel();
        var cts = new CancellationTokenSource();
        _pendingRefresh = cts;
        var token = cts.Token;

        _dispatcher.Post(async () =>
        {
            try
            {
                await Task.Delay(RefreshDebounce, token).ConfigureAwait(true);
                if (token.IsCancellationRequested) return;

                // Suppress refresh while a preview modal is up — rebuilding Targets
                // would replace the GameTargetViewModel instances and the user-visible
                // SelectedTarget would shuffle out from under the preview decision.
                // The next process event after the preview closes triggers a fresh
                // debounce, so nothing's lost.
                if (_stateMachine.CurrentState is CaptureState.Previewing or CaptureState.Saving)
                {
                    _logger.LogDebug(
                        "Suppressed dashboard refresh while in {State} state.",
                        _stateMachine.CurrentState);
                    return;
                }

                await RefreshAsync().ConfigureAwait(true);
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer event — expected.
            }
        });
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_stateMachine.CurrentState == CaptureState.Armed)
        {
            UpdateStatusForState();
        }

        ApplyAutoSwitchSetting();
    }

    private void ApplyAutoSwitchSetting()
    {
        var enabled = _settings.Current.Capture.AutoSwitchOnAltTab;
        if (enabled && !_foregroundWatcher.IsRunning)
        {
            _foregroundWatcher.Start();
        }
        else if (!enabled && _foregroundWatcher.IsRunning)
        {
            _foregroundWatcher.Stop();
        }
    }

    private void OnForegroundChanged(object? sender, nint hwnd)
    {
        // Watcher runs on a thread-pool timer; marshal to the UI thread before touching
        // SelectedTarget so binding updates land on the dispatcher.
        _dispatcher.Post(() =>
        {
            if (_disposed) return;
            if (!_settings.Current.Capture.AutoSwitchOnAltTab) return;

            foreach (var vm in Targets)
            {
                if (vm.Target.WindowHandle == hwnd)
                {
                    if (!ReferenceEquals(SelectedTarget, vm))
                    {
                        SelectedTarget = vm;
                        _logger.LogDebug(
                            "Auto-switched target to PID {Pid} ({Name}).",
                            vm.ProcessId, vm.DisplayName);
                    }
                    return;
                }
            }
            // No match — keep the current selection. Clearing it would surprise users
            // who Alt-Tab to apps that are filtered out of the target list (Explorer,
            // Settings, etc.).
        });
    }

    /// <summary>
    /// Play the standard Windows "SystemAsterisk" notification sound on a successful
    /// capture when <c>UI.SoundEnabled</c> is on. The setting has lived in
    /// <c>AppSettings</c> since v1.0 but had no consumer until v1.3-M7a — this hooks it
    /// up. Uses Win32 PlaySound directly (via P/Invoke) instead of
    /// <c>System.Media.SystemSounds</c> because the latter lives in
    /// <c>System.Windows.Extensions</c> which only ships for Windows TFMs, and the
    /// ViewModels project is intentionally TFM-portable. Failures are non-fatal: the
    /// capture has already succeeded; missing audio devices just get logged.
    /// </summary>
    [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_ALIAS = 0x00010000;

    private void PlayCaptureSoundIfEnabled()
    {
        if (!_settings.Current.UI.SoundEnabled) return;
        try
        {
            PlaySound("SystemAsterisk", IntPtr.Zero, SND_ASYNC | SND_ALIAS);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to play capture sound.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hotkeys.Triggered -= OnHotkeyTriggered;
        _watcher.Changed -= OnProcessChanged;
        _settings.Changed -= OnSettingsChanged;
        _foregroundWatcher.ForegroundChanged -= OnForegroundChanged;
        _foregroundWatcher.Stop();
        _stateMachine.StateChanged -= OnStateChanged;
        Localization.PropertyChanged -= OnLocalizationChanged;
        _pendingRefresh?.Cancel();
        _pendingRefresh?.Dispose();
    }
}
