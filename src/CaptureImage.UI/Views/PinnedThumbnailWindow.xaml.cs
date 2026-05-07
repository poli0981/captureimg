using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Theming;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace CaptureImage.UI.Views;

/// <summary>
/// Small always-on-top floating window that shows a thumbnail of the most recent capture.
/// Each capture spawns a fresh instance so users can pin several side-by-side.
/// </summary>
public sealed partial class PinnedThumbnailWindow : Window
{
    private const int InitialWidth = 280;
    private const int InitialHeight = 220;

    private readonly ILocalizationService _localization;
    private ISettingsStore? _settings;
    private string? _filePath;

    /// <summary>Localization service exposed for XAML <c>{Binding Localization[...]}</c>.</summary>
    public ILocalizationService Localization => _localization;

    public PinnedThumbnailWindow(ILocalizationService localization)
    {
        InitializeComponent();
        _localization = localization;
        Root.DataContext = this;

        Title = "CaptureImage";
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));

        // OverlappedPresenter.IsAlwaysOnTop pins the window above other top-level windows
        // without needing SetWindowPos(HWND_TOPMOST). Drop the maximize button — the
        // thumbnail is meant to stay small.
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
        }

        Closed += OnClosed;
    }

    public void SetThumbnail(byte[] pngBytes, string? filePath, string targetName)
    {
        _filePath = filePath;
        TitleText.Text = targetName;

        var bitmap = new BitmapImage();
        using (var stream = new InMemoryRandomAccessStream())
        {
            stream.WriteAsync(pngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            stream.Seek(0);
            bitmap.SetSource(stream);
        }
        ThumbnailImage.Source = bitmap;
    }

    public void AttachThemeStore(ISettingsStore settings)
    {
        _settings = settings;
        ThemeApplicator.Apply(Content as FrameworkElement, settings.Current.Theme);
        settings.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_settings is null) return;
        ThemeApplicator.Apply(Content as FrameworkElement, _settings.Current.Theme);
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_filePath)) return;
        try
        {
            // /select highlights the file in Explorer rather than opening it.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_filePath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Best-effort — failure here just means the user clicks Open and nothing happens.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_settings is not null)
        {
            _settings.Changed -= OnSettingsChanged;
            _settings = null;
        }
    }
}
