// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.IO;
using System.Windows.Threading;
using CaptureImage.App.Composition;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Paths;
using CaptureImage.Infrastructure.Logging;
using CaptureImage.Infrastructure.Platform;
using CaptureImage.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Velopack;

namespace CaptureImage.App;

/// <summary>Entry point: Velopack hooks, single instance, logging, container, then the WPF app.</summary>
public static class Program
{
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack's install / update / uninstall hooks must run before anything else.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => AutostartService.RemoveForUninstall())
            .Run();

        var options = StartupOptions.Parse(args);
        var paths = AppPaths.FromCommandLine(args);
        paths.EnsureCreated();

        using var instance = new SingleInstance(options.InstanceId);
        if (!instance.TryAcquire())
        {
            instance.SignalExisting();
            return 0;
        }

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var logBuffer = new InMemorySink();
        Log.Logger = CreateLogger(paths, levelSwitch, logBuffer);
        var log = Log.ForContext(typeof(Program));
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Fatal(e.ExceptionObject as Exception, "Unhandled exception; CaptureImage is terminating.");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };

        try
        {
            log.Information("CaptureImage {Version} starting (data: {DataDir}).", AppVersion(), paths.DataDirectory);
            return Run(args, options, paths, levelSwitch, logBuffer, instance);
        }
        finally
        {
            log.Information("CaptureImage exited.");
            Log.CloseAndFlush();
        }
    }

    private static int Run(
        string[] args,
        StartupOptions options,
        AppPaths paths,
        LoggingLevelSwitch levelSwitch,
        InMemorySink logBuffer,
        SingleInstance instance)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        builder.Logging.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddCaptureImageCore(paths, levelSwitch, logBuffer);
        builder.Services.AddCaptureImageUi(Dispatcher.CurrentDispatcher);
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        using var host = builder.Build();
        var settings = host.Services.GetRequiredService<ISettingsStore>();
        settings.LoadAsync().GetAwaiter().GetResult();
        levelSwitch.MinimumLevel = SerilogLogLevelSwitcher.Parse(settings.Current.LogLevel);
        host.StartAsync().GetAwaiter().GetResult();

        var app = new UI.App(host.Services, new UiStartupOptions(options.StartMinimized, options.SmokeTest, options.ScreenshotDirectory));
        instance.ShowRequested += (_, _) => app.Dispatcher.BeginInvoke(app.ShowMainWindow);
        instance.StartListening();

        var exitCode = app.Run();

        host.StopAsync(ShutdownTimeout).GetAwaiter().GetResult();
        settings.FlushAsync().GetAwaiter().GetResult();
        return exitCode;
    }

    private static Logger CreateLogger(AppPaths paths, LoggingLevelSwitch levelSwitch, InMemorySink logBuffer) =>
        new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new UserPathRedactionEnricher())
            .WriteTo.Async(sink => sink.File(
                Path.Combine(paths.LogsDirectory, "captureimage-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:l}{NewLine}{Exception}",
                fileSizeLimitBytes: 10L * 1024 * 1024,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 14))
            .WriteTo.Sink(logBuffer)
            .CreateLogger();

    private static string AppVersion() => ViewModels.Services.AppInfo.FromAssembly(typeof(Program).Assembly).DisplayVersion;
}
