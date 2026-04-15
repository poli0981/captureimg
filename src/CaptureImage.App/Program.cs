using System;
using Avalonia;
using CaptureImage.UI;
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
        // Bootstrap Serilog early so any failure during DI wiring is captured.
        Log.Logger = LoggingSetup.CreateBootstrapLogger();

        try
        {
            Log.Information("CaptureImage starting. Args: {Args}", args);

            var services = CompositionRoot.BuildServices();
            UI.App.Services = services;

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
