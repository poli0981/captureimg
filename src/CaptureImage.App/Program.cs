using System;
using System.Threading.Tasks;
using Avalonia;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace CaptureImage.App;

internal static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    /// <remarks>
    /// NOTE: When Velopack lands in M4, <c>VelopackApp.Build().Run()</c> must be the very first
    /// line of <see cref="Main"/> so it can handle <c>--squirrel-install</c>, <c>--squirrel-firstrun</c>,
    /// etc. before Avalonia boots.
    /// </remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        // Bootstrap Serilog early (and keep the in-memory sink reference for DI).
        var inMemorySink = LoggingSetup.Initialize();

        try
        {
            Log.Information("CaptureImage starting. Args: {Args}", args);

            var services = CompositionRoot.BuildServices(inMemorySink);
            UI.App.Services = services;

            // Load persisted settings before the UI binds to them.
            var settingsStore = services.GetRequiredService<ISettingsStore>();
            settingsStore.LoadAsync().GetAwaiter().GetResult();

            // Apply persisted culture before the first window is constructed so nav labels etc.
            // render in the right language from frame zero.
            var localization = services.GetRequiredService<ILocalizationService>();
            var cultureName = settingsStore.Current.Culture;
            if (!string.IsNullOrWhiteSpace(cultureName))
            {
                try
                {
                    localization.SetCulture(System.Globalization.CultureInfo.GetCultureInfo(cultureName));
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    Log.Warning("Persisted culture {Culture} is unknown; falling back to default.", cultureName);
                }
            }

            return BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "CaptureImage crashed during startup.");
            return -1;
        }
        finally
        {
            // Flush any pending settings writes and close the logger.
            try
            {
                if (UI.App.Services is not null)
                {
                    var store = UI.App.Services.GetService<ISettingsStore>();
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
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Avalonia AppBuilder. Also used by the Avalonia designer — do not do expensive work here.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<UI.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
