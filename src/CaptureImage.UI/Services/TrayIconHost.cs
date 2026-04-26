using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using CaptureImage.Core.Abstractions;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using WinRT.Interop;

namespace CaptureImage.UI.Services;

/// <summary>
/// WinUI 3 tray-icon host backed by <c>H.NotifyIcon.WindowsAppSDK</c>. Draws its own icon
/// at runtime via SkiaSharp (matches v1.2 — keeps the repo free of binary assets), wires
/// a localized context menu, and implements minimize-to-tray by intercepting the main
/// window's <see cref="AppWindow.Closing"/> event.
/// </summary>
public sealed class TrayIconHost : ITrayIconHost
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly ILogger<TrayIconHost> _logger;

    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;
    private AppWindow? _appWindow;
    private bool _disposed;

    public TrayIconHost(
        ILocalizationService localization,
        ISettingsStore settings,
        ILogger<TrayIconHost> logger)
    {
        _localization = localization;
        _settings = settings;
        _logger = logger;
    }

    public void Initialize(object mainWindow)
    {
        if (_disposed) return;
        _mainWindow = mainWindow as Window
            ?? throw new ArgumentException("mainWindow must be a WinUI 3 Window", nameof(mainWindow));

        var hwnd = WindowNative.GetWindowHandle(_mainWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "CaptureImage",
            ContextFlyout = BuildMenu(),
        };

        // Render the icon at runtime via SkiaSharp PNG → BitmapImage. H.NotifyIcon converts
        // the ImageSource to a Win32 HICON internally. Failure is non-fatal: the tray
        // appears with the OS default glyph and a warning is logged.
        var iconSource = TryCreateIconSource();
        if (iconSource is not null)
        {
            _trayIcon.IconSource = iconSource;
        }

        _trayIcon.LeftClickCommand = new SimpleCommand(_ => ShowMainWindow());
        _trayIcon.ForceCreate();

        // close-to-tray: intercept AppWindow.Closing (Window.Closed has no cancel hook).
        _appWindow.Closing += OnAppWindowClosing;
        _localization.PropertyChanged += OnLocalizationChanged;

        _logger.LogInformation("Tray icon initialized and visible.");
    }

    private ImageSource? TryCreateIconSource()
    {
        // H.NotifyIcon.WinUI loads icons via a chain that ends in System.Drawing.Icon,
        // which only accepts .ico file streams — a runtime-generated PNG fails with
        // "Argument 'picture' must be a picture that can be used as an Icon". Generating
        // a real .ico (System.Drawing.Icon.Save round-trip via HICON) needs P/Invoke
        // cleanup that's overkill for M6's scope; the OS default glyph is functional in
        // the meantime. M7 polish will ship a proper .ico via SkiaSharp -> Bitmap ->
        // GetHicon -> Icon.FromHandle -> Save with DestroyIcon cleanup.
        try
        {
            // Touch BuildRuntimeIconPng so the SkiaSharp drawing code stays alive +
            // exercised under build; M7 swaps the consumer to write .ico instead.
            _ = BuildRuntimeIconPng();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Runtime tray-icon SkiaSharp draw failed; tray will use default glyph anyway.");
        }
        return null;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]" && _trayIcon is not null)
        {
            _trayIcon.ContextFlyout = BuildMenu();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localization.PropertyChanged -= OnLocalizationChanged;
        if (_appWindow is not null)
        {
            _appWindow.Closing -= OnAppWindowClosing;
        }
        if (_trayIcon is not null)
        {
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _logger.LogInformation("Tray icon disposed.");
    }

    // -- menu ----------------------------------------------------------------

    private MenuFlyout BuildMenu()
    {
        var menu = new MenuFlyout();

        var showItem = new MenuFlyoutItem { Text = _localization["Tray_ShowWindow"] };
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(showItem);

        var openFolderItem = new MenuFlyoutItem { Text = _localization["Tray_OpenFolder"] };
        openFolderItem.Click += (_, _) => OpenCaptureFolder();
        menu.Items.Add(openFolderItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = _localization["Tray_Exit"] };
        exitItem.Click += (_, _) => QuitApp();
        menu.Items.Add(exitItem);

        return menu;
    }

    // -- actions -------------------------------------------------------------

    private void ShowMainWindow()
    {
        if (_mainWindow is null || _appWindow is null) return;
        _appWindow.Show();
        _mainWindow.Activate();
    }

    private void OpenCaptureFolder()
    {
        var outputDir = !string.IsNullOrWhiteSpace(_settings.Current.Capture.OutputDirectory)
            ? _settings.Current.Capture.OutputDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "CaptureImage");
        Directory.CreateDirectory(outputDir);
        try
        {
            Process.Start(new ProcessStartInfo(outputDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open capture folder {Path}", outputDir);
        }
    }

    private void QuitApp()
    {
        Application.Current.Exit();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_disposed) return;
        if (!_settings.Current.UI.MinimizeToTray) return;

        // Swallow the close, hide instead. User can quit via tray menu.
        args.Cancel = true;
        sender.Hide();
        _logger.LogDebug("Main window hidden to tray (MinimizeToTray enabled).");
    }

    // -- icon generation -----------------------------------------------------

    /// <summary>Port of v1.2's BuildRuntimeIcon — same shape, returns PNG bytes.</summary>
    private static byte[] BuildRuntimeIconPng()
    {
        const int size = 32;
        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using (var body = new SKPaint
        {
            Color = SKColor.Parse("#FF6B35"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        })
        {
            var rect = new SKRect(3, 8, size - 3, size - 4);
            canvas.DrawRoundRect(rect, 4, 4, body);
        }

        using (var lens = new SKPaint
        {
            Color = SKColor.Parse("#1F2937"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        })
        {
            canvas.DrawCircle(size / 2f, size / 2f + 2, 6, lens);
        }

        using (var lensInner = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        })
        {
            canvas.DrawCircle(size / 2f, size / 2f + 2, 3, lensInner);
        }

        using (var tab = new SKPaint
        {
            Color = SKColor.Parse("#FF6B35"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        })
        {
            canvas.DrawRoundRect(new SKRect(12, 4, 20, 8), 2, 2, tab);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Tiny ICommand for H.NotifyIcon's LeftClickCommand — avoids pulling in
    /// CommunityToolkit's RelayCommand for a single click handler.
    /// </summary>
    private sealed class SimpleCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        public SimpleCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
