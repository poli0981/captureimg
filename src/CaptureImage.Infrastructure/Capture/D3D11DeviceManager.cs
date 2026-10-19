// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using Microsoft.Extensions.Logging;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Windows.Graphics.DirectX.Direct3D11;

namespace CaptureImage.Infrastructure.Capture;

/// <summary>
/// Lazily created Direct3D 11 device shared by all captures, plus its WinRT wrapper. After a
/// GPU reset or driver update the engine calls <see cref="Invalidate"/> and the next capture
/// gets a fresh device (v1 never recreated it, so every capture failed until restart).
/// </summary>
public sealed partial class D3D11DeviceManager : IDisposable
{
    private readonly ILogger<D3D11DeviceManager> _logger;
    private readonly Lock _gate = new();
    private Session? _session;
    private bool _disposed;

    public D3D11DeviceManager(ILogger<D3D11DeviceManager> logger)
    {
        _logger = logger;
    }

    /// <summary>The device, its immediate context and the WinRT wrapper WGC needs.</summary>
    internal sealed record Session(ID3D11Device Device, ID3D11DeviceContext Context, IDirect3DDevice WinRtDevice) : IDisposable
    {
        public void Dispose()
        {
            WinRtDevice.Dispose();
            Context.Dispose();
            Device.Dispose();
        }
    }

    internal Session Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _session ??= Create();
        }
    }

    /// <summary>Drop the current device; the next <see cref="Acquire"/> creates a new one.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _session?.Dispose();
            _session = null;
        }
        LogInvalidated();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _session?.Dispose();
            _session = null;
        }
    }

    private Session Create()
    {
        // BGRA support is required for WGC interop. Fall back to WARP on machines without a
        // usable GPU driver (virtual machines, CI runners).
        FeatureLevel[] levels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0];
        var driver = DriverType.Hardware;
        var result = D3D11.D3D11CreateDevice(null, driver, DeviceCreationFlags.BgraSupport, levels, out ID3D11Device device, out ID3D11DeviceContext context);
        if (result.Failure)
        {
            driver = DriverType.Warp;
            result = D3D11.D3D11CreateDevice(null, driver, DeviceCreationFlags.BgraSupport, levels, out device, out context);
        }

        if (result.Failure)
        {
            throw new InvalidOperationException($"D3D11CreateDevice failed: HRESULT 0x{result.Code:X8}.");
        }

        LogCreated(driver, device.FeatureLevel);
        return new Session(device, context, WinRtCaptureInterop.CreateDirect3DDevice(device));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "D3D11 device created ({Driver}, feature level {Level}).")]
    private partial void LogCreated(DriverType driver, FeatureLevel level);

    [LoggerMessage(Level = LogLevel.Information, Message = "D3D11 device dropped; it will be recreated on the next capture.")]
    private partial void LogInvalidated();
}
