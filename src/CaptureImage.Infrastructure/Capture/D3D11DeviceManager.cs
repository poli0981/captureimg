using System.Runtime.Versioning;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Microsoft.Extensions.Logging;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// Owns a lazily-created, thread-safe <see cref="ID3D11Device"/> used by the capture pipeline.
/// Single instance — re-creating devices on every capture is expensive (10-30ms) and wastes GPU
/// memory. The device is re-created only if a previous capture observed a <c>DEVICE_LOST</c>
/// error (signalled externally via <see cref="Invalidate"/>).
/// </summary>
[SupportedOSPlatform("windows10.0.22621.0")]
public sealed class D3D11DeviceManager : IDisposable
{
    private readonly ILogger<D3D11DeviceManager> _logger;
    private readonly object _gate = new();
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private bool _disposed;

    public D3D11DeviceManager(ILogger<D3D11DeviceManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get the cached D3D11 device, creating it on first use. The returned device is owned
    /// by the manager — do NOT dispose it from the caller.
    /// </summary>
    public (ID3D11Device Device, ID3D11DeviceContext Context) Get()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_device is null || _context is null)
            {
                CreateDevice_NoLock();
            }
            return (_device!, _context!);
        }
    }

    /// <summary>
    /// Mark the current device as invalid. Next call to <see cref="Get"/> will recreate it.
    /// Call this after a DEVICE_LOST or DEVICE_REMOVED HRESULT.
    /// </summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            DisposeDevice_NoLock();
            _logger.LogInformation("D3D11 device invalidated and will be recreated on next use.");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            DisposeDevice_NoLock();
        }
    }

    private void CreateDevice_NoLock()
    {
        // BGRA support is REQUIRED for Windows.Graphics.Capture interop — WGC hands us a
        // B8G8R8A8UIntNormalized surface and the device must understand that format.
        var flags = DeviceCreationFlags.BgraSupport;

        // Try feature level 11.1 first, fall back to 11.0 on older adapters.
        var featureLevels = new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 };

        var hr = D3D11.D3D11CreateDevice(
            adapter: null,
            DriverType.Hardware,
            flags,
            featureLevels,
            out _device,
            out _context);

        if (hr.Failure || _device is null || _context is null)
        {
            throw new InvalidOperationException(
                $"D3D11CreateDevice(Hardware) failed: HRESULT 0x{hr.Code:X8}.");
        }

        _logger.LogDebug("D3D11 device created (feature level {Level}).", _device.FeatureLevel);
    }

    private void DisposeDevice_NoLock()
    {
        _context?.Dispose();
        _device?.Dispose();
        _context = null;
        _device = null;
    }
}
