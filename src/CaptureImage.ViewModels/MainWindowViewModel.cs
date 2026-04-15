using System.Collections.ObjectModel;
using CaptureImage.ViewModels.About;
using CaptureImage.ViewModels.Dashboard;
using CaptureImage.ViewModels.Navigation;
using CaptureImage.ViewModels.Settings;
using CaptureImage.ViewModels.Update;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CaptureImage.ViewModels;

/// <summary>
/// Root VM for the main window. Exposes the nav rail items and the currently active child VM.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavItem? _selectedNavItem;

    public ObservableCollection<NavItem> NavItems { get; }

    public MainWindowViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.CurrentViewModelChanged += OnCurrentViewModelChanged;

        NavItems = new ObservableCollection<NavItem>
        {
            new("dashboard", "Nav_Dashboard", "\uE80F", typeof(DashboardViewModel)),
            new("update",    "Nav_Update",    "\uE895", typeof(UpdateViewModel)),
            new("settings",  "Nav_Settings",  "\uE713", typeof(SettingsViewModel)),
            new("about",     "Nav_About",     "\uE946", typeof(AboutViewModel)),
        };

        // Activate the first item by default.
        SelectedNavItem = NavItems[0];
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value is null)
        {
            return;
        }

        _navigation.NavigateTo(value.TargetViewModel);
    }

    private void OnCurrentViewModelChanged(object? sender, EventArgs e)
    {
        CurrentViewModel = _navigation.CurrentViewModel;
    }

    [RelayCommand]
    private void SelectNavItem(NavItem? item)
    {
        if (item is not null)
        {
            SelectedNavItem = item;
        }
    }
}
