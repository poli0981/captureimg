using System;
using System.Threading.Tasks;
using Avalonia;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
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
    /// install/update/uninstall event; these need to be handled before Avalonia boots,
    /// otherwise the installer will hang waiting for the previous instance to exit.
    /// </remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack first — runs hooks for install/firstrun/update/uninstall and returns only
        // when the launch is a normal one.
        VelopackApp.Build().Run();

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
            // render in the right language from frame zero. Only accept cultures the app
            // actually ships translations for — a hand-edited settings.json with, say,
            // Culture=fr-FR used to flip the app to fr and then fall through to neutral
            // en strings with Settings showing "English" selected; refuse at startup
            // instead so the default en-US stays coherent.
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
