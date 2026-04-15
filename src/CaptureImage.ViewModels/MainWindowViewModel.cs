using System.Collections.ObjectModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Dashboard;
using CaptureImage.ViewModels.Logs;
using CaptureImage.ViewModels.Navigation;
using CaptureImage.ViewModels.Settings;
using CaptureImage.ViewModels.Update;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels;

/// <summary>
/// Root VM for the main window. Exposes the nav rail items, the currently active child VM,
/// the localization service (so XAML bindings on the shell can use
/// <c>{Binding Localization[Key]}</c>), the toast collection the ToastHost overlay binds to,
/// and the log viewer drawer toggle.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavItemViewModel? _selectedNavItem;

    [ObservableProperty]
    private bool _isLogViewerVisible;

    public ILocalizationService Localization { get; }

    public LogViewerViewModel LogViewer { get; }

    public ObservableCollection<ToastItem> Toasts { get; }

    public ObservableCollection<NavItemViewModel> NavItems { get; }

    public MainWindowViewModel(
        INavigationService navigation,
        ILocalizationService localization,
        IToastService toastService,
        LogViewerViewModel logViewer)
    {
        _navigation = navigation;
        Localization = localization;
        Toasts = toastService.Visible;
        LogViewer = logViewer;
        _navigation.CurrentViewModelChanged += OnCurrentViewModelChanged;

        NavItems = new ObservableCollection<NavItemViewModel>
        {
            new("dashboard", "Nav_Dashboard", "\uE80F", typeof(DashboardViewModel), localization),
            new("update",    "Nav_Update",    "\uE895", typeof(UpdateViewModel),    localization),
            new("settings",  "Nav_Settings",  "\uE713", typeof(SettingsViewModel),  localization),
            new("about",     "Nav_About",     "\uE946", typeof(AboutViewModel),     localization),
        };

        // Activate the first item by default.
        SelectedNavItem = NavItems[0];
    }

    partial void OnSelectedNavItemChanged(NavItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        _navigation.NavigateTo(value.TargetViewModel);
    }

    private void OnCurrentViewModelChanged(object? sender, System.EventArgs e)
    {
        CurrentViewModel = _navigation.CurrentViewModel;
    }

    [RelayCommand]
    private void SelectNavItem(NavItemViewModel? item)
    {
        if (item is not null)
        {
            SelectedNavItem = item;
        }
    }

    [RelayCommand]
    private void ToggleLogViewer()
    {
        IsLogViewerVisible = !IsLogViewerVisible;
        if (IsLogViewerVisible)
        {
            LogViewer.EnsureHydrated();
        }
    }
}
