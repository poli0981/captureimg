using System;
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

        // Logging: route Microsoft.Extensions.Logging through Serilog
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SerilogLoggerProvider(Log.Logger, dispose: false));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Navigation
        services.AddSingleton<INavigationService, NavigationService>();

        // Main window VM — singleton because MainWindow is single-instance
        services.AddSingleton<MainWindowViewModel>();

        // Tab view models — transient so nav can re-create on revisit later if we want
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<UpdateViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
