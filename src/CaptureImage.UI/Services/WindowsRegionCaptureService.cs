using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.UI.Theming;
using CaptureImage.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace CaptureImage.UI.Services;

/// <summary>
/// Region-capture flow:
///   1. Capture the primary monitor pixels in one shot via GDI <c>CopyFromScreen</c>.
///   2. Encode that screenshot as PNG so the overlay can show it as a background.
///   3. Open <see cref="RegionSelectorOverlay"/> — user drags a rectangle (Esc cancels).
///   4. Crop the original BGRA buffer to the user's rectangle and return a
///      <see cref="CapturedFrame"/> ready to feed into the existing save pipeline.
///
/// Capturing the screen BEFORE showing the overlay means the overlay never appears
/// inside the screenshot.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegionCaptureService : IRegionCaptureService
{
    private readonly IUIThreadDispatcher _dispatcher;
    private readonly ISettingsStore _settings;
    private readonly ILocalizationService _localization;
    private readonly ILogger<WindowsRegionCaptureService> _logger;

    public WindowsRegionCaptureService(
        IUIThreadDispatcher dispatcher,
        ISettingsStore settings,
        ILocalizationService localization,
        ILogger<WindowsRegionCaptureService> logger)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _localization = localization;
        _logger = logger;
    }

    public async Task<CapturedFrame?> SelectAndCaptureAsync(CancellationToken cancellationToken = default)
    {
        // Capture the primary monitor immediately so the overlay is never inside the shot.
        var (bgraPixels, screenWidth, screenHeight) = CaptureScreen();
        if (bgraPixels is null) return null;

        var pngBytes = EncodePng(bgraPixels, screenWidth, screenHeight);

        var tcs = new TaskCompletionSource<RegionSelectionResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _dispatcher.Post(() =>
        {
            try
            {
                var hint = _localization["Region_DragHint"];
                var overlay = new RegionSelectorOverlay(pngBytes, screenWidth, screenHeight, hint);
                ThemeApplicator.Apply(overlay.Content as FrameworkElement, _settings.Current.Theme);
                overlay.Activate();

                _ = WaitForResultAsync(overlay, tcs);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        using (cancellationToken.Register(() => tcs.TrySetResult(null)))
        {
            var result = await tcs.Task.ConfigureAwait(false);
            if (result is null) return null;

            // Clamp to screen bounds — the overlay can only land within them, but
            // belt+braces in case of float-rounding edge cases.
            var x = Math.Clamp(result.X, 0, screenWidth - 1);
            var y = Math.Clamp(result.Y, 0, screenHeight - 1);
            var w = Math.Clamp(result.Width,  1, screenWidth - x);
            var h = Math.Clamp(result.Height, 1, screenHeight - y);

            return CropToBgraFrame(bgraPixels, screenWidth, screenHeight, x, y, w, h);
        }
    }

    private static async Task WaitForResultAsync(
        RegionSelectorOverlay overlay,
        TaskCompletionSource<RegionSelectionResult?> tcs)
    {
        try
        {
            var r = await overlay.ResultAsync.ConfigureAwait(true);
            tcs.TrySetResult(r);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    private (byte[]? Pixels, int Width, int Height) CaptureScreen()
    {
        // GetSystemMetrics SM_CXSCREEN / SM_CYSCREEN — primary monitor in physical pixels
        // on a per-monitor-DPI-aware app. v1.5 ships single-monitor; multi-monitor +
        // DPI per monitor lands in v1.6.
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("Could not determine screen dimensions for region capture.");
            return (null, 0, 0);
        }

        try
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            }

            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var bytes = new byte[stride * height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                // GDI 32bppArgb on little-endian is BGRA in memory — same layout the
                // existing encoders expect. Pass through unchanged.
                return (bytes, width, height);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Region capture failed while reading screen pixels.");
            return (null, 0, 0);
        }
    }

    private static byte[] EncodePng(byte[] bgraPixels, int width, int height)
    {
        var stride = bgraPixels.Length / height;
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            // If the source stride matches the GDI bitmap stride we can blit; otherwise
            // copy row-by-row to honour the source padding.
            if (stride == data.Stride)
            {
                Marshal.Copy(bgraPixels, 0, data.Scan0, bgraPixels.Length);
            }
            else
            {
                var rowLen = width * 4;
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(bgraPixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), rowLen);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static CapturedFrame CropToBgraFrame(
        byte[] sourceBgra, int srcWidth, int srcHeight,
        int x, int y, int width, int height)
    {
        var srcStride = sourceBgra.Length / srcHeight;
        var dstStride = width * 4;
        var dst = new byte[dstStride * height];
        for (var row = 0; row < height; row++)
        {
            var srcOffset = (y + row) * srcStride + x * 4;
            var dstOffset = row * dstStride;
            Buffer.BlockCopy(sourceBgra, srcOffset, dst, dstOffset, dstStride);
        }
        return new CapturedFrame(width, height, dstStride, dst);
    }

    private static class NativeMethods
    {
        internal const int SM_CXSCREEN = 0;
        internal const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int nIndex);
    }
}
