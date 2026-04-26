using System;
using System.ComponentModel;
using System.Numerics;
using CaptureImage.UI.Views;
using CaptureImage.ViewModels;
using CaptureImage.ViewModels.Navigation;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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

    // How far the drawer is pushed off-screen when collapsed. Matches v1.2's translateX(48px).
    // The sign flips under RTL so the drawer always slides in toward its anchored edge.
    private const float DrawerOffsetX = 48f;

    // Show/hide timing matches v1.2-M7 (220 ms slide, 180 ms fade, CircularEaseOut-ish).
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan FadeDuration  = TimeSpan.FromMilliseconds(180);

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

        Root.Loaded += OnRootLoaded;
    }

    public MainWindowViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Wires the VM after construction. Window has no DataContext property in WinUI 3 —
    /// content elements (the root Grid) carry it instead. Called from App.OnLaunched
    /// after DI resolves the VM.
    /// </summary>
    public void SetViewModel(MainWindowViewModel vm)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        ViewModel = vm;
        Root.DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // The VM seeds SelectedNavItem to NavItems[0] in its constructor; mirror that
        // into the Frame so M1's blank window doesn't linger before the user clicks.
        if (vm.SelectedNavItem is { } first)
        {
            NavigateToNavItem(first);
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Push the drawer off-screen + invisible without animation. Subsequent
        // IsLogViewerVisible toggles drive the animated transition.
        var visual = ElementCompositionPreview.GetElementVisual(LogDrawer);
        visual.Offset = new Vector3(GetCollapsedOffsetX(), 0f, 0f);
        visual.Opacity = 0f;
        LogDrawer.IsHitTestVisible = false;
    }

    private float GetCollapsedOffsetX() =>
        Root.FlowDirection == FlowDirection.RightToLeft ? -DrawerOffsetX : DrawerOffsetX;

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
            // Pass App.Services through Frame.Navigate's parameter so each Page can
            // resolve its own VM from DI without taking a project reference back to
            // CaptureImage.App. Each Page reads it in OnNavigatedTo.
            ContentFrame.Navigate(pageType, App.Services);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is null) return;
        if (e.PropertyName == nameof(MainWindowViewModel.IsLogViewerVisible))
        {
            if (ViewModel.IsLogViewerVisible) ShowLogDrawer();
            else HideLogDrawer();
        }
    }

    private void ShowLogDrawer()
    {
        // Mark hit-testable up-front so a click during the slide registers correctly.
        LogDrawer.IsHitTestVisible = true;
        AnimateDrawer(targetOffsetX: 0f, targetOpacity: 1f);
    }

    private void HideLogDrawer()
    {
        AnimateDrawer(targetOffsetX: GetCollapsedOffsetX(), targetOpacity: 0f);
        // Drop hit-testing immediately — clicks during the fade-out aren't intentional.
        LogDrawer.IsHitTestVisible = false;
    }

    private void AnimateDrawer(float targetOffsetX, float targetOpacity)
    {
        var visual = ElementCompositionPreview.GetElementVisual(LogDrawer);
        var compositor = visual.Compositor;

        // CubicBezier(0.4, 0, 0.2, 1) — Material/Fluent-style "decelerate"; close to v1.2's
        // CircularEaseOut without the slight overshoot the literal circular curve gives at
        // the end. Visually indistinguishable for sub-300 ms motions.
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.4f, 0f), new Vector2(0.2f, 1f));

        var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(1f, new Vector3(targetOffsetX, 0f, 0f), easing);
        offsetAnim.Duration = SlideDuration;
        visual.StartAnimation("Offset", offsetAnim);

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1f, targetOpacity, easing);
        opacityAnim.Duration = FadeDuration;
        visual.StartAnimation("Opacity", opacityAnim);
    }
}
