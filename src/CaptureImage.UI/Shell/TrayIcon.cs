// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CaptureImage.Core.Abstractions;
using CaptureImage.UI.Localization;
using CaptureImage.ViewModels.Services;
using CaptureImage.ViewModels.Shell;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace CaptureImage.UI.Shell;

/// <summary>
/// The notification-area icon: left click shows the window, right click opens the menu. The
/// icon gets a red dot while the hotkeys are armed. H.NotifyIcon re-adds it after Explorer restarts.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly ShellViewModel _viewModel;
    private readonly ILocalizationService _localization;
    private readonly TaskbarIcon _icon;
    private readonly ImageSource _idleIcon = LoadIcon("CaptureImage.ico");
    private readonly ImageSource _armedIcon = LoadIcon("CaptureImage-armed.ico");
    private bool _disposed;

    public TrayIcon(ShellViewModel viewModel, ILocalizationService localization)
    {
        _viewModel = viewModel;
        _localization = localization;
        _icon = new TaskbarIcon
        {
            IconSource = viewModel.IsArmed ? _armedIcon : _idleIcon,
            NoLeftClickDelay = true,
            LeftClickCommand = viewModel.ShowWindowCommand,
            DoubleClickCommand = viewModel.ShowWindowCommand,
            MenuActivation = PopupActivationMode.RightClick,
            ContextMenu = BuildMenu(viewModel),
        };
        _icon.SetBinding(TaskbarIcon.ToolTipTextProperty, new Binding(nameof(ShellViewModel.TrayToolTip)) { Source = viewModel, Mode = BindingMode.OneWay });
        LocalizationSource.Apply(_icon.ContextMenu);

        _viewModel.PropertyChanged += OnViewModelChanged;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>Add the icon to the notification area.</summary>
    public void Show() => _icon.ForceCreate(enablesEfficiencyMode: false);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _localization.CultureChanged -= OnCultureChanged;
        _icon.Dispose();
    }

    private static ContextMenu BuildMenu(ShellViewModel vm)
    {
        var menu = new ContextMenu();
        var show = Item("Tray_ShowWindow", vm.ShowWindowCommand, SymbolRegular.WindowApps24);
        show.FontWeight = FontWeights.SemiBold;
        menu.Items.Add(show);

        var arm = Item(null, vm.ToggleArmCommand, SymbolRegular.Keyboard24);
        arm.SetBinding(HeaderedItemsControl.HeaderProperty, new Binding(nameof(ShellViewModel.ArmMenuText)) { Source = vm, Mode = BindingMode.OneWay });
        menu.Items.Add(arm);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Tray_CaptureWindow", vm.CaptureWindowCommand, SymbolRegular.Window24));
        menu.Items.Add(Item("Tray_CaptureRegion", vm.CaptureRegionCommand, SymbolRegular.Crop24));
        menu.Items.Add(Item("Tray_CaptureMonitor", vm.CaptureMonitorCommand, SymbolRegular.Desktop24));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Tray_OpenFolder", vm.OpenOutputFolderCommand, SymbolRegular.FolderOpen24));
        menu.Items.Add(Item("Nav_Library", vm.NavigateCommand, SymbolRegular.ImageMultiple24, PageKeys.Library));
        menu.Items.Add(Item("Nav_Settings", vm.NavigateCommand, SymbolRegular.Settings24, PageKeys.Settings));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Tray_Exit", vm.ExitCommand, SymbolRegular.Power24));
        return menu;
    }

    private static MenuItem Item(string? key, ICommand command, SymbolRegular symbol, object? parameter = null)
    {
        var item = new MenuItem { Command = command, CommandParameter = parameter, Icon = new SymbolIcon(symbol) };
        if (key is not null)
        {
            item.SetBinding(HeaderedItemsControl.HeaderProperty, LocalizationSource.Bind(key));
            item.SetValue(System.Windows.Automation.AutomationProperties.AutomationIdProperty, key);
        }
        return item;
    }

    private static BitmapFrame LoadIcon(string name) =>
        BitmapFrame.Create(new Uri($"pack://application:,,,/CaptureImage.UI;component/Assets/{name}", UriKind.Absolute));

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsArmed))
        {
            _icon.IconSource = _viewModel.IsArmed ? _armedIcon : _idleIcon;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e) => _icon.Dispatcher.BeginInvoke(() => LocalizationSource.Apply(_icon.ContextMenu));
}
