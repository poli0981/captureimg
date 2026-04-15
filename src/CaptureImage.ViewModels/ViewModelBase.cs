using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels;

/// <summary>
/// Base type for all view models. Sits on top of <see cref="ObservableObject"/>
/// so every VM gets source-generated <see cref="System.ComponentModel.INotifyPropertyChanged"/>.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
