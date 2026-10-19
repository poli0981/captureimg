// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.IO;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Paths;
using CaptureImage.Core.Pipeline;
using CaptureImage.Infrastructure.Capture;
using CaptureImage.Infrastructure.Diagnostics;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Library;
using CaptureImage.Infrastructure.Logging;
using CaptureImage.Infrastructure.Ocr;
using CaptureImage.Infrastructure.Platform;
using CaptureImage.Infrastructure.Settings;
using CaptureImage.Infrastructure.Steam;
using CaptureImage.Infrastructure.Update;
using CaptureImage.Infrastructure.Win32;
using CaptureImage.Infrastructure.Windowing;
using CaptureImage.ViewModels.Localization;
using CaptureImage.ViewModels.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace CaptureImage.App.Composition;

/// <summary>Core and Infrastructure services (the UI layer registers its own in CaptureImage.UI).</summary>
internal static class ServiceRegistration
{
    public static IServiceCollection AddCaptureImageCore(
        this IServiceCollection services,
        AppPaths paths,
        LoggingLevelSwitch levelSwitch,
        InMemorySink logBuffer)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(AppInfo.FromAssembly(typeof(Program).Assembly));
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<ILocalizationService, ResxLocalizationService>();
        services.AddSingleton<ILogBufferSource>(logBuffer);
        services.AddSingleton<ILogLevelSwitcher>(new SerilogLogLevelSwitcher(levelSwitch));

        // A --data-dir run (tests, CI smoke) never adopts the real v1 settings.
        services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(
            paths,
            sp.GetRequiredService<ILogger<JsonSettingsStore>>(),
            paths.IsOverridden ? null : AppPaths.LegacySettingsFile));

        services.AddSingleton<IHotkeyService, Win32HotkeyService>();
        services.AddSingleton<IWindowWatcher, WinEventWindowWatcher>();
        services.AddSingleton<IconExtractor>();
        services.AddSingleton<ISteamDetector>(sp => new SteamLibraryScanner(sp.GetRequiredService<ILogger<SteamLibraryScanner>>()));
        services.AddSingleton<ITargetCatalog, TargetCatalog>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<D3D11DeviceManager>();
        services.AddSingleton<ICaptureEngine, WgcCaptureEngine>();

        services.AddSingleton<IImageEncoder, SkiaImageEncoder>();
        services.AddSingleton<IImageEncoder, WicImageEncoder>();
        services.AddSingleton<IImageDecoder, WicImageDecoder>();
        services.AddSingleton(new FileNameStrategy(File.Exists));
        services.AddSingleton<CaptureFileWriter>();
        services.AddSingleton<ICaptureLibrary, CaptureLibraryService>();

        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<ISoundService, SoundService>();
        services.AddSingleton<IPerformanceModeService, PerformanceModeService>();
        services.AddSingleton<IAutostartService, AutostartService>();
        services.AddSingleton<IDiagnosticsExporter, DiagnosticsExporter>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<IOcrService, WindowsOcrService>();

        services.AddSingleton<CaptureCoordinator>();
        services.AddHostedService<WindowWatcherService>();
        return services;
    }
}

/// <summary>Starts the WinEvent window watcher with the host and stops it on shutdown.</summary>
internal sealed class WindowWatcherService(IWindowWatcher watcher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        watcher.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        watcher.Stop();
        return Task.CompletedTask;
    }
}
