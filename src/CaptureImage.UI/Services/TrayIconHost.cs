using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
/// WinUI 3 tray-icon host backed by <c>H.NotifyIcon.WinUI</c>. Wires a localized context
/// menu, single-click-to-restore, and minimize-to-tray by intercepting the main window's
/// <see cref="AppWindow.Closing"/> event. Tray icon ships with the OS default glyph until
/// a real .ico round-trip lands (see feedback_h_notifyicon_winui_quirks.md).
/// </summary>
public sealed class TrayIconHost : ITrayIconHost
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly ILogger<TrayIconHost> _logger;

    private TaskbarIcon? _trayIcon;
    private Window? _mainWindow;
    private AppWindow? _appWindow;
    private IntPtr _hwnd;
    private bool _disposed;
    // Set by QuitApp so OnAppWindowClosing knows to allow the close instead of swallowing
    // it for minimize-to-tray. Without this, Application.Current.Exit() would hang
    // because the closing handler keeps cancelling the shutdown.
    private bool _quitting;

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

        _hwnd = WindowNative.GetWindowHandle(_mainWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "CaptureImage",
            ContextFlyout = BuildMenu(),
            // Single-click should fire immediately (no double-click wait). Default
            // H.NotifyIcon behavior is to delay LeftClick by ~500 ms looking for a
            // double-click, which made single-click-to-restore feel unresponsive.
            NoLeftClickDelay = true,
        };

        // Render the icon at runtime via SkiaSharp PNG (kept alive for v1.4 .ico path).
        TryCreateIconSource();

        // Both single + double click restore the window — covers either user habit.
        _trayIcon.LeftClickCommand = new SimpleCommand(_ =>
        {
            _logger.LogDebug("Tray left-click → restoring main window.");
            ShowMainWindow();
        });
        _trayIcon.DoubleClickCommand = new SimpleCommand(_ =>
        {
            _logger.LogDebug("Tray double-click → restoring main window.");
            ShowMainWindow();
        });
        _trayIcon.ForceCreate();

        // close-to-tray: intercept AppWindow.Closing (Window.Closed has no cancel hook).
        _appWindow.Closing += OnAppWindowClosing;
        _localization.PropertyChanged += OnLocalizationChanged;

        _logger.LogInformation("Tray icon initialized and visible.");
    }

    private void TryCreateIconSource()
    {
        // H.NotifyIcon.WinUI loads icons via a chain that ends in System.Drawing.Icon,
        // which only accepts .ico file streams — a runtime-generated PNG fails with
        // "Argument 'picture' must be a picture that can be used as an Icon". Generating
        // a real .ico (System.Drawing.Icon.Save round-trip via HICON) needs P/Invoke
        // cleanup that's deferred to v1.4. OS default glyph is functional in the meantime.
        try
        {
            // Touch BuildRuntimeIconPng so the SkiaSharp drawing code stays alive +
            // exercised under build; v1.4 swaps the consumer to write .ico instead.
            _ = BuildRuntimeIconPng();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Runtime tray-icon SkiaSharp draw failed; tray will use default glyph anyway.");
        }
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
        showItem.Click += (_, _) =>
        {
            _logger.LogDebug("Tray menu: Show clicked.");
            ShowMainWindow();
        };
        menu.Items.Add(showItem);

        var openFolderItem = new MenuFlyoutItem { Text = _localization["Tray_OpenFolder"] };
        openFolderItem.Click += (_, _) =>
        {
            _logger.LogDebug("Tray menu: Open Folder clicked.");
            OpenCaptureFolder();
        };
        menu.Items.Add(openFolderItem);

        menu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = _localization["Tray_Exit"] };
        exitItem.Click += (_, _) =>
        {
            _logger.LogDebug("Tray menu: Exit clicked.");
            QuitApp();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    // -- actions -------------------------------------------------------------

    private void ShowMainWindow()
    {
        if (_mainWindow is null || _appWindow is null) return;
        try
        {
            // AppWindow.Show brings the window back from Hide(). ShowWindow(SW_RESTORE)
            // also handles the "minimized via taskbar" case (AppWindow.Show alone leaves
            // a minimized window minimized). SetForegroundWindow forces Z-order to top
            // even when another app has focus — without it the window comes back behind
            // whatever the user was working in.
            _appWindow.Show();
            ShowWindow(_hwnd, SW_RESTORE);
            _mainWindow.Activate();
            SetForegroundWindow(_hwnd);
            _logger.LogDebug("Main window restored from tray.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore main window from tray.");
        }
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
        // Bypass MinimizeToTray so OnAppWindowClosing lets the close through.
        _quitting = true;
        Application.Current.Exit();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_disposed || _quitting) return;
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
