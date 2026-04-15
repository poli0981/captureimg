using System;
using System.IO.Abstractions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Pipeline;
using CaptureImage.Infrastructure.Capture;
using CaptureImage.Infrastructure.Hotkeys;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Logging;
using CaptureImage.Infrastructure.Processes;
using CaptureImage.Infrastructure.Settings;
using CaptureImage.Infrastructure.Steam;
using CaptureImage.UI.Localization;
using CaptureImage.UI.Services;
using CaptureImage.ViewModels;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Dashboard;
using CaptureImage.ViewModels.Logs;
using CaptureImage.ViewModels.Navigation;
using CaptureImage.ViewModels.Settings;
using CaptureImage.ViewModels.Update;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace CaptureImage.App;

/// <summary>
/// Builds the DI container for the app. Kept as a single file so it's easy to see every binding.
/// </summary>
internal static class CompositionRoot
{
    public static IServiceProvider BuildServices(InMemorySink inMemorySink)
    {
        var services = new ServiceCollection();

        // --- Logging ------------------------------------------------------------
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: false));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // The same InMemorySink instance is both the Serilog sink AND the log buffer source
        // used by the log viewer VM.
        services.AddSingleton(inMemorySink);
        services.AddSingleton<ILogBufferSource>(inMemorySink);

        // --- Cross-cutting -----------------------------------------------------
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IUIThreadDispatcher, AvaloniaUIDispatcher>();
        services.AddSingleton<ILocalizationService, ResxLocalizationService>();

        // --- Settings ----------------------------------------------------------
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();

        // --- Steam detection ---------------------------------------------------
        services.AddSingleton<ISteamRootLocator, RegistrySteamRootLocator>();
        services.AddSingleton<ISteamDetector, SteamLibraryScanner>();

        // --- Process / window enumeration --------------------------------------
        services.AddSingleton<WindowEnumerator>();
        services.AddSingleton<IconExtractor>();
        services.AddSingleton<IProcessDetector, GameDetector>();
        services.AddSingleton<IProcessWatcher, WmiProcessWatcher>();

        // --- Capture engine + encoders -----------------------------------------
        services.AddSingleton<D3D11DeviceManager>();
        services.AddSingleton<ICaptureEngine, WindowsGraphicsCaptureEngine>();
        services.AddSingleton<PrintWindowFallback>();
        services.AddSingleton<IImageEncoder, SkiaImageEncoder>();
        services.AddSingleton<IImageEncoder, ImageSharpTiffEncoder>();
        services.AddSingleton<FileNameStrategy>(_ =>
            new FileNameStrategy(System.IO.File.Exists));
        services.AddSingleton<CaptureOrchestrator>();

        // --- Hotkeys -----------------------------------------------------------
        services.AddSingleton<IHotkeyService, SharpHookHotkeyService>();

        // --- UI-side services (toasts, preview, tray) --------------------------
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IPreviewPresenter, AvaloniaPreviewPresenter>();
        services.AddSingleton<ITrayIconHost, AvaloniaTrayIconHost>();

        // --- Navigation --------------------------------------------------------
        services.AddSingleton<INavigationService, NavigationService>();

        // --- View models -------------------------------------------------------
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<LogViewerViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
