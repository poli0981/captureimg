using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CaptureImage.ViewModels.Preview;

/// <summary>
/// VM for the preview window. Holds the captured frame's dimensions + PNG bytes so the view
/// can render it without asking the engine to re-encode.
/// </summary>
public sealed partial class PreviewViewModel : ViewModelBase
{
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
