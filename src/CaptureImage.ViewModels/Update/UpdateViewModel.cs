using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Update;

/// <summary>
/// Update tab. Owns the check/download/install workflow, surfaces the service's log lines
/// in a scrollable list, and updates state/progress based on <see cref="IUpdateService"/>.
/// </summary>
public sealed partial class UpdateViewModel : ViewModelBase, IDisposable
{
    private readonly IUpdateService _updateService;
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILogger<UpdateViewModel> _logger;
    private bool _disposed;

    public ILocalizationService Localization { get; }

    /// <summary>Rolling log of service messages. Bounded to avoid runaway growth.</summary>
    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    private UpdateStatus _status = UpdateStatus.Idle;

    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private string? _availableVersion;

    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private bool _isBusy;

    public UpdateViewModel(
        IUpdateService updateService,
        IUIThreadDispatcher dispatcher,
        ILocalizationService localization,
        ILogger<UpdateViewModel> logger)
    {
        _updateService = updateService;
        _dispatcher = dispatcher;
        Localization = localization;
        _logger = logger;

        _currentVersion = _updateService.CurrentVersion;
        _updateService.LogEmitted += OnLogEmitted;
    }

    public bool CanCheck => !IsBusy;
    public bool CanDownload => !IsBusy && Status == UpdateStatus.UpdateAvailable;
    public bool CanInstall => !IsBusy && Status == UpdateStatus.Ready;

    partial void OnIsBusyChanged(bool value)
    {
        CheckCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanInstall));
    }

    partial void OnStatusChanged(UpdateStatus value)
    {
        CheckCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanInstall));
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckAsync()
    {
        if (_disposed) return;
        try
        {
            IsBusy = true;
            Status = UpdateStatus.Checking;

            var result = await _updateService.CheckAsync().ConfigureAwait(true);
            CurrentVersion = result.CurrentVersion;
            AvailableVersion = result.AvailableVersion;
            ReleaseNotes = result.ReleaseNotes;
            Status = result.Status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed.");
            Status = UpdateStatus.Failed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (_disposed) return;
        try
        {
            IsBusy = true;
            Status = UpdateStatus.Downloading;
            DownloadProgress = 0;

            var progress = new Progress<int>(p =>
                _dispatcher.Post(() => DownloadProgress = p));

            await _updateService.DownloadAsync(progress).ConfigureAwait(true);
            Status = UpdateStatus.Ready;
            DownloadProgress = 100;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed.");
            Status = UpdateStatus.Failed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void Install()
    {
        if (_disposed) return;
        try
        {
            IsBusy = true;
            Status = UpdateStatus.Installing;
            _updateService.ApplyAndRestart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update install failed.");
            Status = UpdateStatus.Failed;
            IsBusy = false;
        }
    }

    private void OnLogEmitted(object? sender, string line)
    {
        _dispatcher.Post(() =>
        {
            if (_disposed) return;
            LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            while (LogLines.Count > 200)
            {
                LogLines.RemoveAt(0);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _updateService.LogEmitted -= OnLogEmitted;
    }
}
