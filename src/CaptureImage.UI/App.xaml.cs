// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Pipeline;
using CaptureImage.UI.Localization;
using CaptureImage.UI.Shell;
using CaptureImage.ViewModels.Profiles;
using CaptureImage.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaptureImage.UI;

/// <summary>Command-line choices that affect the UI.</summary>
/// <param name="StartMinimized">Start in the tray (<c>--minimized</c>, used by "Start with Windows").</param>
/// <param name="SmokeTest">Open every page, then exit with code 0 (<c>--smoke-test</c>, used by CI).</param>
/// <param name="ScreenshotDirectory">With the smoke test, save a PNG of every page here (<c>--screenshots</c>).</param>
public sealed record UiStartupOptions(bool StartMinimized, bool SmokeTest, string? ScreenshotDirectory = null);

/// <summary>
/// The WPF application: applies language and theme, creates the main window and tray icon,
/// and owns shutdown. <c>Main</c> lives in CaptureImage.App so Velopack runs first.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly UiStartupOptions _options;
    private readonly ILogger<App> _logger;
    private MainWindow? _mainWindow;
    private TrayIcon? _tray;

    public App(IServiceProvider services, UiStartupOptions options)
    {
        _services = services;
        _options = options;
        _logger = services.GetRequiredService<ILogger<App>>();
        InitializeComponent();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>Another instance was started: bring this one forward instead.</summary>
    public void ShowMainWindow() => _mainWindow?.ShowFromTray();

    /// <summary>Quit for real (tray Exit, update restart, close with "minimize to tray" off).</summary>
    public void RequestExit()
    {
        _mainWindow?.AllowClose();
        Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = _services.GetRequiredService<ISettingsStore>();
        var localization = _services.GetRequiredService<ILocalizationService>();
        LocalizationSource.Service = localization;
        ApplyCulture(settings, localization);

        // Listens for "create profile" from the Dashboard before its page is first opened.
        _services.GetRequiredService<ProfilesViewModel>();

        _mainWindow = _services.GetRequiredService<MainWindow>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Current.Theme);

        _tray = _services.GetRequiredService<TrayIcon>();
        _tray.Show();

        if (_options.SmokeTest || !(_options.StartMinimized || settings.Current.UI.StartMinimized))
        {
            _mainWindow.Show();
        }
        else
        {
            _services.GetRequiredService<IPerformanceModeService>().SetBackground(true);
        }

        if (settings.Current.UI.ArmOnStartup && !_options.SmokeTest)
        {
            _services.GetRequiredService<CaptureCoordinator>().Arm();
        }

        LogStarted(_logger, localization.CurrentCulture.Name, settings.Current.Theme);
        if (_options.SmokeTest)
        {
            _ = RunSmokeTestAsync(settings);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _services.GetRequiredService<CaptureCoordinator>().Disarm();
        base.OnExit(e);
    }

    private static void ApplyCulture(ISettingsStore settings, ILocalizationService localization)
    {
        CultureInfo? requested = null;
        if (!string.IsNullOrWhiteSpace(settings.Current.Culture))
        {
            try
            {
                requested = CultureInfo.GetCultureInfo(settings.Current.Culture);
            }
            catch (CultureNotFoundException)
            {
                requested = null;
            }
        }

        // First run: follow the Windows display language.
        var culture = localization.MatchSupportedCulture(requested ?? CultureInfo.CurrentUICulture);
        localization.SetCulture(culture);
        if (!string.Equals(settings.Current.Culture, culture.Name, StringComparison.Ordinal))
        {
            settings.Update(s => s with { Culture = culture.Name });
        }
    }

    /// <summary>CI: every page must resolve from the container and load, then the app exits 0.</summary>
    private async Task RunSmokeTestAsync(ISettingsStore settings)
    {
        try
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var window = _mainWindow ?? throw new InvalidOperationException("The main window was not created.");
            if (_options.ScreenshotDirectory is not null)
            {
                // Same size every run, so screenshots are comparable.
                window.WindowState = WindowState.Normal;
                window.Width = 1200;
                window.Height = 800;
                Directory.CreateDirectory(_options.ScreenshotDirectory);
            }

            foreach (var pageType in Shell.MainWindow.AllPageTypes)
            {
                // Navigating to the page already shown reports false; that is fine.
                if (window.CurrentPageType != pageType && !window.NavigateTo(pageType))
                {
                    throw new InvalidOperationException($"Navigation to {pageType.Name} failed.");
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(150).ConfigureAwait(true);
                if (window.CurrentPageType != pageType)
                {
                    throw new InvalidOperationException($"{pageType.Name} did not become the current page.");
                }

                if (_options.ScreenshotDirectory is { } directory)
                {
                    await Task.Delay(400).ConfigureAwait(true); // page transition
                    SaveScreenshot(window, Path.Combine(directory, pageType.Name.Replace("Page", string.Empty, StringComparison.Ordinal) + ".png"));
                }
            }

            await settings.FlushAsync().ConfigureAwait(true);
            LogSmokeTestPassed(_logger, Shell.MainWindow.AllPageTypes.Count);
            window.AllowClose();
            Shutdown(0);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            LogSmokeTestFailed(_logger, ex);
            _mainWindow?.AllowClose();
            Shutdown(1);
        }
    }

    /// <summary>Render the window's content (on the theme background, Mica isn't captured) to a PNG.</summary>
    private static void SaveScreenshot(Window window, string path)
    {
        if (window.Content is not FrameworkElement content)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var size = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(window.TryFindResource("ApplicationBackgroundBrush") as Brush ?? Brushes.Black, null, size);
            if (window.FlowDirection == FlowDirection.RightToLeft)
            {
                // The window mirrors right-to-left content; a VisualBrush doesn't.
                context.PushTransform(new MatrixTransform(-1, 0, 0, 1, size.Width, 0));
            }
            context.DrawRectangle(new VisualBrush(content), null, size);
        }

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(size.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(size.Height * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogUnhandled(_logger, e.Exception);
        if (_options.SmokeTest)
        {
            _mainWindow?.AllowClose();
            Shutdown(2);
            return;
        }

        // A tray app should survive a failing click handler; the error is in the log.
        e.Handled = true;
        _services.GetService<IToastService>()?.Show(Core.Models.ToastKind.Error, LocalizationSource.Service?["Toast_Error"] ?? "Error", e.Exception.Message);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "UI started (language {Culture}, theme {Theme}).")]
    private static partial void LogStarted(ILogger logger, string culture, string theme);

    [LoggerMessage(Level = LogLevel.Information, Message = "Smoke test passed: {Count} pages loaded.")]
    private static partial void LogSmokeTestPassed(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Smoke test failed.")]
    private static partial void LogSmokeTestFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on the UI thread.")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
