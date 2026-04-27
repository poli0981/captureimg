using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;
using Velopack;

namespace CaptureImage.App;

internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <remarks>
    /// <see cref="VelopackApp.Build"/> MUST be the very first thing <see cref="Main"/> does.
    /// The Velopack installer passes command-line arguments like <c>--squirrel-install</c>,
    /// <c>--squirrel-firstrun</c>, and <c>--squirrel-uninstall</c> on the first launch after an
    /// install/update/uninstall event; these need to be handled before WinUI 3 boots,
    /// otherwise the installer will hang waiting for the previous instance to exit.
    /// </remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().Run();

        var (inMemorySink, loggingLevelSwitch) = LoggingSetup.Initialize();

        RegisterGlobalExceptionHandlers();

        var shutdownReason = "Normal";
        try
        {
            Log.Information(
                "CaptureImage starting. Version={Version} OS={OS} Culture={Culture} Args={Args}",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0-dev",
                System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                System.Globalization.CultureInfo.CurrentUICulture.Name,
                args);

            var services = CompositionRoot.BuildServices(inMemorySink, loggingLevelSwitch);
            App.Services = services;

            var settingsStore = services.GetRequiredService<ISettingsStore>();
            settingsStore.LoadAsync().GetAwaiter().GetResult();

            var configuredLevel = LoggingSetup.ParseLevel(settingsStore.Current.LogLevel);
            loggingLevelSwitch.MinimumLevel = configuredLevel;
            Log.Information("Log level set to {Level} from settings.", configuredLevel);

            var localization = services.GetRequiredService<ILocalizationService>();
            var cultureName = settingsStore.Current.Culture;
            if (!string.IsNullOrWhiteSpace(cultureName))
            {
                try
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(cultureName);
                    var supported = false;
                    foreach (var shipped in localization.SupportedCultures)
                    {
                        if (shipped.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            supported = true;
                            break;
                        }
                    }

                    if (supported)
                    {
                        localization.SetCulture(culture);
                    }
                    else
                    {
                        Log.Warning(
                            "Persisted culture {Culture} is not a shipped language; falling back to default.",
                            cultureName);
                    }
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    Log.Warning("Persisted culture {Culture} is unknown; falling back to default.", cultureName);
                }
            }

            // WinUI 3 entry point. Application.Start blocks until the dispatcher exits. The
            // callback runs once on the freshly-minted UI thread before any Window is shown
            // — install a SynchronizationContext that flows back to that thread so async
            // continuations land where bindings can see them.
            Application.Start(p =>
            {
                var ctx = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(ctx);
                _ = new App();
            });

            return 0;
        }
        catch (Exception ex)
        {
            shutdownReason = "StartupCrash";
            Log.Fatal(ex, "CaptureImage crashed during startup.");
            return -1;
        }
        finally
        {
            try
            {
                if (App.Services is not null)
                {
                    var store = App.Services.GetService<ISettingsStore>();
                    if (store is not null)
                    {
                        store.FlushAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to flush settings on shutdown.");
            }
            Log.Information("CaptureImage shutting down. Reason={Reason}", shutdownReason);
            Log.CloseAndFlush();
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(
                ex,
                "Unhandled AppDomain exception. IsTerminating={IsTerminating}",
                e.IsTerminating);
            if (e.IsTerminating)
            {
                Log.CloseAndFlush();
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved();
        };
    }
}
