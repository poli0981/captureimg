using System;
using CaptureImage.Core.Abstractions;
using CaptureImage.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CaptureImage.UI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        BrowseOutputButton.Click += OnBrowseOutputClicked;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is IServiceProvider services)
        {
            DataContext = services.GetRequiredService<SettingsViewModel>();
        }
    }

    private async void OnBrowseOutputClicked(object sender, RoutedEventArgs e)
    {
        // WinUI 3 unpackaged: FolderPicker requires explicit HWND association via
        // WinRT.Interop.InitializeWithWindow. Without it the picker either silently
        // no-ops or crashes with "Invalid window handle" depending on SDK version.
        // The window handle comes from the active main window — we walk up via
        // XamlRoot since SettingsPage doesn't carry a Window reference itself.
        var hwnd = WindowNative.GetWindowHandle(GetWindowFromXamlRoot());
        if (hwnd == IntPtr.Zero) return;

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        // Bug-compat: FileTypeFilter must have at least one entry on FolderPicker too,
        // even though folders aren't filtered by extension. WinAppSDK throws otherwise.
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;

        if (DataContext is SettingsViewModel vm)
        {
            vm.OutputDirectory = folder.Path;
        }
    }

    /// <summary>
    /// Pull the host Window from XamlRoot. Returns a placeholder Window-typed object
    /// (we only need its HWND); falls back to App.Services-resolved approach if XamlRoot
    /// hasn't materialized yet.
    /// </summary>
    private object GetWindowFromXamlRoot()
    {
        // XamlRoot.ContentIslandEnvironment doesn't expose the host Window directly,
        // but the XamlRoot is created by a Window so we can walk up via the framework
        // by resolving the App.Services-singleton ITrayIconHost (which holds a window
        // reference) — except that's too tight a coupling.
        //
        // Pragmatic path: use Application.Current to get the App, then expose the main
        // window via a static helper on App. CaptureImage.UI doesn't reference
        // CaptureImage.App, so we go through reflection via App.Services hack? No — too
        // fragile. Better: stash the active main window on a static accessor that App
        // sets at startup. For M7a, use the simpler pattern: the SettingsPage is hosted
        // inside the main window, so XamlRoot.HostWindow is what we want — but the
        // public API for that lands in WinAppSDK 1.6+. For 1.5 we need this workaround.
        return WindowHostHelper.MainWindow
            ?? throw new InvalidOperationException(
                "WindowHostHelper.MainWindow not set — App.OnLaunched must register the host before SettingsPage is shown.");
    }
}

/// <summary>
/// Tiny static bridge that lets CaptureImage.UI Pages reach the host Window without
/// taking a project reference back to CaptureImage.App. Set by App.OnLaunched once the
/// main window exists.
/// </summary>
public static class WindowHostHelper
{
    public static Microsoft.UI.Xaml.Window? MainWindow { get; set; }
}
