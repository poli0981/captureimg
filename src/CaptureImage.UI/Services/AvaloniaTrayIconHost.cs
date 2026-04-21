using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace CaptureImage.UI.Services;

/// <summary>
/// Avalonia-backed tray icon host. Draws its own icon at runtime via SkiaSharp so we don't
/// need to ship an <c>.ico</c> asset yet, wires a localized context menu, and implements
/// minimize-to-tray behaviour by intercepting the main window's <c>Closing</c> event.
/// </summary>
public sealed class AvaloniaTrayIconHost : ITrayIconHost
{
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settings;
    private readonly ILogger<AvaloniaTrayIconHost> _logger;

    private TrayIcon? _trayIcon;
    private Window? _mainWindow;
    private bool _disposed;

    public AvaloniaTrayIconHost(
        ILocalizationService localization,
        ISettingsStore settings,
        ILogger<AvaloniaTrayIconHost> logger)
    {
        _localization = localization;
        _settings = settings;
        _logger = logger;
    }

    public void Initialize(object mainWindow)
    {
        if (_disposed) return;
        _mainWindow = mainWindow as Window
            ?? throw new ArgumentException("mainWindow must be an Avalonia Window", nameof(mainWindow));

        _trayIcon = new TrayIcon
        {
            Icon = BuildRuntimeIconSafely(),
            ToolTipText = "CaptureImage",
            IsVisible = true,
            Menu = BuildMenu(),
        };

        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        var icons = new TrayIcons { _trayIcon };
        TrayIcon.SetIcons(Application.Current!, icons);

        _mainWindow.Closing += OnMainWindowClosing;
        _localization.PropertyChanged += OnLocalizationChanged;

        _logger.LogInformation("Tray icon initialized and visible.");
    }

    private WindowIcon? BuildRuntimeIconSafely()
    {
        try
        {
            return BuildRuntimeIcon();
        }
        catch (Exception ex)
        {
            // A SkiaSharp / native-asset failure here used to take the whole startup down.
            // Fall back to a null icon (Avalonia renders a default glyph) and log — the
            // user still gets a tray entry, just one without our branding.
            _logger.LogWarning(ex, "Failed to render runtime tray icon via SkiaSharp; falling back to default.");
            return null;
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        // `ResxLocalizationService.SetCulture` raises three events per switch
        // (Item[], CurrentCulture, CurrentFlowDirection). Rebuild the menu once, not
        // three times — Item[] is the canonical "all lookups may have changed" signal.
        if (e.PropertyName == "Item[]")
        {
            RefreshMenuLabels();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localization.PropertyChanged -= OnLocalizationChanged;
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
        }
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _logger.LogInformation("Tray icon disposed.");
    }

    // -- menu ----------------------------------------------------------------

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem(_localization["Tray_ShowWindow"]);
        showItem.Click += (_, _) => ShowMainWindow();
        menu.Add(showItem);

        var openFolderItem = new NativeMenuItem(_localization["Tray_OpenFolder"]);
        openFolderItem.Click += (_, _) => OpenCaptureFolder();
        menu.Add(openFolderItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem(_localization["Tray_Exit"]);
        exitItem.Click += (_, _) => QuitApp();
        menu.Add(exitItem);

        return menu;
    }

    private void RefreshMenuLabels()
    {
        if (_trayIcon?.Menu is null) return;
        // Rebuild the menu from scratch — simplest approach, and the menu is tiny.
        var newMenu = BuildMenu();
        _trayIcon.Menu = newMenu;
    }

    // -- actions -------------------------------------------------------------

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
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
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_disposed) return;
        if (!_settings.Current.UI.MinimizeToTray) return;

        // Swallow the close, hide instead. User can quit via tray menu.
        e.Cancel = true;
        _mainWindow?.Hide();
        _logger.LogDebug("Main window hidden to tray (MinimizeToTray enabled).");
    }

    // -- icon generation -----------------------------------------------------

    /// <summary>
    /// Draw a 32×32 PNG icon at runtime and wrap it in a <see cref="WindowIcon"/>. Keeps
    /// the repo free of binary assets until we have real branding.
    /// </summary>
    private static WindowIcon BuildRuntimeIcon()
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
            // Rounded rectangle body.
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

        // Shutter tab on top.
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
        return new WindowIcon(new MemoryStream(data.ToArray()));
    }
}
