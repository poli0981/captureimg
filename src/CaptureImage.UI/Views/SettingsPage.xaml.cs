using System;
using CaptureImage.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureImage.UI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is IServiceProvider services)
        {
            DataContext = services.GetRequiredService<SettingsViewModel>();
        }
    }
}
