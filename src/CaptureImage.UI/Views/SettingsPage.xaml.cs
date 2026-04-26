using System;
using CaptureImage.UI.Services;
using CaptureImage.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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

    private void OnBrowseOutputClicked(object sender, RoutedEventArgs e)
    {
        var window = WindowHostHelper.MainWindow;
        if (window is null) return;
        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero) return;

        // Use the Win32 IFileOpenDialog COM interface instead of
        // Windows.Storage.Pickers.FolderPicker — the WinRT picker throws
        // COMException 0x80004005 on PickSingleFolderAsync in unpackaged WinUI 3 apps
        // even with InitializeWithWindow set up correctly. IFileOpenDialog works
        // on all Windows versions without packaging requirements.
        var initial = (DataContext as SettingsViewModel)?.OutputDirectory;
        var folder = Win32FolderPicker.PickSingleFolder(hwnd, initial);
        if (folder is null) return;

        if (DataContext is SettingsViewModel vm)
        {
            vm.OutputDirectory = folder;
        }
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
