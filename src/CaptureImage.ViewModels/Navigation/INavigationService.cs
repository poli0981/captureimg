namespace CaptureImage.ViewModels.Navigation;

/// <summary>
/// Abstraction for navigating between top-level view models.
/// The UI layer binds a <see cref="ContentControl"/> (or equivalent) to
/// <see cref="CurrentViewModel"/> and reacts to <see cref="CurrentViewModelChanged"/>.
/// </summary>
public interface INavigationService
{
    /// <summary>Currently active view model, or <c>null</c> before first navigation.</summary>
    ViewModelBase? CurrentViewModel { get; }

    /// <summary>Raised after <see cref="CurrentViewModel"/> changes.</summary>
    event EventHandler? CurrentViewModelChanged;

    /// <summary>Resolve <typeparamref name="TViewModel"/> from DI and make it current.</summary>
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    /// <summary>Resolve a view model by its CLR type and make it current.</summary>
    void NavigateTo(Type viewModelType);
}
