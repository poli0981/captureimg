using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CaptureImage.UI.Views;
using CaptureImage.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CaptureImage.UI;

public partial class App : Application
{
    /// <summary>
    /// Service provider is assigned by the host (CaptureImage.App) before
    /// <see cref="OnFrameworkInitializationCompleted"/> runs, so view-models can be resolved from DI.
    /// </summary>
    public static IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Services
                ?? throw new InvalidOperationException(
                    "App.Services must be assigned before the Avalonia lifetime starts.");

            // MainWindowViewModel's ctor sets SelectedNavItem = NavItems[0], which flows through
            // the nav service and activates DashboardViewModel. No explicit NavigateTo needed here.
            var mainVm = services.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
