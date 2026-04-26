using CaptureImage.UI.Views;
using CaptureImage.ViewModels;
using CaptureImage.ViewModels.Navigation;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace CaptureImage.App;

public sealed partial class MainWindow : Window
{
    // Logical dimensions carried over from v1.2-M7. AppWindow.Resize takes physical
    // pixels — DPI scaling refinement is deferred to M7 polish.
    private const int InitialWidth = 1200;
    private const int InitialHeight = 720;

    public MainWindow()
    {
        InitializeComponent();
        Title = "CaptureImage";

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));
    }

    public MainWindowViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Wires the VM after construction. Window has no DataContext property in WinUI 3 —
    /// content elements (the root Grid) carry it instead. Called from App.OnLaunched
    /// after DI resolves the VM.
    /// </summary>
    public void SetViewModel(MainWindowViewModel vm)
    {
        ViewModel = vm;
        Root.DataContext = vm;

        // The VM seeds SelectedNavItem to NavItems[0] in its constructor; mirror that
        // into the Frame so M1's blank window doesn't linger before the user clicks.
        if (vm.SelectedNavItem is { } first)
        {
            NavigateToNavItem(first);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavItemViewModel item)
        {
            NavigateToNavItem(item);
        }
    }

    private void NavigateToNavItem(NavItemViewModel item)
    {
        var pageType = item.Key switch
        {
            "dashboard" => typeof(DashboardPage),
            "update"    => typeof(UpdatePage),
            "settings"  => typeof(SettingsPage),
            "about"     => typeof(AboutPage),
            _           => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
