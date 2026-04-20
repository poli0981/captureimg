using System.Collections.ObjectModel;
using System.Reflection;
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

    /// <summary>Brand name shown in the nav rail header. Constant across cultures — brand names do not translate.</summary>
    public string AppTitle => "CaptureImage";

    /// <summary>
    /// Short semver pulled from the entry assembly's <c>Version</c> at runtime. Release builds
    /// set this via <c>/p:Version=&lt;git-tag&gt;</c> in release.yml; dev builds fall back to the
    /// <c>&lt;Version&gt;</c> element in CaptureImage.App.csproj. Zero-valued versions (a bare
    /// <c>dotnet build</c> with no version props) render as "dev".
    /// </summary>
    public string AppVersion
    {
        get
        {
            var info = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // Strip the +commit-sha suffix if present.
                var plus = info.IndexOf('+');
                return "v" + (plus >= 0 ? info[..plus] : info);
            }
            var v = Assembly.GetEntryAssembly()?.GetName().Version;
            if (v is null || v.Major == 0 && v.Minor == 0 && v.Build == 0) return "dev";
            return $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }

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

        // `{Binding Localization[Nav_LogViewer]}` on the log drawer toggle (and AppTitle /
        // AppVersion / tooltip bindings) need an explicit poke at the VM level when
        // culture changes — Avalonia's compiled indexer binding through an intermediate
        // property doesn't always refresh on the service's own Item[] notification.
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Localization));
        }
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
