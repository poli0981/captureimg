using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CaptureImage.ViewModels.Navigation;

/// <summary>
/// Default <see cref="INavigationService"/>. Resolves view models from <see cref="IServiceProvider"/>
/// so each navigation gets a fresh scoped instance; cross-cutting state lives on singleton services instead.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<NavigationService> _logger;
    private ViewModelBase? _currentViewModel;

    public NavigationService(IServiceProvider services, ILogger<NavigationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public ViewModelBase? CurrentViewModel => _currentViewModel;

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        => NavigateTo(typeof(TViewModel));

    public void NavigateTo(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
        {
            throw new ArgumentException(
                $"Type {viewModelType.FullName} does not derive from {nameof(ViewModelBase)}.",
                nameof(viewModelType));
        }

        var vm = (ViewModelBase)_services.GetRequiredService(viewModelType);
        _currentViewModel = vm;
        _logger.LogInformation("Navigated to {ViewModel}", viewModelType.Name);
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
