using System.Management;
using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Processes;

/// <summary>
/// <see cref="IProcessWatcher"/> backed by WMI <c>__InstanceCreationEvent</c> /
/// <c>__InstanceDeletionEvent</c> over <c>Win32_Process</c>. Polling interval is fixed at
/// 2 seconds — the cost/latency trade-off the plan already ratified.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WmiProcessWatcher : IProcessWatcher
{
    private const int PollSeconds = 2;

    private readonly ILogger<WmiProcessWatcher> _logger;
    private readonly object _gate = new();
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private bool _disposed;

    public event EventHandler<ProcessChange>? Changed;

    public WmiProcessWatcher(ILogger<WmiProcessWatcher> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WmiProcessWatcher));
            if (_startWatcher is not null) return;

            try
            {
                _startWatcher = CreateWatcher("__InstanceCreationEvent", ProcessChangeKind.Started);
                _stopWatcher = CreateWatcher("__InstanceDeletionEvent", ProcessChangeKind.Stopped);
                _startWatcher.Start();
                _stopWatcher.Start();
                _logger.LogInformation("WMI process watcher started (poll interval {Seconds}s).", PollSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start WMI process watcher.");
                DisposeWatchers_NoLock();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            DisposeWatchers_NoLock();
            _logger.LogInformation("WMI process watcher stopped.");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            DisposeWatchers_NoLock();
        }
    }

    private ManagementEventWatcher CreateWatcher(string eventClass, ProcessChangeKind kind)
    {
        var query = new WqlEventQuery(
            eventClass,
            TimeSpan.FromSeconds(PollSeconds),
            "TargetInstance ISA 'Win32_Process'");

        var watcher = new ManagementEventWatcher(query);
        watcher.EventArrived += (_, args) => OnEventArrived(args, kind);
        return watcher;
    }

    private void OnEventArrived(EventArrivedEventArgs args, ProcessChangeKind kind)
    {
        try
        {
            var target = args.NewEvent.Properties["TargetInstance"].Value as ManagementBaseObject;
            if (target is null) return;

            var pidObj = target.Properties["ProcessId"].Value;
            var nameObj = target.Properties["Name"].Value;

            if (pidObj is null) return;
            var pid = Convert.ToUInt32(pidObj);
            var name = nameObj as string ?? string.Empty;

            Changed?.Invoke(this, new ProcessChange(kind, pid, name));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse WMI process event.");
        }
    }

    private void DisposeWatchers_NoLock()
    {
        try { _startWatcher?.Stop(); } catch { /* ignore */ }
        try { _stopWatcher?.Stop(); } catch { /* ignore */ }
        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();
        _startWatcher = null;
        _stopWatcher = null;
    }
}
