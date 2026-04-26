using CaptureImage.ViewModels.Preview;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace CaptureImage.UI.Views;

/// <summary>
/// WinUI 3 preview window for a captured frame. Closed by the Save / Discard buttons or
/// the title-bar X — <see cref="ResultAsync"/> resolves true only when the user clicks Save.
/// </summary>
public sealed partial class PreviewWindow : Window
{
    private const int InitialWidth = 720;
    private const int InitialHeight = 560;

    private readonly TaskCompletionSource<bool> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PreviewWindow()
    {
        InitializeComponent();
        Title = "CaptureImage — Preview";

        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        ExtendsContentIntoTitleBar = true;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));

        Closed += OnClosed;
    }

    /// <summary>Task that completes when the user accepts (true) or discards (false).</summary>
    public Task<bool> ResultAsync => _result.Task;

    public void SetViewModel(PreviewViewModel vm)
    {
        Root.DataContext = vm;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _result.TrySetResult(true);
        Close();
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        _result.TrySetResult(false);
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Closing via the X button counts as discard.
        _result.TrySetResult(false);
    }
}
