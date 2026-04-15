using System;
using System.IO.Abstractions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Pipeline;
using CaptureImage.Infrastructure.Capture;
using CaptureImage.Infrastructure.Hotkeys;
using CaptureImage.Infrastructure.Imaging;
using CaptureImage.Infrastructure.Processes;
using CaptureImage.Infrastructure.Steam;
using CaptureImage.UI.Services;
using CaptureImage.ViewModels;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Dashboard;
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
    public static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // --- Logging ------------------------------------------------------------
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: false));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // --- Cross-cutting -----------------------------------------------------
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IUIThreadDispatcher, AvaloniaUIDispatcher>();

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

        // --- Navigation --------------------------------------------------------
        services.AddSingleton<INavigationService, NavigationService>();

        // --- View models -------------------------------------------------------
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
