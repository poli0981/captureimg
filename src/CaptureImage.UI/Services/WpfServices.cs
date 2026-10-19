// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Windows;
using System.Windows.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Services;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CaptureImage.UI.Services;

/// <summary>The main window, once created; services use it as dialog owner and to pick snackbar vs. toast.</summary>
public sealed class ShellContext
{
    public Window? MainWindow { get; set; }

    /// <summary>The main window is on screen and focused, so in-window notifications will be seen.</summary>
    public bool IsMainWindowActive => MainWindow is { IsVisible: true, IsActive: true, WindowState: not WindowState.Minimized };

    public bool IsMainWindowVisible => MainWindow is { IsVisible: true, WindowState: not WindowState.Minimized };
}

/// <summary><see cref="IUIThreadDispatcher"/> over the WPF dispatcher.</summary>
public sealed class WpfDispatcher(Dispatcher dispatcher) : IUIThreadDispatcher
{
    public bool IsOnUIThread => dispatcher.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return dispatcher.CheckAccess() ? Task.FromResult(func()) : dispatcher.InvokeAsync(func).Task;
    }

    public Task InvokeAsync(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        return dispatcher.CheckAccess() ? func() : dispatcher.InvokeAsync(func).Task.Unwrap();
    }
}

/// <summary>Quit the app (tray "Exit", update restart).</summary>
public sealed class AppLifetime(Dispatcher dispatcher) : IAppLifetime
{
    public void Shutdown() => dispatcher.BeginInvoke(() => Application.Current?.Shutdown());
}

/// <summary>System / Light / Dark / HighContrast through WPF UI's theme manager.</summary>
public sealed class ThemeService(ShellContext shell) : IThemeService
{
    private bool _watching;

    public void Apply(string theme)
    {
        var followSystem = SettingValues.NormalizeTheme(theme) == "System";
        var applied = SettingValues.NormalizeTheme(theme) switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            "HighContrast" => ApplicationTheme.HighContrast,
            _ => SystemApplicationTheme(),
        };

        ApplicationThemeManager.Apply(applied, WindowBackdropType.Mica, updateAccent: true);

        if (shell.MainWindow is { } window)
        {
            if (followSystem && !_watching)
            {
                SystemThemeWatcher.Watch(window, WindowBackdropType.Mica, updateAccents: true);
                _watching = true;
            }
            else if (!followSystem && _watching)
            {
                SystemThemeWatcher.UnWatch(window);
                _watching = false;
            }
        }
    }

    private static ApplicationTheme SystemApplicationTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast())
        {
            return ApplicationTheme.HighContrast;
        }
        return ApplicationThemeManager.GetSystemTheme() is SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }
}

/// <summary>Navigation pages come from the container (constructor injection, one instance each).</summary>
public sealed class ServicePageProvider(IServiceProvider services) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => services.GetService(pageType);
}
