using System;
using System.ComponentModel;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels.Preview;

/// <summary>
/// VM for the preview window. Holds the captured frame's dimensions + PNG bytes so the view
/// can render it without asking the engine to re-encode.
/// </summary>
public sealed partial class PreviewViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    public ILocalizationService Localization { get; }

    [ObservableProperty]
    private byte[]? _pngBytes;

    [ObservableProperty]
    private string _targetName = string.Empty;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    public PreviewViewModel(ILocalizationService localization)
    {
        Localization = localization;

        // Preview window is modal and short-lived, but the user can still switch language
        // from the tray menu while it is open. Raise Localization on culture change so the
        // `{Binding Localization[Preview_*]}` indexer bindings in PreviewWindow.axaml
        // re-resolve in place. Matches the v1.1.1 pattern used by every other VM.
        Localization.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed) return;
        if (e.PropertyName is "Item[]" or nameof(ILocalizationService.CurrentCulture))
        {
            OnPropertyChanged(nameof(Localization));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Localization.PropertyChanged -= OnLocalizationChanged;
    }

    /// <summary>
    /// Fill the VM from a captured frame. The frame is encoded to PNG bytes once so the
    /// WPF-style binding to <see cref="PngBytes"/> works with our existing
    /// <c>BytesToBitmapConverter</c>.
    /// </summary>
    public void SetFrame(CapturedFrame frame, GameTarget target, byte[] pngBytes)
    {
        PngBytes = pngBytes;
        TargetName = target.DisplayName;
        Width = frame.Width;
        Height = frame.Height;
    }
}
