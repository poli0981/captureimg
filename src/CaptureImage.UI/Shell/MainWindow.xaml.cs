// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Windows;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.UI.Interop;
using CaptureImage.UI.Localization;
using CaptureImage.UI.Pages;
using CaptureImage.UI.Services;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Shell;
using CommunityToolkit.Mvvm.Messaging;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace CaptureImage.UI.Shell;

/// <summary>
/// The main window. Closing it hides to the tray (unless that option is off); its placement
/// is remembered; only the visible page's view model does background refreshes.
/// </summary>
public partial class MainWindow : FluentWindow, IRecipient<NavigateMessage>
{
    private static readonly IReadOnlyDictionary<string, Type> PageTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [PageKeys.Dashboard] = typeof(DashboardPage),
        [PageKeys.Library] = typeof(LibraryPage),
        [PageKeys.Profiles] = typeof(ProfilesPage),
        [PageKeys.Settings] = typeof(SettingsPage),
        [PageKeys.Logs] = typeof(LogsPage),
        [PageKeys.Update] = typeof(UpdatePage),
        [PageKeys.About] = typeof(AboutPage),
    };

    private readonly ISettingsStore _settings;
    private readonly IPerformanceModeService _performance;
    private IActivatablePage? _currentPage;
    private bool _allowClose;

    public MainWindow(
        ShellViewModel viewModel,
        ShellContext shell,
        INavigationViewPageProvider pages,
        ISnackbarService snackbar,
        IContentDialogService dialogs,
        IMessenger messenger,
        ISettingsStore settings,
        IPerformanceModeService performance)
    {
        _settings = settings;
        _performance = performance;
        DataContext = viewModel;
        InitializeComponent();
        LocalizationSource.Attach(this);
        RestorePlacement(settings.Current.UI.MainWindow);

        shell.MainWindow = this;
        Navigation.SetPageProviderService(pages);
        Navigation.Navigated += OnNavigated;
        snackbar.SetSnackbarPresenter(SnackbarPresenter);
        dialogs.SetDialogHost(DialogHost);
        messenger.Register(this);

        Loaded += (_, _) => Navigation.Navigate(typeof(DashboardPage));
        IsVisibleChanged += (_, _) => UpdateActivity();
        StateChanged += (_, _) => UpdateActivity();
    }

    /// <summary>The page types, in menu order (smoke test and UI tests visit them all).</summary>
    public static IReadOnlyCollection<Type> AllPageTypes => [.. PageTypes.Values];

    /// <summary>Let the next close really close (app exit), instead of hiding to the tray.</summary>
    public void AllowClose() => _allowClose = true;

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        WindowInterop.BringToFront(this);
    }

    public bool NavigateTo(Type pageType) => Navigation.Navigate(pageType);

    public Type? CurrentPageType => _currentPage?.GetType();

    public void Receive(NavigateMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Dispatcher.BeginInvoke(() =>
        {
            ShowFromTray();
            if (message.PageKey is { } key && PageTypes.TryGetValue(key, out var type))
            {
                Navigation.Navigate(type);
            }
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SavePlacement();
        if (!_allowClose)
        {
            e.Cancel = true;
            if (_settings.Current.UI.MinimizeToTray)
            {
                Hide();
            }
            else
            {
                (Application.Current as App)?.RequestExit();
            }
            return;
        }

        base.OnClosing(e);
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        var next = args.Page as IActivatablePage;
        if (ReferenceEquals(next, _currentPage))
        {
            return;
        }

        _currentPage?.SetActive(false);
        _currentPage = next;
        UpdateActivity();
    }

    private void UpdateActivity()
    {
        var onScreen = IsVisible && WindowState != WindowState.Minimized;
        _currentPage?.SetActive(onScreen);
        _performance.SetBackground(!IsVisible);
    }

    private void RestorePlacement(WindowPlacement? placement)
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Min(Width, area.Width * 0.92);
        Height = Math.Min(Height, area.Height * 0.92);
        if (placement is null || placement.Width < MinWidth || placement.Height < MinHeight)
        {
            return;
        }

        // Only restore a rectangle that is still (mostly) on a connected screen.
        var saved = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var desktop = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var visible = Rect.Intersect(saved, desktop);
        if (visible.IsEmpty || visible.Width < 200 || visible.Height < 120)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = saved.Left;
        Top = saved.Top;
        Width = saved.Width;
        Height = saved.Height;
        if (placement.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SavePlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left) || bounds.Width <= 0)
        {
            return;
        }

        var placement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
        _settings.Update(s => s with { UI = s.UI with { MainWindow = placement } });
    }
}
