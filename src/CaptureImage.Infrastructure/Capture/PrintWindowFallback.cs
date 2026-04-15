using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using CaptureImage.Infrastructure.Processes.Interop;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// GDI-based fallback capture for windows where WGC misbehaves (old D3D9 exclusive fullscreen,
/// WinForms apps that bypass DWM thumbnails, etc.). Slower and CPU-only, but doesn't need a
/// D3D device and is easier for the OS to refuse in reasonable ways.
/// </summary>
/// <remarks>
/// Pipeline:
/// <list type="number">
///   <item>Query the window's client rect.</item>
///   <item>Create a compatible DC + compatible bitmap sized to the client area.</item>
///   <item>Call <c>PrintWindow(hwnd, memDC, PW_RENDERFULLCONTENT)</c>.</item>
///   <item>Read back the bitmap bits via <c>GetDIBits</c> as BGRA 32bpp, top-down.</item>
///   <item>Check for an all-black frame (common failure mode) and fail loudly if detected.</item>
/// </list>
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class PrintWindowFallback
{
    private readonly ILogger<PrintWindowFallback> _logger;

    public PrintWindowFallback(ILogger<PrintWindowFallback> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Capture the target's client area as a <see cref="CapturedFrame"/>. Throws
    /// <see cref="CaptureException"/> on any failure so the orchestrator can log + toast.
    /// </summary>
    public CapturedFrame Capture(GameTarget target)
    {
        if (target.WindowHandle == 0)
        {
            throw new CaptureException(CaptureError.TargetGone, "PrintWindow: target window handle is zero.");
        }

        var hwnd = (IntPtr)target.WindowHandle;

        if (!Gdi32.GetClientRect(hwnd, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new CaptureException(CaptureError.TargetGone, "PrintWindow: GetClientRect failed.");
        }

        var width = rect.Width;
        var height = rect.Height;

        var windowDc = Gdi32.GetDC(hwnd);
        if (windowDc == IntPtr.Zero)
        {
            throw new CaptureException(CaptureError.GraphicsDeviceFailure, "PrintWindow: GetDC returned null.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;

        try
        {
            memoryDc = Gdi32.CreateCompatibleDC(windowDc);
            if (memoryDc == IntPtr.Zero)
            {
                throw new CaptureException(CaptureError.GraphicsDeviceFailure, "PrintWindow: CreateCompatibleDC failed.");
            }

            bitmap = Gdi32.CreateCompatibleBitmap(windowDc, width, height);
            if (bitmap == IntPtr.Zero)
            {
                throw new CaptureException(CaptureError.GraphicsDeviceFailure, "PrintWindow: CreateCompatibleBitmap failed.");
            }

            var oldBitmap = Gdi32.SelectObject(memoryDc, bitmap);
            try
            {
                if (!Gdi32.PrintWindow(hwnd, memoryDc, Gdi32.PW_CLIENTONLY | Gdi32.PW_RENDERFULLCONTENT))
                {
                    throw new CaptureException(CaptureError.GraphicsDeviceFailure, "PrintWindow returned FALSE.");
                }

                var pixels = ReadBits(memoryDc, bitmap, width, height);
                if (IsAllBlack(pixels))
                {
                    throw new CaptureException(
                        CaptureError.FallbackProducedBlackFrame,
                        "PrintWindow returned an all-black frame (target likely uses hardware surface or DRM).");
                }

                _logger.LogDebug("PrintWindow fallback captured {W}x{H} frame.", width, height);
                return new CapturedFrame(width, height, width * 4, pixels);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                {
                    Gdi32.SelectObject(memoryDc, oldBitmap);
                }
            }
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                Gdi32.DeleteObject(bitmap);
            }
            if (memoryDc != IntPtr.Zero)
            {
                Gdi32.DeleteDC(memoryDc);
            }
            Gdi32.ReleaseDC(hwnd, windowDc);
        }
    }

    /// <summary>
    /// Read the bitmap as BGRA8 top-down (negative height trick) so the row order matches
    /// the WGC engine output and the encoders don't need a separate flip path.
    /// </summary>
    private static byte[] ReadBits(IntPtr memoryDc, IntPtr bitmap, int width, int height)
    {
        var info = new Gdi32.BitmapInfo
        {
            bmiHeader = new Gdi32.BitmapInfoHeader
            {
                biSize = Marshal.SizeOf<Gdi32.BitmapInfoHeader>(),
                biWidth = width,
                biHeight = -height, // negative => top-down
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Gdi32.BI_RGB,
                biSizeImage = width * height * 4,
            },
            bmiColors = new int[4],
        };

        var pixels = new byte[width * height * 4];
        var copied = Gdi32.GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, Gdi32.DIB_RGB_COLORS);
        if (copied == 0)
        {
            throw new CaptureException(CaptureError.GraphicsDeviceFailure, "PrintWindow: GetDIBits returned 0.");
        }
        return pixels;
    }

    /// <summary>
    /// Scan the pixel buffer for any non-zero RGB byte. Early-exits on first non-black pixel.
    /// Ignores the alpha channel because GDI bitmaps often leave alpha at 0.
    /// </summary>
    private static bool IsAllBlack(byte[] bgraPixels)
    {
        for (var i = 0; i + 3 < bgraPixels.Length; i += 4)
        {
            if (bgraPixels[i] != 0 || bgraPixels[i + 1] != 0 || bgraPixels[i + 2] != 0)
            {
                return false;
            }
        }
        return true;
    }
}
