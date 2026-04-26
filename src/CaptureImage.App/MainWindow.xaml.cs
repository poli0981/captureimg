using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace CaptureImage.App;

public sealed partial class MainWindow : Window
{
    // Logical dimensions carried over from v1.2-M7. AppWindow.Resize takes physical
    // pixels — DPI scaling refinement is M3's problem when the real shell lands.
    private const int InitialWidth = 1200;
    private const int InitialHeight = 720;

    public MainWindow()
    {
        InitializeComponent();
        Title = "CaptureImage";

        // Fluent 2 backdrop. Mica picks up the Win11 desktop wallpaper tint and falls
        // back gracefully to a solid system color where Mica isn't available
        // (older Windows builds, RDP sessions, etc.).
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };

        // Borderless title bar — drag region defaults to the strip above content,
        // which is enough for the M2 placeholder. M3's NavigationView shell will
        // claim explicit drag regions.
        ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));
    }
}
