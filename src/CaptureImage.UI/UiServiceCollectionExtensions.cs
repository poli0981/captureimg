// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Pages;
using CaptureImage.UI.Services;
using CaptureImage.UI.Shell;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Dashboard;
using CaptureImage.ViewModels.Library;
using CaptureImage.ViewModels.Logs;
using CaptureImage.ViewModels.Profiles;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Settings;
using CaptureImage.ViewModels.Shell;
using CaptureImage.ViewModels.Update;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace CaptureImage.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// UI services, view models, pages and windows. <paramref name="dispatcher"/> is the
    /// dispatcher of the thread that will run the WPF application.
    /// </summary>
    public static IServiceCollection AddCaptureImageUi(this IServiceCollection services, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dispatcher);

        services.AddSingleton(dispatcher);
        services.AddSingleton<ShellContext>();
        services.AddSingleton<IUIThreadDispatcher, WpfDispatcher>();
        services.AddSingleton<IAppLifetime, AppLifetime>();
        services.AddSingleton<ViewModels.Services.IThemeService, Services.ThemeService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPreviewPresenter, PreviewPresenter>();
        services.AddSingleton<IRegionSelector, RegionSelector>();
        services.AddSingleton<IPinnedThumbnailHost, PinnedThumbnailHost>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<INavigationViewPageProvider, ServicePageProvider>();

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogViewerViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<AboutViewModel>();

        services.AddSingleton<DashboardPage>();
        services.AddSingleton<LibraryPage>();
        services.AddSingleton<ProfilesPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<LogsPage>();
        services.AddSingleton<UpdatePage>();
        services.AddSingleton<AboutPage>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<TrayIcon>();
        return services;
    }
}
