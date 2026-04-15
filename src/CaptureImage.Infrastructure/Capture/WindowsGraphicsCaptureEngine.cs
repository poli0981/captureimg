using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// Captures a single frame of a target window using the
/// <see cref="Windows.Graphics.Capture"/> API (WGC). Hardware-accelerated, same API OBS uses
/// for Game Capture. Works for windowed and borderless-fullscreen games; fails (and raises
/// <see cref="CaptureError.ProtectedContent"/>) on protected content.
/// </summary>
/// <remarks>
/// <para><b>Pipeline:</b></para>
/// <list type="number">
///   <item>Create a <see cref="GraphicsCaptureItem"/> from the target HWND via <see cref="CaptureItemInterop"/>.</item>
///   <item>Wrap the shared D3D11 device in a WinRT <c>IDirect3DDevice</c>.</item>
///   <item>Create a free-threaded <see cref="Direct3D11CaptureFramePool"/> sized to the item.</item>
///   <item>Start a <see cref="GraphicsCaptureSession"/> (border/cursor disabled).</item>
///   <item>Wait for the first <c>FrameArrived</c> event and grab the frame.</item>
///   <item>Copy to a staging texture, map, read BGRA rows into a heap buffer, unmap.</item>
///   <item>Dispose session + pool + staging texture; return <see cref="CapturedFrame"/>.</item>
/// </list>
/// <para>
/// All WinRT objects (<c>GraphicsCaptureItem</c>, <c>Direct3D11CaptureFramePool</c>,
/// <c>GraphicsCaptureSession</c>, <c>Direct3D11CaptureFrame</c>) implement <see cref="IDisposable"/>
/// via their WinRT projections — we dispose them in reverse order of creation.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class WindowsGraphicsCaptureEngine : ICaptureEngine
{
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(2);

    private readonly D3D11DeviceManager _deviceManager;
    private readonly ILogger<WindowsGraphicsCaptureEngine> _logger;

    public WindowsGraphicsCaptureEngine(
        D3D11DeviceManager deviceManager,
        ILogger<WindowsGraphicsCaptureEngine> logger)
    {
        _deviceManager = deviceManager;
        _logger = logger;
    }

    public async Task<CapturedFrame> CaptureAsync(GameTarget target, CancellationToken cancellationToken = default)
    {
        if (target.WindowHandle == 0)
        {
            throw new CaptureException(CaptureError.TargetGone, "Target window handle is zero.");
        }

        // WGC availability check — fails on Windows versions older than Win10 1803.
        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new CaptureException(
                CaptureError.GraphicsDeviceFailure,
                "Windows.Graphics.Capture is not supported on this Windows build.");
        }

        GraphicsCaptureItem? item;
        try
        {
            item = CaptureItemInterop.CreateForWindow(target.WindowHandle);
        }
        catch (Exception ex)
        {
            throw new CaptureException(CaptureError.TargetGone,
                $"CreateForWindow failed for HWND 0x{target.WindowHandle:X}.", ex);
        }

        if (item is null)
        {
            throw new CaptureException(CaptureError.TargetGone, "GraphicsCaptureItem was null.");
        }

        var (d3dDevice, d3dContext) = _deviceManager.Get();
        var winrtDevice = Direct3D11Interop.CreateDirect3DDevice(d3dDevice);

        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;

        try
        {
            framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                winrtDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                numberOfBuffers: 1,
                size: item.Size);

            session = framePool.CreateCaptureSession(item);

            // Win11 22H2+: no yellow capture border.
            session.IsBorderRequired = false;
            session.IsCursorCaptureEnabled = false;

            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void OnFrameArrived(Direct3D11CaptureFramePool pool, object _)
            {
                try
                {
                    var nextFrame = pool.TryGetNextFrame();
                    if (nextFrame is not null)
                    {
                        tcs.TrySetResult(nextFrame);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            framePool.FrameArrived += OnFrameArrived;
            session.StartCapture();

            Direct3D11CaptureFrame frame;
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(FrameTimeout, cancellationToken)).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new CaptureException(
                        CaptureError.NoFrameArrived,
                        $"No frame arrived within {FrameTimeout.TotalSeconds:F1}s.");
                }
                frame = await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                framePool.FrameArrived -= OnFrameArrived;
            }

            try
            {
                return ReadBackFrame(d3dDevice, d3dContext, frame);
            }
            finally
            {
                frame.Dispose();
            }
        }
        catch (CaptureException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new CaptureException(CaptureError.Cancelled, "Capture was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WGC capture failed for HWND 0x{Hwnd:X}.", target.WindowHandle);
            throw new CaptureException(CaptureError.Unknown, "WGC capture failed: " + ex.Message, ex);
        }
        finally
        {
            session?.Dispose();
            framePool?.Dispose();
            // Note: `item` and `winrtDevice` are WinRT projections — they release automatically
            // when the RCW is collected. Explicit dispose is optional but not required here.
        }
    }

    /// <summary>
    /// Copy the GPU frame to a CPU-readable staging texture, map it, and read BGRA rows into
    /// a managed byte buffer. This is the only place we actually touch pixel data.
    /// </summary>
    private static CapturedFrame ReadBackFrame(
        ID3D11Device device,
        ID3D11DeviceContext context,
        Direct3D11CaptureFrame frame)
    {
        using var gpuTexture = Direct3D11Interop.GetTexture2D(frame.Surface);

        var desc = gpuTexture.Description;
        // Build a staging copy: usage=STAGING, CPU READ, no bind flags, no misc flags.
        var stagingDesc = new Texture2DDescription
        {
            Width = desc.Width,
            Height = desc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = desc.Format,
            SampleDescription = new SampleDescription(count: 1, quality: 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };

        using var staging = device.CreateTexture2D(stagingDesc);
        context.CopyResource(staging, gpuTexture);

        var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var width = (int)desc.Width;
            var height = (int)desc.Height;
            var rowStride = (int)mapped.RowPitch;

            var pixels = new byte[(long)rowStride * height];
            unsafe
            {
                fixed (byte* dst = pixels)
                {
                    Buffer.MemoryCopy(
                        source: (void*)mapped.DataPointer,
                        destination: dst,
                        destinationSizeInBytes: pixels.LongLength,
                        sourceBytesToCopy: pixels.LongLength);
                }
            }

            return new CapturedFrame(width, height, rowStride, pixels);
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }
}
