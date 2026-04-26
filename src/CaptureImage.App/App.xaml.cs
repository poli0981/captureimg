using System;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Serilog;

namespace CaptureImage.App;

/// <summary>
/// WinUI 3 Application root. Holds the static <see cref="Services"/> handle so view-models
/// resolved from XAML data templates can reach the DI container; <see cref="OnLaunched"/>
/// binds the UI dispatcher and shows the first window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Service provider populated by <c>Program.Main</c> before <see cref="Application.Start"/>
    /// returns. View-models and converters resolve through this.
    /// </summary>
    public static IServiceProvider? Services { get; set; }

    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = Services
            ?? throw new InvalidOperationException(
                "App.Services must be assigned before Application.Start runs.");

        // Bind the WinUI 3 dispatcher now that we are on the UI thread.
        var dispatcher = services.GetRequiredService<WinUIThreadDispatcher>();
        dispatcher.Bind(DispatcherQueue.GetForCurrentThread());

        _window = new MainWindow();
        _window.Activate();

        // Tray host attaches to the live Window. M1 ships a no-op stub; M6 wires the real
        // H.NotifyIcon.WindowsAppSDK implementation.
        var tray = services.GetRequiredService<ITrayIconHost>();
        tray.Initialize(_window);
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on UI dispatcher.");
        e.Handled = true;
    }
}
