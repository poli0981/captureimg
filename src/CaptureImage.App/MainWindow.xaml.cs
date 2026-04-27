using System.ComponentModel;
using System.Numerics;
using CaptureImage.UI.Views;
using CaptureImage.ViewModels;
using CaptureImage.ViewModels.Navigation;
using Microsoft.UI;
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
    // pixels — DPI scaling refinement is deferred to v1.4 polish.
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
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        Root.Loaded += OnRootLoaded;
    }

    public MainWindowViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Apply the persisted theme preference to the window root. <c>System</c> falls back
    /// to <see cref="ElementTheme.Default"/> which lets the OS theme drive everything,
    /// including Mica. Called at startup and whenever <c>ISettingsStore.Changed</c> fires.
    /// Safe to call from any thread — internally hops to the dispatcher.
    /// </summary>
    public void ApplyTheme(string theme)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark"  => ElementTheme.Dark,
                    _       => ElementTheme.Default,
                };
            }
        });
    }

    public void SetViewModel(MainWindowViewModel vm)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        ViewModel = vm;
        Root.DataContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        if (vm.SelectedNavItem is { } first)
        {
            NavigateToNavItem(first);
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        // Enable the Composition `Translation` property on the drawer's visual. WITHOUT
        // this, animating Translation has no effect. WITH it, Translation is added on
        // top of the layout-computed position — which is what we want: the drawer stays
        // pinned to Grid.Column=1 (right 480 px) while Translation supplies the slide
        // offset. Setting `visual.Offset` directly (the v1.3-M5 approach) OVERRODE
        // layout position and made the drawer render from x=±48 absolute, ignoring its
        // Grid cell.
        ElementCompositionPreview.SetIsTranslationEnabled(LogDrawer, true);

        var visual = ElementCompositionPreview.GetElementVisual(LogDrawer);
        // Seed Translation to the collapsed offset; subsequent toggles animate via
        // visual.StartAnimation("Translation", ...).
        visual.Properties.InsertVector3("Translation", new Vector3(GetCollapsedOffsetX(), 0f, 0f));
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
        LogDrawer.IsHitTestVisible = true;
        AnimateDrawer(targetTranslationX: 0f, targetOpacity: 1f);
    }

    private void HideLogDrawer()
    {
        AnimateDrawer(targetTranslationX: GetCollapsedOffsetX(), targetOpacity: 0f);
        LogDrawer.IsHitTestVisible = false;
    }

    private void AnimateDrawer(float targetTranslationX, float targetOpacity)
    {
        var visual = ElementCompositionPreview.GetElementVisual(LogDrawer);
        var compositor = visual.Compositor;

        // CubicBezier(0.4, 0, 0.2, 1) — Material/Fluent decelerate. Visually
        // indistinguishable from v1.2's Avalonia CircularEaseOut for sub-300ms motion.
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.4f, 0f), new Vector2(0.2f, 1f));

        // Animate Translation (an additive transform that respects layout) instead of
        // Offset (which overrides layout). Target name "Translation" routes through the
        // Properties dictionary set up in OnRootLoaded.
        var translationAnim = compositor.CreateVector3KeyFrameAnimation();
        translationAnim.InsertKeyFrame(1f, new Vector3(targetTranslationX, 0f, 0f), easing);
        translationAnim.Duration = SlideDuration;
        translationAnim.Target = "Translation";
        visual.StartAnimation("Translation", translationAnim);

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1f, targetOpacity, easing);
        opacityAnim.Duration = FadeDuration;
        visual.StartAnimation("Opacity", opacityAnim);
    }
}
