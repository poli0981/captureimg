using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.UI.Views;
using CaptureImage.ViewModels.Preview;
using SkiaSharp;

namespace CaptureImage.UI.Services;

/// <summary>
/// WinUI 3-backed implementation of <see cref="IPreviewPresenter"/>. Encodes the captured
/// frame to PNG bytes (reusing SkiaSharp), opens a non-modal <see cref="PreviewWindow"/>,
/// and awaits the user decision via the window's <see cref="PreviewWindow.ResultAsync"/>
/// task. Replaces v1.2's <c>AvaloniaPreviewPresenter</c> with the same external contract.
/// </summary>
public sealed class PreviewPresenter : IPreviewPresenter
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ILocalizationService _localization;

    public PreviewPresenter(IUIThreadDispatcher dispatcher, ILocalizationService localization)
    {
        _dispatcher = dispatcher;
        _localization = localization;
    }

    public async Task<bool> ShowAsync(
        CapturedFrame frame,
        GameTarget target,
        CancellationToken cancellationToken = default)
    {
        // Encode once for the preview image. PNG only — preview is display-only; the real
        // save path goes through the orchestrator with the user-chosen format.
        var pngBytes = EncodeAsPngBytes(frame);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.Post(() =>
        {
            try
            {
                var vm = new PreviewViewModel(_localization);
                vm.SetFrame(frame, target, pngBytes);

                var window = new PreviewWindow();
                window.SetViewModel(vm);
                window.Activate();

                _ = WaitForResultAsync(window, vm, tcs);
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

    private static async Task WaitForResultAsync(
        PreviewWindow window,
        PreviewViewModel vm,
        TaskCompletionSource<bool> tcs)
    {
        try
        {
            var result = await window.ResultAsync.ConfigureAwait(true);
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            // Detach the VM's Localization.PropertyChanged subscription so each preview
            // doesn't leak one handler into the singleton localization service.
            vm.Dispose();
        }
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

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
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
