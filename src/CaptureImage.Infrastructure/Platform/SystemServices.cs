// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Diagnostics;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Media.Audio;
using Windows.Win32.System.Threading;

namespace CaptureImage.Infrastructure.Platform;

/// <summary>Plays the Windows "asterisk" sound after a capture.</summary>
public sealed class SoundService : ISoundService
{
    public void PlayCaptureSound() =>
        PInvoke.PlaySound("SystemAsterisk", default, SND_FLAGS.SND_ALIAS | SND_FLAGS.SND_ASYNC | SND_FLAGS.SND_NODEFAULT);
}

/// <summary>
/// Windows 11 efficiency mode: EcoQoS plus below-normal priority while the app idles in the
/// tray. <see cref="BeginForegroundWork"/> lifts it for the duration of a capture.
/// </summary>
public sealed partial class PerformanceModeService : IPerformanceModeService
{
    private readonly ILogger<PerformanceModeService> _logger;
    private readonly Lock _gate = new();
    private bool _background;
    private int _foregroundWork;

    public PerformanceModeService(ILogger<PerformanceModeService> logger)
    {
        _logger = logger;
    }

    public void SetBackground(bool isBackground)
    {
        lock (_gate)
        {
            _background = isBackground;
            Apply_NoLock();
        }
    }

    public IDisposable BeginForegroundWork()
    {
        lock (_gate)
        {
            _foregroundWork++;
            Apply_NoLock();
        }
        return new Scope(this);
    }

    private void EndForegroundWork()
    {
        lock (_gate)
        {
            _foregroundWork--;
            Apply_NoLock();
        }
    }

    private unsafe void Apply_NoLock()
    {
        var eco = _background && _foregroundWork == 0;
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = eco ? PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };

        if (!PInvoke.SetProcessInformation(PInvoke.GetCurrentProcess(), PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, &state, (uint)sizeof(PROCESS_POWER_THROTTLING_STATE)))
        {
            LogThrottlingFailed();
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = eco ? ProcessPriorityClass.BelowNormal : ProcessPriorityClass.Normal;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            LogPriorityFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "SetProcessInformation(ProcessPowerThrottling) failed.")]
    private partial void LogThrottlingFailed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Changing the process priority failed.")]
    private partial void LogPriorityFailed(Exception exception);

    private sealed class Scope(PerformanceModeService owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.EndForegroundWork();
            }
        }
    }
}

/// <summary>
/// "Start with Windows" via the per-user Run key. Points at the Velopack launcher in the
/// install root (stable across updates) when present, otherwise at the running executable.
/// </summary>
public sealed partial class AutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CaptureImage";

    private readonly ILogger<AutostartService> _logger;

    public AutostartService(ILogger<AutostartService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string { Length: > 0 };
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                key.SetValue(ValueName, $"\"{LauncherPath()}\" --minimized", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            LogChanged(enabled);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            LogFailed(ex);
            return false;
        }
    }

    /// <summary>Remove the Run value; called from the Velopack uninstall hook.</summary>
    public static void RemoveForUninstall()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string LauncherPath()
    {
        var current = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CaptureImage.App.exe");
        var directory = Path.GetDirectoryName(current);
        // Velopack layout: <root>\current\CaptureImage.App.exe with a launcher stub at <root>\CaptureImage.App.exe.
        if (directory is not null && string.Equals(Path.GetFileName(directory), "current", StringComparison.OrdinalIgnoreCase))
        {
            var stub = Path.Combine(Path.GetDirectoryName(directory)!, Path.GetFileName(current));
            if (File.Exists(stub))
            {
                return stub;
            }
        }
        return current;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Start with Windows {Enabled}.")]
    private partial void LogChanged(bool enabled);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not update the Run registry key.")]
    private partial void LogFailed(Exception exception);
}
