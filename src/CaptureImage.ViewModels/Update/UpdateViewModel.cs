// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Collections.ObjectModel;
using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Update;

/// <summary>
/// Update page: check GitHub Releases, download, then restart into the new version. The
/// first visit checks automatically; status text is localized, the detail log is not.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase, IDisposable
{
    private const int MaxLogLines = 200;
    private static readonly Uri ReleasesUrl = new("https://github.com/poli0981/captureimg/releases");

    private readonly IUpdateService _updates;
    private readonly IAppLifetime _lifetime;
    private readonly IShellService _shell;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<UpdateViewModel> _logger;
    private CancellationTokenSource? _cts;
    private bool _autoChecked;
    private bool _disposed;

    public UpdateViewModel(
        IUpdateService updates,
        IAppLifetime lifetime,
        IShellService shell,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        AppInfo appInfo,
        ILogger<UpdateViewModel> logger)
    {
        _updates = updates;
        _lifetime = lifetime;
        _shell = shell;
        _dispatcher = dispatcher;
        Localization = localization;
        AppInfo = appInfo;
        _logger = logger;
        _status = updates.IsAvailable ? UpdateStatus.Idle : UpdateStatus.Unavailable;

        _updates.LogEmitted += OnLogEmitted;
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    public ILocalizationService Localization { get; }

    public AppInfo AppInfo { get; }

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsWorking), nameof(ShowProgress), nameof(IsUpdateAvailable), nameof(IsReady), nameof(HasFailed))]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand), nameof(DownloadCommand), nameof(InstallAndRestartCommand), nameof(CancelCommand))]
    private UpdateStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _availableVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReleaseNotes))]
    private string? _releaseNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    private int _progress;

    public string StatusText => Status == UpdateStatus.UpdateAvailable
        ? Localization.Format("Update_Status_UpdateAvailable", AvailableVersion ?? "?")
        : Localization[$"Update_Status_{Status}"];

    public bool IsWorking => Status is UpdateStatus.Checking or UpdateStatus.Downloading or UpdateStatus.Installing;

    public bool ShowProgress => Status is UpdateStatus.Checking or UpdateStatus.Downloading;

    /// <summary>Checking has no percentage; downloading shows one once Velopack reports it.</summary>
    public bool IsProgressIndeterminate => Status != UpdateStatus.Downloading || Progress <= 0;

    public bool IsUpdateAvailable => Status == UpdateStatus.UpdateAvailable;

    public bool IsReady => Status == UpdateStatus.Ready;

    public bool HasFailed => Status == UpdateStatus.Failed;

    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);

    /// <summary>Set by the view: the first visit runs a check.</summary>
    public bool IsPageActive
    {
        set
        {
            if (value && !_autoChecked && _updates.IsAvailable && Status == UpdateStatus.Idle)
            {
                _autoChecked = true;
                CheckCommand.Execute(null);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        _autoChecked = true;
        using var cts = BeginOperation();
        Status = UpdateStatus.Checking;
        try
        {
            var result = await _updates.CheckAsync(cts.Token).ConfigureAwait(true);
            AvailableVersion = result.AvailableVersion;
            ReleaseNotes = result.ReleaseNotes;
            Status = result.Status;
        }
        catch (OperationCanceledException)
        {
            Status = UpdateStatus.Idle;
        }
        finally
        {
            EndOperation(cts);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        using var cts = BeginOperation();
        Progress = 0;
        Status = UpdateStatus.Downloading;
        try
        {
            await _updates.DownloadAsync(new DispatchedProgress(_dispatcher, p => Progress = p), cts.Token).ConfigureAwait(true);
            Status = UpdateStatus.Ready;
        }
        catch (OperationCanceledException)
        {
            Status = UpdateStatus.UpdateAvailable;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            LogDownloadFailed(ex);
            AppendLog(ex.Message);
            Status = UpdateStatus.Failed;
        }
        finally
        {
            EndOperation(cts);
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void InstallAndRestart()
    {
        try
        {
            Status = UpdateStatus.Installing;
            _updates.ApplyOnExit();
            _lifetime.Shutdown();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            LogInstallFailed(ex);
            AppendLog(ex.Message);
            Status = UpdateStatus.Failed;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void OpenReleases() => _shell.OpenUrl(ReleasesUrl);

    private bool CanCheck() => _updates.IsAvailable && !IsWorking;

    private bool CanDownload() => Status == UpdateStatus.UpdateAvailable;

    private bool CanInstall() => Status == UpdateStatus.Ready;

    private bool CanCancel() => Status is UpdateStatus.Checking or UpdateStatus.Downloading;

    private CancellationTokenSource BeginOperation()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;
        return cts;
    }

    private void EndOperation(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_cts, cts))
        {
            _cts = null;
        }
    }

    private void AppendLog(string line)
    {
        LogLines.Add(line);
        while (LogLines.Count > MaxLogLines)
        {
            LogLines.RemoveAt(0);
        }
    }

    private void OnLogEmitted(object? sender, string line) => _dispatcher.Post(() =>
    {
        if (!_disposed)
        {
            AppendLog(line);
        }
    });

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_disposed && e.PropertyName == "Item[]")
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _updates.LogEmitted -= OnLogEmitted;
        Localization.PropertyChanged -= OnLocalizationChanged;
        _cts?.Cancel();
    }

    /// <summary>Reports progress on the UI thread without relying on a synchronization context.</summary>
    private sealed class DispatchedProgress(IUIThreadDispatcher dispatcher, Action<int> report) : IProgress<int>
    {
        public void Report(int value) => dispatcher.Post(() => report(value));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Update download failed.")]
    private partial void LogDownloadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not start the update.")]
    private partial void LogInstallFailed(Exception exception);
}
