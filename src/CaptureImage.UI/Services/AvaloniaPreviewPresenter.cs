using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.UI.Views;
using CaptureImage.ViewModels.Preview;
using SkiaSharp;

namespace CaptureImage.UI.Services;

/// <summary>
/// Avalonia-backed implementation of <see cref="IPreviewPresenter"/>. Encodes the captured
/// frame to PNG bytes (reusing SkiaSharp which we already ship), opens a modal
/// <see cref="PreviewWindow"/> as a child of the main window, and awaits the user decision.
/// </summary>
public sealed class AvaloniaPreviewPresenter : IPreviewPresenter
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILocalizationService _localization;

    public AvaloniaPreviewPresenter(IUIThreadDispatcher dispatcher, ILocalizationService localization)
    {
        _dispatcher = dispatcher;
        _localization = localization;
    }

    public async Task<bool> ShowAsync(
        CapturedFrame frame,
        GameTarget target,
        CancellationToken cancellationToken = default)
    {
        // Encode once for the preview image. Only PNG — previews are display-only, the real
        // save path goes through the orchestrator with the user-chosen format.
        var pngBytes = EncodeAsPngBytes(frame);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.Post(async () =>
        {
            try
            {
                var vm = new PreviewViewModel(_localization);
                vm.SetFrame(frame, target, pngBytes);

                var window = new PreviewWindow
                {
                    DataContext = vm,
                };

                var owner = GetMainWindow();
                if (owner is not null)
                {
                    // Fire-and-track: ShowDialog's own task completes when the window closes;
                    // we synchronize on the user's Save/Discard click via ResultAsync.
                    _ = window.ShowDialog(owner);
                }
                else
                {
                    window.Show();
                }
                var result = await window.ResultAsync.ConfigureAwait(true);
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        using (cancellationToken.Register(() => tcs.TrySetResult(false)))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    private static Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    private static byte[] EncodeAsPngBytes(CapturedFrame frame)
    {
        var pixels = frame.IsTightlyPacked ? frame.BgraPixels : frame.ToTightlyPacked();
        var rowBytes = frame.Width * 4;

        var info = new SKImageInfo(
            width: frame.Width,
            height: frame.Height,
            colorType: SKColorType.Bgra8888,
            alphaType: SKAlphaType.Premul);

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            if (!bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), rowBytes))
            {
                throw new InvalidOperationException("Failed to install pixels into SKBitmap for preview.");
            }
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream();
            encoded.SaveTo(ms);
            return ms.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }
}
