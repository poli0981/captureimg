// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Runtime.InteropServices;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Imaging;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// One-shot captures through Windows.Graphics.Capture (the API OBS uses for game capture):
/// windows and whole monitors, 8-bit or HDR (scRGB FP16).
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Only the needed rectangle is copied off the GPU (frame <c>ContentSize</c>, and the
///   client area of windows unless the frame is requested).</item>
///   <item>The frame wait honors cancellation and a timeout; a frame that arrives late is disposed.</item>
///   <item>On <c>DXGI_ERROR_DEVICE_REMOVED/RESET/HUNG</c> the device is recreated and the capture
///   retried once.</item>
///   <item>8-bit frames are forced opaque so saved images are never see-through.</item>
/// </list>
/// </remarks>
public sealed partial class WgcCaptureEngine : ICaptureEngine, IDisposable
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(3);

    private readonly D3D11DeviceManager _devices;
    private readonly ILogger<WgcCaptureEngine> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WgcCaptureEngine(D3D11DeviceManager devices, ILogger<WgcCaptureEngine> logger)
    {
        _devices = devices;
        _logger = logger;
    }

    public async Task<CapturedFrame> CaptureAsync(CaptureSource source, CaptureOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new CaptureException(CaptureError.NotSupported, "Windows.Graphics.Capture is not supported on this system.");
        }

        ValidateSource(source);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                return await CaptureOnceAsync(source, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsDeviceLost(ex))
            {
                LogDeviceLost(ex);
                _devices.Invalidate();
                return await CaptureOnceAsync(source, options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not CaptureException and not OperationCanceledException)
        {
            throw Classify(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private static void ValidateSource(CaptureSource source)
    {
        switch (source)
        {
            case WindowCaptureSource { WindowHandle: var hwnd }:
                if (hwnd == 0 || !PInvoke.IsWindow(new HWND(hwnd)))
                {
                    throw new CaptureException(CaptureError.TargetGone, "The window no longer exists.");
                }
                if (PInvoke.IsIconic(new HWND(hwnd)))
                {
                    throw new CaptureException(CaptureError.TargetMinimized, "The window is minimized.");
                }
                break;
            case MonitorCaptureSource { MonitorHandle: 0 }:
                throw new CaptureException(CaptureError.TargetGone, "No monitor handle.");
        }
    }

    private async Task<CapturedFrame> CaptureOnceAsync(CaptureSource source, CaptureOptions options, CancellationToken cancellationToken)
    {
        var device = _devices.Acquire();
        var item = source switch
        {
            WindowCaptureSource w => WinRtCaptureInterop.CreateItemForWindow(w.WindowHandle),
            MonitorCaptureSource m => WinRtCaptureInterop.CreateItemForMonitor(m.MonitorHandle),
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };

        var pixelFormat = options.PreferHdr ? DirectXPixelFormat.R16G16B16A16Float : DirectXPixelFormat.B8G8R8A8UIntNormalized;
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device.WinRtDevice, pixelFormat, 1, item.Size);
        using var session = pool.CreateCaptureSession(item);
        DisableCaptureBorder(session);
        session.IsCursorCaptureEnabled = options.IncludeCursor;

        var arrived = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var frame = sender.TryGetNextFrame();
            if (frame is not null && !arrived.TrySetResult(frame))
            {
                frame.Dispose();
            }
        }

        pool.FrameArrived += OnFrameArrived;
        try
        {
            session.StartCapture();
            Direct3D11CaptureFrame frame;
            try
            {
                frame = await arrived.Task.WaitAsync(FrameTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new CaptureException(CaptureError.NoFrameArrived, $"No frame arrived within {FrameTimeout.TotalSeconds:0} s.");
            }

            using (frame)
            {
                var result = ReadBack(device, frame, source, options);
                LogCaptured(source is WindowCaptureSource ? "window" : "monitor", result.Width, result.Height, result.Format);
                return result;
            }
        }
        finally
        {
            pool.FrameArrived -= OnFrameArrived;
        }
    }

    private static CapturedFrame ReadBack(D3D11DeviceManager.Session device, Direct3D11CaptureFrame frame, CaptureSource source, CaptureOptions options)
    {
        using var texture = WinRtCaptureInterop.GetTexture(frame.Surface);
        var description = texture.Description;
        var content = new PixelRect(
            0,
            0,
            Math.Min(frame.ContentSize.Width, (int)description.Width),
            Math.Min(frame.ContentSize.Height, (int)description.Height));

        var area = content;
        if (source is WindowCaptureSource window && !options.IncludeWindowFrame
            && TryGetClientArea(window.WindowHandle, out var client))
        {
            var clipped = client.Intersect(content);
            area = clipped.IsEmpty ? content : clipped;
        }

        if (area.IsEmpty)
        {
            throw new CaptureException(CaptureError.NoFrameArrived, "The captured frame is empty.");
        }

        var staging = new Texture2DDescription
        {
            Width = (uint)area.Width,
            Height = (uint)area.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = description.Format,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };

        var isHdr = description.Format == Format.R16G16B16A16_Float;
        var bytesPerPixel = isHdr ? 8 : 4;
        var rowBytes = area.Width * bytesPerPixel;
        var pixels = GC.AllocateUninitializedArray<byte>(rowBytes * area.Height);

        using var stagingTexture = device.Device.CreateTexture2D(staging);
        device.Context.CopySubresourceRegion(
            stagingTexture, 0, 0, 0, 0, texture, 0,
            new Vortice.Mathematics.Box(area.X, area.Y, 0, area.Right, area.Bottom, 1));

        var mapped = device.Context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            for (var y = 0; y < area.Height; y++)
            {
                Marshal.Copy(mapped.DataPointer + (nint)(y * (long)mapped.RowPitch), pixels, y * rowBytes, rowBytes);
            }
        }
        finally
        {
            device.Context.Unmap(stagingTexture, 0);
        }

        var result = new CapturedFrame(area.Width, area.Height, rowBytes, pixels, isHdr ? FramePixelFormat.RgbaHalf : FramePixelFormat.Bgra8);
        FrameOperations.ForceOpaque(result);
        return result;
    }

    /// <summary>
    /// Client area relative to what WGC captures for a window (its DWM extended frame bounds),
    /// in physical pixels.
    /// </summary>
    private static unsafe bool TryGetClientArea(nint windowHandle, out PixelRect client)
    {
        client = default;
        var hwnd = new HWND(windowHandle);
        RECT frameBounds;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frameBounds, (uint)sizeof(RECT)).Failed
            || !PInvoke.GetClientRect(hwnd, out var clientRect))
        {
            return false;
        }

        var origin = new System.Drawing.Point(0, 0);
        if (!PInvoke.ClientToScreen(hwnd, ref origin))
        {
            return false;
        }

        client = new PixelRect(
            origin.X - frameBounds.left,
            origin.Y - frameBounds.top,
            clientRect.right - clientRect.left,
            clientRect.bottom - clientRect.top);
        return !client.IsEmpty && client.X >= 0 && client.Y >= 0;
    }

    private void DisableCaptureBorder(GraphicsCaptureSession session)
    {
        try
        {
            session.IsBorderRequired = false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or COMException or NotSupportedException)
        {
            LogBorderNotDisabled(ex);
        }
    }

    private static bool IsDeviceLost(Exception exception)
    {
        var code = exception switch
        {
            SharpGenException sharpGen => sharpGen.ResultCode.Code,
            COMException com => com.HResult,
            _ => 0,
        };
        return code == Vortice.DXGI.ResultCode.DeviceRemoved.Code || code == Vortice.DXGI.ResultCode.DeviceReset.Code || code == Vortice.DXGI.ResultCode.DeviceHung.Code;
    }

    private static CaptureException Classify(Exception exception)
    {
        const int EInvalidArg = unchecked((int)0x80070057);
        const int EAccessDenied = unchecked((int)0x80070005);

        if (IsDeviceLost(exception))
        {
            return new CaptureException(CaptureError.GraphicsDeviceFailure, "The graphics device was reset.", exception);
        }

        return exception switch
        {
            COMException { HResult: EInvalidArg } => new CaptureException(CaptureError.TargetGone, "The capture target is no longer valid.", exception),
            COMException { HResult: EAccessDenied } or UnauthorizedAccessException =>
                new CaptureException(CaptureError.ProtectedContent, "Windows refused to capture this content.", exception),
            SharpGenException or InvalidOperationException =>
                new CaptureException(CaptureError.GraphicsDeviceFailure, exception.Message, exception),
            _ => new CaptureException(CaptureError.Unknown, exception.Message, exception),
        };
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Captured {Source}: {Width}x{Height} {Format}.")]
    private partial void LogCaptured(string source, int width, int height, FramePixelFormat format);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The graphics device was lost; recreating it and retrying once.")]
    private partial void LogDeviceLost(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not hide the capture border.")]
    private partial void LogBorderNotDisabled(Exception exception);
}
