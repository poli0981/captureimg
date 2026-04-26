using System;
using CaptureImage.ViewModels.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CaptureImage.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is IServiceProvider services)
        {
            DataContext = services.GetRequiredService<DashboardViewModel>();
        }
    }
}
